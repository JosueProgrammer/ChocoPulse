namespace CallTarifier.Web.Models;

public sealed class WebRtcSessionConfig
{
    public string WebSocketUrl { get; init; } = string.Empty;

    public string SipDomain { get; init; } = string.Empty;

    public string SipServer { get; init; } = string.Empty;

    public string Extension { get; init; } = string.Empty;

    public string AuthorizationUsername { get; init; } = string.Empty;

    public string AuthorizationPassword { get; init; } = string.Empty;

    public string SipUri { get; init; } = string.Empty;

    public bool IceServersEnabled { get; init; } = true;
}
