namespace CallTarifier.Integration.Models;

public sealed class InvoiceRecord
{
    public string Id { get; set; } = string.Empty;

    public string Number { get; set; } = string.Empty;

    public string CallId { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string Destination { get; set; } = string.Empty;

    public int DurationSeconds { get; set; }

    public decimal RingCharge { get; set; }

    public decimal CallCharge { get; set; }

    public decimal Subtotal { get; set; }

    public decimal Tax { get; set; }

    public decimal Total { get; set; }

    public string Status { get; set; } = "PENDING";

    public DateTime IssuedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
