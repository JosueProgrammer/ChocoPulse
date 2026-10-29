using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using CallTarifier.Web.Configuration;
using Microsoft.Extensions.Options;

namespace CallTarifier.Web.Services;

public sealed class AmiSipSecretReader
{
    private static readonly Regex AuthNamePattern = new(@"\b(?:in)?auth\s*:\s*(\S+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PasswordPattern = new(@"\bpassword\s*:\s*(\S+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly AsteriskOptions _options;
    private readonly ILogger<AmiSipSecretReader> _logger;
    private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);

    public AmiSipSecretReader(IOptions<AsteriskOptions> options, ILogger<AmiSipSecretReader> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string?> TryGetAsync(string extension, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Host) ||
            string.IsNullOrWhiteSpace(_options.Username) ||
            string.IsNullOrWhiteSpace(_options.Secret) ||
            !Regex.IsMatch(extension, "^[0-9]{2,8}$"))
        {
            return null;
        }

        if (_cache.TryGetValue(extension, out var cached))
        {
            return cached;
        }

        try
        {
            var secret = await ReadFromAmiAsync(extension, cancellationToken);
            if (!string.IsNullOrWhiteSpace(secret))
            {
                _cache[extension] = secret;
                return secret;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("AMI secret lookup timed out for extension {Extension}", extension);
        }
        catch (OperationCanceledException)
        {
            // The browser left the page while the lookup was running.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read SIP secret from AMI for extension {Extension}", extension);
        }

        return null;
    }

    private async Task<string?> ReadFromAmiAsync(string extension, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(4));
        var token = timeout.Token;

        using var client = new TcpClient();
        await client.ConnectAsync(_options.Host, _options.Port, token);
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
        await using var writer = new StreamWriter(stream, Encoding.ASCII, 1024, leaveOpen: true)
        {
            NewLine = "\r\n",
            AutoFlush = true
        };

        _ = await reader.ReadLineAsync(token);
        await WriteActionAsync(writer, token,
            "Action: Login",
            $"Username: {_options.Username}",
            $"Secret: {_options.Secret}",
            "Events: off");

        var login = await ReadBlockAsync(reader, token, untilEndCommand: false);
        if (!login.Contains("Response: Success", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("AMI login failed while resolving SIP secret for {Extension}", extension);
            return null;
        }

        var endpointText = await CommandAsync(writer, reader, $"pjsip show endpoint {extension}", token);
        var authName = AuthNamePattern.Match(endpointText) is { Success: true } match
            ? match.Groups[1].Value.Trim()
            : extension;
        if (authName.Equals("no", StringComparison.OrdinalIgnoreCase) ||
            authName.Equals("<none>", StringComparison.OrdinalIgnoreCase))
        {
            authName = extension;
        }

        var authText = await CommandAsync(writer, reader, $"pjsip show auth {authName}", token);
        await WriteActionAsync(writer, token, "Action: Logoff");

        var password = PasswordPattern.Match(authText);
        if (!password.Success)
        {
            return null;
        }

        var value = password.Groups[1].Value.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith('<'))
        {
            return null;
        }

        return value;
    }

    private static async Task<string> CommandAsync(StreamWriter writer, StreamReader reader, string command, CancellationToken cancellationToken)
    {
        await WriteActionAsync(writer, cancellationToken, "Action: Command", $"Command: {command}");
        return await ReadBlockAsync(reader, cancellationToken, untilEndCommand: true);
    }

    private static async Task WriteActionAsync(StreamWriter writer, CancellationToken cancellationToken, params string[] lines)
    {
        foreach (var line in lines)
        {
            await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
        }

        await writer.WriteLineAsync(string.Empty.AsMemory(), cancellationToken);
    }

    private static async Task<string> ReadBlockAsync(StreamReader reader, CancellationToken cancellationToken, bool untilEndCommand)
    {
        var builder = new StringBuilder();
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

            builder.AppendLine(line);
            if (untilEndCommand && line.Contains("--END COMMAND--", StringComparison.Ordinal))
            {
                break;
            }
        }

        return builder.ToString();
    }
}
