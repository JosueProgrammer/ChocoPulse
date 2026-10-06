namespace CallTarifier.Integration.Configuration;

public sealed class FirebaseOptions
{
    public const string SectionName = "Firebase";

    public string ProjectId { get; set; } = string.Empty;

    /// <summary>Path to service account JSON. Never log or expose contents.</summary>
    public string ServiceAccountPath { get; set; } = string.Empty;
}
