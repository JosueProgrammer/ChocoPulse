using System.Security.Claims;
using CallTarifier.Web.Configuration;
using CallTarifier.Web.Models;
using Microsoft.Extensions.Options;

namespace CallTarifier.Web.Services;

public interface IWebRtcSessionService
{
    Task<WebRtcSessionConfig?> GetSessionForCurrentUserAsync(CancellationToken cancellationToken = default);
}

public sealed class WebRtcSessionService : IWebRtcSessionService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IFirestoreDbFactory _dbFactory;
    private readonly WebRtcOptions _options;
    private readonly ILogger<WebRtcSessionService> _logger;

    public WebRtcSessionService(
        IHttpContextAccessor httpContextAccessor,
        IFirestoreDbFactory dbFactory,
        IOptions<WebRtcOptions> options,
        ILogger<WebRtcSessionService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _dbFactory = dbFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<WebRtcSessionConfig?> GetSessionForCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogWarning("WebRTC not configured (missing WebSocketUrl/SipDomain)");
            return null;
        }

        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var extension = user.FindFirstValue("extension");
        var uid = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(extension))
        {
            _logger.LogWarning("Authenticated user has no extension claim");
            return null;
        }

        var secret = await ResolveSipSecretAsync(extension, uid, cancellationToken);
        if (string.IsNullOrWhiteSpace(secret))
        {
            _logger.LogWarning(
                "SIP secret missing for extension {Extension}. Set Firestore extensions/{{ext}}.sipSecret or WebRTC:ExtensionSecrets:{{ext}}",
                extension);
            return null;
        }

        var domain = _options.SipDomain.Trim();
        return new WebRtcSessionConfig
        {
            WebSocketUrl = _options.WebSocketUrl.Trim(),
            SipDomain = domain,
            SipServer = string.IsNullOrWhiteSpace(_options.SipServer) ? domain : _options.SipServer.Trim(),
            Extension = extension,
            AuthorizationUsername = extension,
            AuthorizationPassword = secret,
            SipUri = $"sip:{extension}@{domain}",
            IceServersEnabled = _options.IceServersEnabled
        };
    }

    private async Task<string?> ResolveSipSecretAsync(string extension, string? uid, CancellationToken cancellationToken)
    {
        if (_options.ExtensionSecrets.TryGetValue(extension, out var fromConfig) &&
            !string.IsNullOrWhiteSpace(fromConfig))
        {
            return fromConfig;
        }

        try
        {
            var db = await _dbFactory.GetDbAsync(cancellationToken);

            var extSnap = await db.Collection("extensions").Document(extension).GetSnapshotAsync(cancellationToken);
            if (extSnap.Exists && extSnap.ContainsField("sipSecret"))
            {
                var secret = extSnap.GetValue<string>("sipSecret");
                if (!string.IsNullOrWhiteSpace(secret))
                {
                    return secret;
                }
            }

            if (!string.IsNullOrWhiteSpace(uid))
            {
                var userSnap = await db.Collection("users").Document(uid).GetSnapshotAsync(cancellationToken);
                if (userSnap.Exists && userSnap.ContainsField("sipSecret"))
                {
                    var secret = userSnap.GetValue<string>("sipSecret");
                    if (!string.IsNullOrWhiteSpace(secret))
                    {
                        return secret;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load SIP secret from Firestore for {Extension}", extension);
        }

        return null;
    }
}
