using CallTarifier.Web.Models;

namespace CallTarifier.Web.Services.Mock;

public sealed class MockFirebaseService : IFirebaseService
{
    private readonly MockDataStore _store;
    private readonly ILogger<MockFirebaseService> _logger;

    public MockFirebaseService(MockDataStore store, ILogger<MockFirebaseService> logger)
    {
        _store = store;
        _logger = logger;
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Firebase synchronization skipped (mock mode)");
        return Task.FromResult(false);
    }

    public Task<IReadOnlyList<Call>> GetCallsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<Call> calls = _store.Calls.ToList();
        return Task.FromResult(calls);
    }

    public Task<IReadOnlyList<Invoice>> GetInvoicesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<Invoice> invoices = _store.Invoices.ToList();
        return Task.FromResult(invoices);
    }
}
