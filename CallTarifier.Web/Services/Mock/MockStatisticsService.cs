using CallTarifier.Web.Models;
using CallTarifier.Web.Models.Enums;

namespace CallTarifier.Web.Services.Mock;

public sealed class MockStatisticsService : IStatisticsService
{
    private readonly MockDataStore _store;
    private readonly IExtensionService _extensionService;

    public MockStatisticsService(MockDataStore store, IExtensionService extensionService)
    {
        _store = store;
        _extensionService = extensionService;
    }

    public async Task<StatisticsSummary> GetSummaryAsync(
        DateTime from,
        DateTime to,
        string? extension = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var calls = _store.Calls
            .Where(c => c.StartedAt >= from && c.StartedAt <= to)
            .Where(c => extension is null || c.Source == extension || c.Destination == extension)
            .ToList();

        var outgoing = calls.Count(c => extension is null ? c.Source == _store.CurrentUser.Extension : c.Source == extension);
        var userExt = extension ?? _store.CurrentUser.Extension;
        var incoming = calls.Count(c => c.Destination == userExt);
        var missed = calls.Count(c =>
            c.Destination == userExt &&
            (c.Status == CallStatus.Missed || c.Status == CallStatus.NoAnswer));

        var destinations = calls
            .Where(c => c.Source == userExt)
            .GroupBy(c => c.Destination)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        string? mostCalledBranch = null;
        if (destinations is not null)
        {
            var ext = await _extensionService.GetByExtensionAsync(destinations.Key, cancellationToken);
            mostCalledBranch = ext?.Branch;
        }

        var byDay = calls
            .GroupBy(c => DateOnly.FromDateTime(c.StartedAt))
            .OrderBy(g => g.Key)
            .Select(g => new DailyCallStat { Date = g.Key, Count = g.Count() })
            .ToList();

        var monthly = calls
            .GroupBy(c => new { c.StartedAt.Year, c.StartedAt.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new MonthlyCostStat
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                CallCount = g.Count(),
                Total = g.Where(c => c.Status == CallStatus.Completed).Sum(c => c.Total)
            })
            .ToList();

        return new StatisticsSummary
        {
            OutgoingCalls = outgoing,
            IncomingCalls = incoming,
            MissedCalls = missed,
            TotalDurationSeconds = calls.Sum(c => c.DurationSeconds),
            TotalCost = calls.Where(c => c.Status == CallStatus.Completed).Sum(c => c.Total),
            MostCalledExtension = destinations?.Key,
            MostCalledBranch = mostCalledBranch,
            CallsByDay = byDay,
            MonthlyConsumption = monthly
        };
    }
}
