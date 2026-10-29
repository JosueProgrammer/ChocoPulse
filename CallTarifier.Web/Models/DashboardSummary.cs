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

    public decimal MonthSubtotal { get; set; }

    public decimal MonthTax { get; set; }

    public int DaysUntilBilling { get; set; }

    public bool ShowsAllExtensions { get; set; }

    public int PendingInvoices { get; set; }

    public IReadOnlyList<Call> RecentCalls { get; set; } = Array.Empty<Call>();

    public IReadOnlyList<ExtensionConsumption> ByExtension { get; set; } = Array.Empty<ExtensionConsumption>();
}

public sealed class ExtensionConsumption
{
    public string Extension { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int Calls { get; set; }

    public decimal Subtotal { get; set; }

    public decimal Tax { get; set; }

    public decimal Total { get; set; }
}
