namespace CallTarifier.Web.Models;

public sealed class StatCardModel
{
    public string Title { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;

    public string? Hint { get; init; }
}
