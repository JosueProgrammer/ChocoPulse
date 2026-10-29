using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using CallTarifier.Web.Configuration;
using CallTarifier.Web.Models;
using Microsoft.Extensions.Options;

namespace CallTarifier.Web.Services;

public sealed class IssabelExtensionRefresher
{
    private static readonly Regex EndpointPattern = new(@"Endpoint:\s+(\d{2,8})(?:/|\s)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NamePattern = new(@"/AMPUSER/(\d{2,8})/cidname\s*:\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

    private readonly AsteriskOptions _options;
    private readonly ILogger<IssabelExtensionRefresher> _logger;

    public IssabelExtensionRefresher(IOptions<AsteriskOptions> options, ILogger<IssabelExtensionRefresher> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ExtensionInfo>?> TryLoadAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Host) ||
            string.IsNullOrWhiteSpace(_options.Username) ||
            string.IsNullOrWhiteSpace(_options.Secret))
        {
            return null;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        var token = timeout.Token;

        using var client = new TcpClient();
        await client.ConnectAsync(_options.Host, _options.Port, token);
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen: true);
        await using var writer = new StreamWriter(stream, Encoding.ASCII, 1024, leaveOpen: true)
        {
            NewLine = "\r\n",
            AutoFlush = true
        };

        _ = await reader.ReadLineAsync(token);
        await WriteAsync(writer, token, "Action: Login", $"Username: {_options.Username}", $"Secret: {_options.Secret}", "Events: off");
        var login = await ReadBlockAsync(reader, token);
        if (!login.Contains("Response: Success", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("AMI login failed while refreshing extensions");
            return null;
        }

        var endpoints = await CommandAsync(writer, reader, "pjsip show endpoints", token);
        var names = await CommandAsync(writer, reader, "database show AMPUSER", token);
        await WriteAsync(writer, token, "Action: Logoff");

        var nameByExtension = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in NamePattern.Matches(names))
        {
            var value = match.Groups[2].Value.Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                nameByExtension[match.Groups[1].Value] = value;
            }
        }

        var result = new List<ExtensionInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in EndpointPattern.Matches(endpoints))
        {
            var extension = match.Groups[1].Value;
            if (!seen.Add(extension))
            {
                continue;
            }

            var branch = nameByExtension.TryGetValue(extension, out var name)
                && !string.Equals(name, extension, StringComparison.Ordinal)
                ? name
                : ExtensionDirectory.NameFor(extension);
            if (branch == "Desconocida")
            {
                branch = extension;
            }

            result.Add(new ExtensionInfo
            {
                Extension = extension,
                Branch = branch,
                Status = "Disponible"
            });
        }

        return result.OrderBy(item => item.Extension).ToList();
    }

    private static async Task<string> CommandAsync(StreamWriter writer, StreamReader reader, string command, CancellationToken cancellationToken)
    {
        await WriteAsync(writer, cancellationToken, "Action: Command", $"Command: {command}");
        return await ReadBlockAsync(reader, cancellationToken);
    }

    private static async Task WriteAsync(StreamWriter writer, CancellationToken cancellationToken, params string[] lines)
    {
        foreach (var line in lines)
        {
            await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
        }

        await writer.WriteLineAsync(string.Empty.AsMemory(), cancellationToken);
    }

    private static async Task<string> ReadBlockAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null || line.Length == 0)
            {
                break;
            }

            builder.AppendLine(line);
            if (line.Contains("--END COMMAND--", StringComparison.Ordinal))
            {
                break;
            }
        }

        return builder.ToString();
    }
}
