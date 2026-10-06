namespace CallTarifier.Web.Configuration;

public sealed class WebRtcOptions
{
    public const string SectionName = "WebRTC";

    public string SipServer { get; set; } = string.Empty;

    public string SipDomain { get; set; } = string.Empty;

    public string WebSocketUrl { get; set; } = string.Empty;

    public bool IceServersEnabled { get; set; } = true;
}
