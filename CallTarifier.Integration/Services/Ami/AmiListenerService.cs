using System.Globalization;
using System.Net.Sockets;
using System.Text;
using CallTarifier.Integration.Configuration;
using CallTarifier.Integration.Models;
using CallTarifier.Integration.Services.Cdr;
using Microsoft.Extensions.Options;

namespace CallTarifier.Integration.Services.Ami;

public sealed class AmiListenerService : IAmiListenerService
{
    private readonly AsteriskOptions _options;
    private readonly ICdrQueue _cdrQueue;
    private readonly ILogger<AmiListenerService> _logger;
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;

    public AmiListenerService(
        IOptions<AsteriskOptions> options,
        ICdrQueue cdrQueue,
        ILogger<AmiListenerService> logger)
    {
        _options = options.Value;
        _cdrQueue = cdrQueue;
        _logger = logger;
    }

    public bool IsConnected { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Secret))
        {
            _logger.LogError("AMI secret is empty. Set Asterisk:Secret via User Secrets or environment.");
            return Task.CompletedTask;
        }

        _loopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loopTask = Task.Run(() => RunLoopAsync(_loopCts.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_loopCts is not null)
        {
            await _loopCts.CancelAsync();
        }

        if (_loopTask is not null)
        {
            try
            {
                await _loopTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch
            {
                // ignored
            }
        }

        IsConnected = false;
        _logger.LogInformation("AMI disconnected");
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAndListenAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                IsConnected = false;
                _logger.LogError(ex, "AMI connection error; retrying in 5s");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task ConnectAndListenAsync(CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        _logger.LogInformation("Connecting AMI to {Host}:{Port}", _options.Host, _options.Port);
        await client.ConnectAsync(_options.Host, _options.Port, cancellationToken);
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
        await using var writer = new StreamWriter(stream, Encoding.ASCII, 1024, leaveOpen: true) { NewLine = "\r\n", AutoFlush = true };

        var banner = await reader.ReadLineAsync(cancellationToken);
        _logger.LogDebug("AMI banner: {Banner}", banner);

        await writer.WriteLineAsync("Action: Login");
        await writer.WriteLineAsync($"Username: {_options.Username}");
        await writer.WriteLineAsync($"Secret: {_options.Secret}");
        await writer.WriteLineAsync("Events: on");
        await writer.WriteLineAsync();

        var loginPacket = await ReadPacketAsync(reader, cancellationToken);
        if (!loginPacket.TryGetValue("Response", out var response) ||
            !string.Equals(response, "Success", StringComparison.OrdinalIgnoreCase))
        {
            var message = loginPacket.GetValueOrDefault("Message") ?? "Login failed";
            throw new InvalidOperationException($"AMI login failed: {message}");
        }

        IsConnected = true;
        _logger.LogInformation("AMI connected");

        while (!cancellationToken.IsCancellationRequested && client.Connected)
        {
            var packet = await ReadPacketAsync(reader, cancellationToken);
            if (packet.Count == 0)
            {
                continue;
            }

            if (!packet.TryGetValue("Event", out var eventName))
            {
                continue;
            }

            if (string.Equals(eventName, "Cdr", StringComparison.OrdinalIgnoreCase))
            {
                var cdr = MapCdr(packet);
                await _cdrQueue.WriteAsync(cdr, cancellationToken);
                _logger.LogInformation(
                    "CDR received UniqueId={UniqueId} {Source}->{Destination} Disposition={Disposition} Duration={Duration}",
                    cdr.UniqueId,
                    cdr.Source,
                    cdr.Destination,
                    cdr.Disposition,
                    cdr.DurationSeconds);
            }
            else if (string.Equals(eventName, "Hangup", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug(
                    "Hangup UniqueId={UniqueId} Channel={Channel} Cause={Cause}",
                    packet.GetValueOrDefault("Uniqueid") ?? packet.GetValueOrDefault("UniqueID"),
                    packet.GetValueOrDefault("Channel"),
                    packet.GetValueOrDefault("Cause"));
            }
        }

        IsConnected = false;
        _logger.LogWarning("AMI disconnected");
    }

    private static async Task<Dictionary<string, string>> ReadPacketAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var packet = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            if (line.Length == 0)
            {
                break;
            }

            var idx = line.IndexOf(':');
            if (idx <= 0)
            {
                continue;
            }

            var key = line[..idx].Trim();
            var value = line[(idx + 1)..].Trim();
            packet[key] = value;
        }

        return packet;
    }

    private static CdrRecord MapCdr(IReadOnlyDictionary<string, string> packet)
    {
        static string? Get(IReadOnlyDictionary<string, string> p, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (p.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }

        static DateTime? ParseAmiDate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || value == "0" || value.StartsWith("0000", StringComparison.Ordinal))
            {
                return null;
            }

            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
            {
                return dt;
            }

            return null;
        }

        static int ParseInt(string? value)
            => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;

        var source = NormalizeEndpoint(Get(packet, "Source", "src", "CallerIDNum"));
        var destination = NormalizeEndpoint(Get(packet, "Destination", "dst", "Exten"));

        return new CdrRecord
        {
            UniqueId = Get(packet, "UniqueID", "Uniqueid", "uniqueid"),
            LinkedId = Get(packet, "Linkedid", "LinkedID", "linkedid"),
            Source = source,
            Destination = destination,
            Channel = Get(packet, "Channel"),
            DestinationChannel = Get(packet, "DestinationChannel", "DestChannel"),
            StartTime = ParseAmiDate(Get(packet, "StartTime", "start")),
            AnswerTime = ParseAmiDate(Get(packet, "AnswerTime", "answer")),
            EndTime = ParseAmiDate(Get(packet, "EndTime", "end")),
            DurationSeconds = ParseInt(Get(packet, "Duration", "duration")),
            BillableSeconds = ParseInt(Get(packet, "BillableSeconds", "billsec")),
            Disposition = Get(packet, "Disposition", "disposition"),
            AccountCode = Get(packet, "AccountCode", "accountcode"),
            ReceivedAt = DateTime.UtcNow
        };
    }

    private static string? NormalizeEndpoint(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        // Strip SIP/PJSIP channel prefixes if present: PJSIP/4000-00000001 -> 4000
        var v = value.Trim();
        if (v.Contains('/'))
        {
            var afterSlash = v.Split('/')[^1];
            var dash = afterSlash.IndexOf('-');
            v = dash > 0 ? afterSlash[..dash] : afterSlash;
        }

        // CallerID like "Name" <4000>
        var lt = v.LastIndexOf('<');
        var gt = v.LastIndexOf('>');
        if (lt >= 0 && gt > lt)
        {
            v = v[(lt + 1)..gt];
        }

        return v.Trim();
    }
}
