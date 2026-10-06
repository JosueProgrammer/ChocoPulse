using CallTarifier.Web.Models;

namespace CallTarifier.Web.Services;

public interface ICallService
{
    Task<IReadOnlyList<Call>> GetCallsAsync(CallFilter? filter = null, CancellationToken cancellationToken = default);

    Task<Call?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<DashboardSummary> GetDashboardAsync(string extension, CancellationToken cancellationToken = default);
}
