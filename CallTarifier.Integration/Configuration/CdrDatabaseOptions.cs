namespace CallTarifier.Integration.Configuration;

public sealed class CdrDatabaseOptions
{
    public const string SectionName = "CdrDatabase";

    public bool Enabled { get; set; }

    public string ConnectionString { get; set; } = string.Empty;
}
