using CallTarifier.Web.Models;

namespace CallTarifier.Web.Services.Mock;

public sealed class MockInvoiceService : IInvoiceService
{
    private readonly MockDataStore _store;
    private readonly ILogger<MockInvoiceService> _logger;

    public MockInvoiceService(MockDataStore store, ILogger<MockInvoiceService> logger)
    {
        _store = store;
        _logger = logger;
    }

    public Task<IReadOnlyList<Invoice>> GetInvoicesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<Invoice> invoices = _store.Invoices
            .OrderByDescending(i => i.IssuedAt)
            .ToList();

        _logger.LogDebug("Mock invoices returned {Count}", invoices.Count);
        return Task.FromResult(invoices);
    }

    public Task<Invoice?> GetByNumberAsync(string number, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var invoice = _store.Invoices.FirstOrDefault(i =>
            i.Number.Equals(number, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(invoice);
    }

    public Task<MonthlyBilling?> GetCurrentPeriodAsync(string extension, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = DateTime.Now;
        var period = _store.MonthlyBillings.FirstOrDefault(m =>
            m.Extension == extension && m.Year == now.Year && m.Month == now.Month);
        return Task.FromResult(period);
    }
}
