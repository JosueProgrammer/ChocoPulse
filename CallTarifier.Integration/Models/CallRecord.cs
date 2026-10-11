namespace CallTarifier.Integration.Models;

public sealed class CallRecord
{
    public string Id { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string Destination { get; set; } = string.Empty;

    public DateTime StartedAt { get; set; }

    public DateTime? RingingAt { get; set; }

    public DateTime? AnsweredAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public string Status { get; set; } = string.Empty;

    public int DurationSeconds { get; set; }

    public decimal RingCharge { get; set; }

    public decimal CallCharge { get; set; }

    public decimal Subtotal { get; set; }

    public decimal Tax { get; set; }

    public decimal Total { get; set; }

    public string? InvoiceId { get; set; }

    public string? UniqueId { get; set; }

    public string? LinkedId { get; set; }

    public DateTime CreatedAt { get; set; }
}
