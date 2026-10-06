namespace CallTarifier.Web.Models;

public sealed class MonthlyBilling
{
    public string Id { get; set; } = string.Empty;

    public string Extension { get; set; } = string.Empty;

    public int Year { get; set; }

    public int Month { get; set; }

    public int CallCount { get; set; }

    public decimal Subtotal { get; set; }

    public decimal Tax { get; set; }

    public decimal Total { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
