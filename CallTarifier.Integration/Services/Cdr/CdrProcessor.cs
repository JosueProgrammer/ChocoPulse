namespace CallTarifier.Integration.Services.Cdr;

public sealed class CdrProcessor : ICdrProcessor
{
    private readonly ILogger<CdrProcessor> _logger;

    public CdrProcessor(ILogger<CdrProcessor> logger)
    {
        _logger = logger;
    }

    public Task ProcessAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogDebug("CDR processor idle (Phase 5 pending)");
        return Task.CompletedTask;
    }
}
