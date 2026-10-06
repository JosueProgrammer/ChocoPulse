using CallTarifier.Web.Models;

namespace CallTarifier.Web.Services;

/// <summary>
/// Abstraction for Firebase operations. Phase 1 uses a mock implementation.
/// </summary>
public interface IFirebaseService
{
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Call>> GetCallsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Invoice>> GetInvoicesAsync(CancellationToken cancellationToken = default);
}
