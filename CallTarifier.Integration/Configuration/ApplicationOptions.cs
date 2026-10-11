namespace CallTarifier.Integration.Configuration;

public sealed class ApplicationOptions
{
    public const string SectionName = "Application";

    public bool EnableCdrBackfill { get; set; }
}
