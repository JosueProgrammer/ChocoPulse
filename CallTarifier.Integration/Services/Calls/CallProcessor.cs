namespace CallTarifier.Integration.Services.Calls;

public sealed class CallProcessor : ICallProcessor
{
    private readonly ILogger<CallProcessor> _logger;

    public CallProcessor(ILogger<CallProcessor> logger)
    {
        _logger = logger;
    }

    public Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogDebug("Call processor idle (Phase 5/6 pending)");
        return Task.CompletedTask;
    }
}
