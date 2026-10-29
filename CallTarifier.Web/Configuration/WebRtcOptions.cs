namespace CallTarifier.Web.Configuration;

public sealed class WebRtcOptions
{
    public const string SectionName = "WebRTC";

    public string SipServer { get; set; } = string.Empty;

    public string SipDomain { get; set; } = string.Empty;

    public string WebSocketUrl { get; set; } = string.Empty;

    public bool IceServersEnabled { get; set; } = true;

    /// <summary>
    /// SIP identity used by the browser to place calls. Not assigned to the user profile.
    /// </summary>
    public string CallerExtension { get; set; } = string.Empty;

    /// <summary>
    /// Optional local secrets keyed by extension (prefer User Secrets).
    /// Never commit real values. Firestore field sipSecret is preferred in production.
    /// </summary>
    public Dictionary<string, string> ExtensionSecrets { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(WebSocketUrl) &&
        !string.IsNullOrWhiteSpace(SipDomain);
}
