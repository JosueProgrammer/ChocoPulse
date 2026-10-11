namespace CallTarifier.Web.Configuration;

public sealed class ApplicationOptions
{
    public const string SectionName = "Application";

    /// <summary>Kept for Settings display; auth is always Firebase.</summary>
    public bool UseFirebaseAuth { get; set; } = true;
}
