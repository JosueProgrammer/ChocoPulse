using CallTarifier.Integration.Models;

namespace CallTarifier.Integration.Services.Cdr;

public sealed class CdrProcessor : ICdrProcessor
{
    private readonly ICdrQueue _queue;
    private readonly ILogger<CdrProcessor> _logger;
    private readonly List<CdrRecord> _buffer = [];
    private readonly object _sync = new();

    public CdrProcessor(ICdrQueue queue, ILogger<CdrProcessor> logger)
    {
        _queue = queue;
        _logger = logger;
    }

    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        // Drain available CDR records into a short-lived buffer for CallProcessor.
        while (_queue is CdrQueue)
        {
            // Non-blocking drain via TryRead on the underlying channel is not exposed;
            // CallProcessor consumes ReadAllAsync in background. This method remains
            // for Worker compatibility and logging.
            break;
        }

        await Task.CompletedTask;
        _logger.LogDebug("CDR processor heartbeat");
    }

    public static string MapStatus(CdrRecord cdr)
    {
        var disposition = (cdr.Disposition ?? string.Empty).Trim().ToUpperInvariant();
        return disposition switch
        {
            "ANSWERED" or "ANSWER" => CallStatuses.Completed,
            "BUSY" => CallStatuses.Busy,
            "FAILED" => CallStatuses.Failed,
            "CONGESTION" => CallStatuses.Failed,
            "NO ANSWER" or "NOANSWER" => CallStatuses.NoAnswer,
            "CANCEL" => CallStatuses.Rejected,
            _ when cdr.BillableSeconds <= 0 && cdr.DurationSeconds <= 0 => CallStatuses.Missed,
            _ => CallStatuses.Failed
        };
    }

    public static bool IsFinalized(CdrRecord cdr)
    {
        if (string.IsNullOrWhiteSpace(cdr.Disposition) && cdr.EndTime is null)
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(cdr.Source) || !string.IsNullOrWhiteSpace(cdr.Destination);
    }
}
