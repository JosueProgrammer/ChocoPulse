namespace CallTarifier.Integration.Models;

public sealed class CdrRecord
{
    public string? UniqueId { get; set; }

    public string? LinkedId { get; set; }

    public string? Source { get; set; }

    public string? Destination { get; set; }

    public string? Channel { get; set; }

    public string? DestinationChannel { get; set; }

    public DateTime? StartTime { get; set; }

    public DateTime? AnswerTime { get; set; }

    public DateTime? EndTime { get; set; }

    public int DurationSeconds { get; set; }

    public int BillableSeconds { get; set; }

    public string? Disposition { get; set; }

    public string? AccountCode { get; set; }

    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}
