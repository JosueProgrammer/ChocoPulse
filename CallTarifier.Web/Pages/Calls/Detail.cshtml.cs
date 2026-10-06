using CallTarifier.Web.Models;
using CallTarifier.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CallTarifier.Web.Pages.Calls;

public class DetailModel : PageModel
{
    private readonly ICallService _callService;

    public DetailModel(ICallService callService)
    {
        _callService = callService;
    }

    [BindProperty(SupportsGet = true)]
    public string? Id { get; set; }

    public Call? Call { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(Id))
        {
            Call = await _callService.GetByIdAsync(Id, cancellationToken);
        }
    }
}
