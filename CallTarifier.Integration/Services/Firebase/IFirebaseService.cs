using CallTarifier.Integration.Models;

namespace CallTarifier.Integration.Services.Firebase;

public interface IFirebaseService
{
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);

    Task SynchronizeAsync(CancellationToken cancellationToken);

    Task EnsureSeedAsync(CancellationToken cancellationToken);

    Task<bool> CallExistsByUniqueIdAsync(string uniqueId, CancellationToken cancellationToken);

    Task SaveCallAsync(CallRecord call, CancellationToken cancellationToken);

    Task SaveInvoiceAsync(InvoiceRecord invoice, CancellationToken cancellationToken);

    Task<string> NextInvoiceNumberAsync(CancellationToken cancellationToken);
}
