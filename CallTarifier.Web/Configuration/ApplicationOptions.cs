namespace CallTarifier.Web.Configuration;

public sealed class ApplicationOptions
{
    public const string SectionName = "Application";

    public bool UseMockData { get; set; } = true;

    /// <summary>
    /// When true, login uses Firebase Authentication (web SDK + server token verification).
    /// </summary>
    public bool UseFirebaseAuth { get; set; }
}
