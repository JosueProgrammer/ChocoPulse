using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using CallTarifier.Web.Configuration;
using Microsoft.Extensions.Options;

namespace CallTarifier.Web.Services;

public sealed class AmiCallService
{
    private static readonly Regex ExtensionPattern = new("^[0-9]{2,8}$", RegexOptions.Compiled);

    private readonly AsteriskOptions _options;
    private readonly PlacedCallRecorder _recorder;
    private readonly ILogger<AmiCallService> _logger;
    private readonly ConcurrentDictionary<string, LiveCall> _calls = new(StringComparer.Ordinal);

    public AmiCallService(
        IOptions<AsteriskOptions> options,
        PlacedCallRecorder recorder,
        ILogger<AmiCallService> logger)
    {
        _options = options.Value;
        _recorder = recorder;
        _logger = logger;
    }

    public LiveCallStatus GetStatus(string userId)
    {
        if (!_calls.TryGetValue(userId, out var call))
        {
            return new LiveCallStatus("idle", null, null, 0);
        }

        var elapsed = call.State == "connected"
            ? (int)Math.Max(0, (DateTimeOffset.UtcNow - (call.ConnectedUtc ?? call.StartedUtc)).TotalSeconds)
            : 0;
        return new LiveCallStatus(call.State, call.Destination, call.Error, elapsed);
    }

    public async Task<string?> DialAsync(string userId, string callerExtension, string callerName, string destination, CancellationToken cancellationToken)
    {
        if (!ExtensionPattern.IsMatch(callerExtension) || !ExtensionPattern.IsMatch(destination))
        {
            return "Extensión no válida.";
        }

        if (string.Equals(callerExtension, destination, StringComparison.Ordinal))
        {
            return "No puedes llamar a tu propia extensión.";
        }

        if (string.IsNullOrWhiteSpace(_options.Host) || string.IsNullOrWhiteSpace(_options.Secret))
        {
            return "La central no está configurada para marcar.";
        }

        await HangupAsync(userId);

        var call = new LiveCall(callerExtension, destination, DateTimeOffset.UtcNow)
        {
            ThroughRoute = destination == "9000"
        };
        _calls[userId] = call;

        TcpClient? client = null;
        try
        {
            client = new TcpClient();
            using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectTimeout.CancelAfter(TimeSpan.FromSeconds(4));
            await client.ConnectAsync(_options.Host, _options.Port, connectTimeout.Token);
            var stream = client.GetStream();
            var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
            var writer = new StreamWriter(stream, Encoding.ASCII, 1024, leaveOpen: true)
            {
                NewLine = "\r\n",
                AutoFlush = true
            };

            _ = await reader.ReadLineAsync(connectTimeout.Token);
            await WriteAsync(writer, connectTimeout.Token,
                "Action: Login",
                $"Username: {_options.Username}",
                $"Secret: {_options.Secret}",
                "Events: on");
            var login = await ReadPacketAsync(reader, connectTimeout.Token);
            if (!IsSuccess(login))
            {
                _calls.TryRemove(userId, out _);
                client.Dispose();
                return "No se pudo entrar a la central.";
            }

            var actionId = "ct-" + Guid.NewGuid().ToString("N");
            call.ActionId = actionId;
            var name = SanitizeName(callerName);
            // 9000 is the trunk route to the other E1, not a local PJSIP endpoint.
            // Ring the caller's Zoiper first, then let from-internal dial 9000.
            var channel = call.ThroughRoute ? $"PJSIP/{callerExtension}" : $"PJSIP/{destination}";
            var exten = call.ThroughRoute ? destination : callerExtension;
            await WriteAsync(writer, connectTimeout.Token,
                "Action: Originate",
                $"ActionID: {actionId}",
                $"Channel: {channel}",
                "Context: from-internal",
                $"Exten: {exten}",
                "Priority: 1",
                $"CallerID: \"{name}\" <{callerExtension}>",
                "Async: true",
                "Timeout: 45000");

            var queued = await ReadResponseAsync(reader, connectTimeout.Token, call);
            if (!IsSuccess(queued))
            {
                var reason = queued.GetValueOrDefault("Message") ?? "La central rechazó la llamada.";
                _calls.TryRemove(userId, out _);
                client.Dispose();
                _logger.LogWarning("Originate rejected for {Destination}: {Reason}", destination, reason);
                return "La extensión no está disponible en Zoiper.";
            }

            call.State = "ringing";
            var ownedClient = client;
            client = null;
            _ = Task.Run(() => ReadEventsAsync(ownedClient, reader, call), CancellationToken.None);
            _logger.LogInformation("Dial {Caller} -> {Destination} queued", callerExtension, destination);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _calls.TryRemove(userId, out _);
            client?.Dispose();
            _logger.LogWarning(ex, "AMI dial failed for {Destination}", destination);
            return "No se pudo timbrar la extensión.";
        }
    }

    public async Task HangupAsync(string userId)
    {
        if (!_calls.TryRemove(userId, out var call))
        {
            return;
        }

        call.State = "ended";
        await SaveFinishedCallAsync(call);
        call.Cancel.Cancel();
        var channels = call.Channels.ToArray();
        if (channels.Length == 0 || string.IsNullOrWhiteSpace(_options.Secret))
        {
            return;
        }

        try
        {
            using var client = new TcpClient();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await client.ConnectAsync(_options.Host, _options.Port, timeout.Token);
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
            await using var writer = new StreamWriter(stream, Encoding.ASCII, 1024, leaveOpen: true)
            {
                NewLine = "\r\n",
                AutoFlush = true
            };

            _ = await reader.ReadLineAsync(timeout.Token);
            await WriteAsync(writer, timeout.Token,
                "Action: Login",
                $"Username: {_options.Username}",
                $"Secret: {_options.Secret}",
                "Events: off");
            _ = await ReadPacketAsync(reader, timeout.Token);
            foreach (var channel in channels)
            {
                await WriteAsync(writer, timeout.Token, "Action: Hangup", $"Channel: {channel}");
                _ = await ReadPacketAsync(reader, timeout.Token);
            }

            await WriteAsync(writer, timeout.Token, "Action: Logoff");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "AMI hangup failed");
        }
    }

    private async Task ReadEventsAsync(TcpClient client, StreamReader reader, LiveCall call)
    {
        using (client)
        {
            try
            {
                while (!call.Cancel.IsCancellationRequested && call.State is "calling" or "ringing" or "connected")
                {
                    var packet = await ReadPacketAsync(reader, call.Cancel.Token);
                    if (packet.Count == 0)
                    {
                        break;
                    }

                    ApplyEvent(call, packet);
                    if (call.State is "ended" or "failed")
                    {
                        await SaveFinishedCallAsync(call);
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Hangup requested.
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AMI call events ended");
                if (call.State is "calling" or "ringing" or "connected")
                {
                    call.State = "ended";
                    await SaveFinishedCallAsync(call);
                }
            }
        }
    }

    private static void ApplyEvent(LiveCall call, Dictionary<string, string> packet)
    {
        var eventName = packet.GetValueOrDefault("Event") ?? string.Empty;
        RememberChannel(call, packet.GetValueOrDefault("Channel"));
        RememberChannel(call, packet.GetValueOrDefault("DestChannel"));
        RememberIdentity(call, packet);

        if (eventName.Equals("OriginateResponse", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(packet.GetValueOrDefault("ActionID"), call.ActionId, StringComparison.OrdinalIgnoreCase) &&
            !IsSuccess(packet) &&
            call.Channels.IsEmpty)
        {
            call.Error = "Zoiper no contestó o la extensión no está registrada.";
            call.State = "failed";
            return;
        }

        var state = packet.GetValueOrDefault("ChannelStateDesc") ?? string.Empty;
        var channel = packet.GetValueOrDefault("Channel") ?? string.Empty;
        var destChannel = packet.GetValueOrDefault("DestChannel");
        var tracked = IsOurChannel(call, channel);
        if (tracked && !string.IsNullOrWhiteSpace(destChannel))
        {
            call.Channels.TryAdd(destChannel, 0);
        }

        var dialStatus = packet.GetValueOrDefault("DialStatus") ?? string.Empty;
        if (call.ThroughRoute)
        {
            if (tracked && state.Contains("Ring", StringComparison.OrdinalIgnoreCase) && call.State != "connected")
            {
                call.RingingUtc ??= DateTimeOffset.UtcNow;
                call.State = "ringing";
            }

            if (dialStatus.Equals("ANSWER", StringComparison.OrdinalIgnoreCase) &&
                (tracked || IsOurChannel(call, destChannel)))
            {
                call.ConnectedUtc ??= DateTimeOffset.UtcNow;
                call.State = "connected";
            }
            else if (eventName.Equals("DialEnd", StringComparison.OrdinalIgnoreCase) &&
                     tracked &&
                     call.State != "connected" &&
                     dialStatus.Length > 0)
            {
                call.Error = dialStatus.Equals("CHANUNAVAIL", StringComparison.OrdinalIgnoreCase) ||
                             dialStatus.Equals("CONGESTION", StringComparison.OrdinalIgnoreCase)
                    ? "No se pudo establecer la llamada al otro E1."
                    : null;
                call.State = call.Error is null ? "ended" : "failed";
            }
        }
        else if (tracked && state.Contains("Ring", StringComparison.OrdinalIgnoreCase) && call.State != "connected")
        {
            call.RingingUtc ??= DateTimeOffset.UtcNow;
            call.State = "ringing";
        }
        else if (tracked && (state.Equals("Up", StringComparison.OrdinalIgnoreCase) ||
                             dialStatus.Equals("ANSWER", StringComparison.OrdinalIgnoreCase)))
        {
            call.ConnectedUtc ??= DateTimeOffset.UtcNow;
            call.State = "connected";
        }

        if (eventName.Equals("Hangup", StringComparison.OrdinalIgnoreCase) && tracked)
        {
            call.Channels.TryRemove(channel, out _);
            if (call.Channels.IsEmpty && call.State != "failed")
            {
                call.State = "ended";
            }
        }
    }

    private async Task SaveFinishedCallAsync(LiveCall call)
    {
        if (Interlocked.Exchange(ref call.Saved, 1) != 0)
        {
            return;
        }

        try
        {
            await _recorder.SaveAsync(new FinishedCall(
                call.CallerExtension,
                call.Destination,
                call.StartedUtc,
                call.RingingUtc,
                call.ConnectedUtc,
                call.UniqueId,
                call.State == "failed"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not save call {Source}->{Destination}", call.CallerExtension, call.Destination);
        }
    }

    private static void RememberIdentity(LiveCall call, Dictionary<string, string> packet)
    {
        if (!string.IsNullOrWhiteSpace(call.UniqueId))
        {
            return;
        }

        var unique = packet.GetValueOrDefault("Uniqueid") ?? packet.GetValueOrDefault("UniqueID");
        var channel = packet.GetValueOrDefault("Channel") ?? packet.GetValueOrDefault("DestChannel");
        if (!string.IsNullOrWhiteSpace(unique) && IsOurChannel(call, channel))
        {
            call.UniqueId = unique;
        }
    }

    private static void RememberChannel(LiveCall call, string? channel)
    {
        if (IsOurChannel(call, channel))
        {
            call.Channels.TryAdd(channel!, 0);
        }
    }

    private static bool IsOurChannel(LiveCall call, string? channel)
    {
        if (string.IsNullOrWhiteSpace(channel))
        {
            return false;
        }

        return channel.Contains($"PJSIP/{call.Destination}-", StringComparison.OrdinalIgnoreCase) ||
               channel.Contains($"PJSIP/{call.CallerExtension}-", StringComparison.OrdinalIgnoreCase) ||
               channel.StartsWith($"PJSIP/{call.Destination}", StringComparison.OrdinalIgnoreCase) ||
               channel.StartsWith($"PJSIP/{call.CallerExtension}", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSuccess(Dictionary<string, string> packet)
        => packet.TryGetValue("Response", out var response) &&
           response.Equals("Success", StringComparison.OrdinalIgnoreCase);

    private static string SanitizeName(string name)
    {
        var cleaned = new string(name.Where(ch => ch is not '\r' and not '\n' and not '"' and not ':').ToArray()).Trim();
        if (cleaned.Length > 40)
        {
            cleaned = cleaned[..40];
        }

        return string.IsNullOrWhiteSpace(cleaned) ? "Extension" : cleaned;
    }

    private static async Task WriteAsync(StreamWriter writer, CancellationToken cancellationToken, params string[] lines)
    {
        foreach (var line in lines)
        {
            await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
        }

        await writer.WriteLineAsync(string.Empty.AsMemory(), cancellationToken);
    }

    private static async Task<Dictionary<string, string>> ReadResponseAsync(StreamReader reader, CancellationToken cancellationToken, LiveCall call)
    {
        while (true)
        {
            var packet = await ReadPacketAsync(reader, cancellationToken);
            if (packet.Count == 0 || packet.ContainsKey("Response"))
            {
                return packet;
            }

            if (packet.ContainsKey("Event"))
            {
                ApplyEvent(call, packet);
            }
        }
    }

    private static async Task<Dictionary<string, string>> ReadPacketAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var packet = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null || line.Length == 0)
            {
                return packet;
            }

            var split = line.IndexOf(':');
            if (split <= 0)
            {
                continue;
            }

            packet[line[..split].Trim()] = line[(split + 1)..].Trim();
        }
    }

    private sealed class LiveCall
    {
        public LiveCall(string callerExtension, string destination, DateTimeOffset startedUtc)
        {
            CallerExtension = callerExtension;
            Destination = destination;
            StartedUtc = startedUtc;
        }

        public string CallerExtension { get; }

        public string Destination { get; }

        public bool ThroughRoute { get; init; }

        public DateTimeOffset StartedUtc { get; }

        public string State { get; set; } = "calling";

        public string? Error { get; set; }

        public string ActionId { get; set; } = string.Empty;

        public string? UniqueId { get; set; }

        public DateTimeOffset? RingingUtc { get; set; }

        public DateTimeOffset? ConnectedUtc { get; set; }

        public int Saved;

        public ConcurrentDictionary<string, byte> Channels { get; } = new(StringComparer.OrdinalIgnoreCase);

        public CancellationTokenSource Cancel { get; } = new();
    }
}

public sealed record LiveCallStatus(string State, string? Destination, string? Error, int ElapsedSeconds);
