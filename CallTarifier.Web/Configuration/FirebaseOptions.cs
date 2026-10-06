namespace CallTarifier.Web.Configuration;

public sealed class FirebaseOptions
{
    public const string SectionName = "Firebase";

    public string ProjectId { get; set; } = string.Empty;

    /// <summary>Client-safe Firebase web config only. Never include service account keys.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public string AuthDomain { get; set; } = string.Empty;

    public string StorageBucket { get; set; } = string.Empty;

    public string MessagingSenderId { get; set; } = string.Empty;

    public string AppId { get; set; } = string.Empty;

    public string MeasurementId { get; set; } = string.Empty;

    /// <summary>Server-only path to Admin SDK credentials. Never send to the browser.</summary>
    public string ServiceAccountPath { get; set; } = string.Empty;

    public bool IsWebConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(AuthDomain) &&
        !string.IsNullOrWhiteSpace(ProjectId) &&
        !string.IsNullOrWhiteSpace(AppId);
}
