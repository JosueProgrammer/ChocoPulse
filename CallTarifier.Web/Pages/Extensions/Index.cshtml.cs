using CallTarifier.Web.Models;
using CallTarifier.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CallTarifier.Web.Pages.Extensions;

public class IndexModel : PageModel
{
    private readonly IExtensionService _extensionService;

    public IndexModel(IExtensionService extensionService)
    {
        _extensionService = extensionService;
    }

    public IReadOnlyList<ExtensionInfo> Extensions { get; private set; } = Array.Empty<ExtensionInfo>();

    [TempData]
    public string? Notice { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var refreshed = await _extensionService.RefreshFromPbxAsync(cancellationToken);
        if (refreshed is null)
        {
            Notice = "No se pudo leer la central. Se muestra la última lista guardada.";
            Extensions = await _extensionService.GetAllAsync(cancellationToken);
            return;
        }

        Extensions = refreshed;
    }

    public async Task<IActionResult> OnPostRefreshAsync(CancellationToken cancellationToken)
    {
        var refreshed = await _extensionService.RefreshFromPbxAsync(cancellationToken);
        Notice = refreshed is null
            ? "No se pudo leer la central. Se muestra la última lista guardada."
            : $"Lista actualizada. {refreshed.Count} extensiones de Issabel.";
        return RedirectToPage();
    }
}
