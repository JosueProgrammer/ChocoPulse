using CallTarifier.Web.Models;
using CallTarifier.Web.Models.Enums;

namespace CallTarifier.Web.Services;

public sealed class FirestoreStatisticsService : IStatisticsService
{
    private readonly ICallService _callService;
    private readonly IExtensionService _extensionService;

    public FirestoreStatisticsService(ICallService callService, IExtensionService extensionService)
    {
        _callService = callService;
        _extensionService = extensionService;
    }

    public async Task<StatisticsSummary> GetSummaryAsync(
        DateTime from,
        DateTime to,
        string? extension = null,
        CancellationToken cancellationToken = default)
    {
        var calls = await _callService.GetCallsAsync(new CallFilter
        {
            From = from,
            To = to,
            Extension = extension
        }, cancellationToken);

        var userExt = extension ?? string.Empty;
        var outgoing = string.IsNullOrEmpty(userExt)
            ? calls.Count
            : calls.Count(c => c.Source == userExt);
        var incoming = string.IsNullOrEmpty(userExt)
            ? 0
            : calls.Count(c => c.Destination == userExt);
        var missed = calls.Count(c =>
            (string.IsNullOrEmpty(userExt) || c.Destination == userExt) &&
            (c.Status == CallStatus.Missed || c.Status == CallStatus.NoAnswer));

        var destinations = calls
            .Where(c => string.IsNullOrEmpty(userExt) || c.Source == userExt)
            .GroupBy(c => c.Destination)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        string? mostCalledBranch = null;
        if (destinations is not null)
        {
            var ext = await _extensionService.GetByExtensionAsync(destinations.Key, cancellationToken);
            mostCalledBranch = ext?.Branch;
        }

        return new StatisticsSummary
        {
            OutgoingCalls = outgoing,
            IncomingCalls = incoming,
            MissedCalls = missed,
            TotalDurationSeconds = calls.Sum(c => c.DurationSeconds),
            TotalCost = calls.Where(c => c.Status == CallStatus.Completed).Sum(c => c.Total),
            MostCalledExtension = destinations?.Key,
            MostCalledBranch = mostCalledBranch,
            CallsByDay = calls
                .GroupBy(c => DateOnly.FromDateTime(c.StartedAt))
                .OrderBy(g => g.Key)
                .Select(g => new DailyCallStat { Date = g.Key, Count = g.Count() })
                .ToList(),
            MonthlyConsumption = calls
                .GroupBy(c => new { c.StartedAt.Year, c.StartedAt.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new MonthlyCostStat
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    CallCount = g.Count(),
                    Total = g.Where(c => c.Status == CallStatus.Completed).Sum(c => c.Total)
                })
                .ToList()
        };
    }
}
