using CallTarifier.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CallTarifier.Web.Pages.Api;

[Authorize]
[IgnoreAntiforgeryToken]
public class WebRtcSessionModel : PageModel
{
    private readonly IWebRtcSessionService _webRtcSessionService;
    private readonly ILogger<WebRtcSessionModel> _logger;

    public WebRtcSessionModel(IWebRtcSessionService webRtcSessionService, ILogger<WebRtcSessionModel> logger)
    {
        _webRtcSessionService = webRtcSessionService;
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync(string? extension, CancellationToken cancellationToken)
    {
        var session = await _webRtcSessionService.GetSessionForCurrentUserAsync(extension, cancellationToken);
        if (session is null)
        {
            _logger.LogWarning("WebRTC session unavailable for extension {Extension}", extension);
            return new JsonResult(new
            {
                error = string.IsNullOrWhiteSpace(extension)
                    ? "Elige tu extensión antes de llamar."
                    : $"La extensión {extension} no pudo identificarse en la central."
            })
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable
            };
        }

        _logger.LogInformation("WebRTC registration started for extension {Extension}", session.Extension);

        // Password is required by the browser SIP stack; never log it.
        return new JsonResult(new
        {
            webSocketUrl = session.WebSocketUrl,
            sipDomain = session.SipDomain,
            sipServer = session.SipServer,
            extension = session.Extension,
            displayName = session.DisplayName,
            authorizationUsername = session.AuthorizationUsername,
            authorizationPassword = session.AuthorizationPassword,
            sipUri = session.SipUri,
            iceServersEnabled = session.IceServersEnabled
        });
    }
}
