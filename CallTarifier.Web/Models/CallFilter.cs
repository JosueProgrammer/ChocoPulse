namespace CallTarifier.Web.Models;

public sealed class CallFilter
{
    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    public string? Extension { get; set; }

    public string? Source { get; set; }

    public string? Destination { get; set; }

    public string? Status { get; set; }

    public string? Search { get; set; }
}
