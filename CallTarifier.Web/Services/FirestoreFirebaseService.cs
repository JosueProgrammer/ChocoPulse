using CallTarifier.Web.Models;

namespace CallTarifier.Web.Services;

public sealed class FirestoreFirebaseService : IFirebaseService
{
    private readonly IFirestoreDbFactory _dbFactory;
    private readonly ICallService _callService;
    private readonly IInvoiceService _invoiceService;
    private readonly ILogger<FirestoreFirebaseService> _logger;

    public FirestoreFirebaseService(
        IFirestoreDbFactory dbFactory,
        ICallService callService,
        IInvoiceService invoiceService,
        ILogger<FirestoreFirebaseService> logger)
    {
        _dbFactory = dbFactory;
        _callService = callService;
        _invoiceService = invoiceService;
        _logger = logger;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var db = await _dbFactory.GetDbAsync(cancellationToken);
            await db.Collection("settings").Limit(1).GetSnapshotAsync(cancellationToken);
            _logger.LogInformation("Firebase synchronization successful");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Firebase not available");
            return false;
        }
    }

    public Task<IReadOnlyList<Call>> GetCallsAsync(CancellationToken cancellationToken = default)
        => _callService.GetCallsAsync(cancellationToken: cancellationToken);

    public Task<IReadOnlyList<Invoice>> GetInvoicesAsync(CancellationToken cancellationToken = default)
        => _invoiceService.GetInvoicesAsync(cancellationToken);
}
