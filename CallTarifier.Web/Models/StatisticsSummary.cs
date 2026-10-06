namespace CallTarifier.Web.Models;

public sealed class StatisticsSummary
{
    public int OutgoingCalls { get; set; }

    public int IncomingCalls { get; set; }

    public int MissedCalls { get; set; }

    public int TotalDurationSeconds { get; set; }

    public decimal TotalCost { get; set; }

    public string? MostCalledExtension { get; set; }

    public string? MostCalledBranch { get; set; }

    public IReadOnlyList<DailyCallStat> CallsByDay { get; set; } = Array.Empty<DailyCallStat>();

    public IReadOnlyList<MonthlyCostStat> MonthlyConsumption { get; set; } = Array.Empty<MonthlyCostStat>();
}

public sealed class DailyCallStat
{
    public DateOnly Date { get; set; }

    public int Count { get; set; }
}

public sealed class MonthlyCostStat
{
    public int Year { get; set; }

    public int Month { get; set; }

    public decimal Total { get; set; }

    public int CallCount { get; set; }
}
