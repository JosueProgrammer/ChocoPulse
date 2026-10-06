namespace CallTarifier.Integration.Services.Invoices;

public sealed class InvoiceService : IInvoiceService
{
    private readonly ILogger<InvoiceService> _logger;

    public InvoiceService(ILogger<InvoiceService> logger)
    {
        _logger = logger;
    }

    public Task GeneratePendingAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogDebug("Invoice service idle (Phase 8 pending)");
        return Task.CompletedTask;
    }
}
