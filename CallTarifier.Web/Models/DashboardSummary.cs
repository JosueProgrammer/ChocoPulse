namespace CallTarifier.Web.Models;

public sealed class DashboardSummary
{
    public string UserName { get; set; } = string.Empty;

    public string Extension { get; set; } = string.Empty;

    public string Branch { get; set; } = string.Empty;

    public int OutgoingCalls { get; set; }

    public int IncomingCalls { get; set; }

    public int MonthCalls { get; set; }

    public decimal AccumulatedCost { get; set; }

    public int PendingInvoices { get; set; }

    public IReadOnlyList<Call> RecentCalls { get; set; } = Array.Empty<Call>();
}
