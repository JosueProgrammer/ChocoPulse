using CallTarifier.Integration.Configuration;
using CallTarifier.Integration.Models;
using CallTarifier.Integration.Services.Cdr;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace CallTarifier.Integration.Services.Cdr;

public interface ICdrBackfillService
{
    Task RunOnceAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Optional historical reproceso from asteriskcdrdb.cdr (not the live primary source).
/// </summary>
public sealed class CdrBackfillService : ICdrBackfillService
{
    private readonly CdrDatabaseOptions _options;
    private readonly ApplicationOptions _application;
    private readonly ICdrQueue _queue;
    private readonly ILogger<CdrBackfillService> _logger;
    private bool _ran;

    public CdrBackfillService(
        IOptions<CdrDatabaseOptions> options,
        IOptions<ApplicationOptions> application,
        ICdrQueue queue,
        ILogger<CdrBackfillService> logger)
    {
        _options = options.Value;
        _application = application.Value;
        _queue = queue;
        _logger = logger;
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        if (_ran || !_application.EnableCdrBackfill || !_options.Enabled ||
            string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            return;
        }

        _ran = true;
        _logger.LogInformation("Starting CDR backfill from asteriskcdrdb");

        try
        {
            await using var conn = new MySqlConnection(_options.ConnectionString);
            await conn.OpenAsync(cancellationToken);

            const string sql = """
                SELECT uniqueid, linkedid, src, dst, channel, dstchannel,
                       calldate, duration, billsec, disposition, accountcode
                FROM cdr
                WHERE calldate >= DATE_SUB(NOW(), INTERVAL 7 DAY)
                ORDER BY calldate ASC
                LIMIT 500
                """;

            await using var cmd = new MySqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var count = 0;

            while (await reader.ReadAsync(cancellationToken))
            {
                var start = reader.GetDateTime(reader.GetOrdinal("calldate"));
                var duration = Convert.ToInt32(reader["duration"] ?? 0);
                var billsec = Convert.ToInt32(reader["billsec"] ?? 0);
                var disposition = reader["disposition"]?.ToString();
                var answered = string.Equals(disposition, "ANSWERED", StringComparison.OrdinalIgnoreCase);

                var cdr = new CdrRecord
                {
                    UniqueId = reader["uniqueid"]?.ToString(),
                    LinkedId = reader["linkedid"]?.ToString(),
                    Source = reader["src"]?.ToString(),
                    Destination = reader["dst"]?.ToString(),
                    Channel = reader["channel"]?.ToString(),
                    DestinationChannel = reader["dstchannel"]?.ToString(),
                    StartTime = start,
                    AnswerTime = answered ? start : null,
                    EndTime = start.AddSeconds(duration),
                    DurationSeconds = duration,
                    BillableSeconds = billsec,
                    Disposition = disposition,
                    AccountCode = reader["accountcode"]?.ToString(),
                    ReceivedAt = DateTime.UtcNow
                };

                await _queue.WriteAsync(cdr, cancellationToken);
                count++;
            }

            _logger.LogInformation("CDR backfill enqueued {Count} rows", count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CDR backfill failed");
        }
    }
}
