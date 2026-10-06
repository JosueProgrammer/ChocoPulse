namespace CallTarifier.Integration.Configuration;

public sealed class ApplicationOptions
{
    public const string SectionName = "Application";

    public bool UseMockData { get; set; } = true;
}
