using System.Security.Claims;
using CallTarifier.Web.Models;
using CallTarifier.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CallTarifier.Web.Pages.Dashboard;

public class IndexModel : PageModel
{
    private readonly ICallService _callService;

    public IndexModel(ICallService callService)
    {
        _callService = callService;
    }

    public DashboardSummary Summary { get; private set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var extension = User.FindFirstValue("extension") ?? string.Empty;
        Summary = await _callService.GetDashboardAsync(extension, cancellationToken);
        Summary.UserName = User.FindFirstValue(ClaimTypes.Name) ?? "Usuario";
        Summary.Branch = User.FindFirstValue("branch") ?? Summary.Branch;
        Summary.Extension = extension;
    }
}
