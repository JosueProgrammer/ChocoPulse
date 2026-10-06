using CallTarifier.Web.Configuration;
using CallTarifier.Web.Models;
using CallTarifier.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace CallTarifier.Web.Pages.Settings;

public class IndexModel : PageModel
{
    private readonly IAuthService _authService;
    private readonly IOptions<ApplicationOptions> _application;
    private readonly IOptions<TariffOptions> _tariffs;
    private readonly IOptions<WebRtcOptions> _webRtc;

    public IndexModel(
        IAuthService authService,
        IOptions<ApplicationOptions> application,
        IOptions<TariffOptions> tariffs,
        IOptions<WebRtcOptions> webRtc)
    {
        _authService = authService;
        _application = application;
        _tariffs = tariffs;
        _webRtc = webRtc;
    }

    public AppUser? CurrentUser { get; private set; }

    public ApplicationOptions Application => _application.Value;

    public TariffOptions Tariffs => _tariffs.Value;

    public WebRtcOptions WebRtc => _webRtc.Value;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        CurrentUser = await _authService.GetCurrentUserAsync(cancellationToken);
    }
}
