using CallTarifier.Web.Models;

namespace CallTarifier.Web.Services;

public interface IStatisticsService
{
    Task<StatisticsSummary> GetSummaryAsync(
        DateTime from,
        DateTime to,
        string? extension = null,
        CancellationToken cancellationToken = default);
}
