using System.Security.Claims;
using CallTarifier.Web.Models;
using CallTarifier.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CallTarifier.Web.Pages.Account;

public class ChooseExtensionModel : PageModel
{
    private readonly IAuthService _authService;
    private readonly IExtensionService _extensionService;

    public ChooseExtensionModel(IAuthService authService, IExtensionService extensionService)
    {
        _authService = authService;
        _extensionService = extensionService;
    }

    public IReadOnlyList<Models.ExtensionInfo> Extensions { get; private set; } = Array.Empty<Models.ExtensionInfo>();

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (string.Equals(User.FindFirst("extensionChosen")?.Value, "true", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToPage("/Dashboard/Index");
        }

        Extensions = await _extensionService.GetAllAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? extension, CancellationToken cancellationToken)
    {
        Extensions = await _extensionService.GetAllAsync(cancellationToken);
        var selected = Extensions.FirstOrDefault(item =>
            string.Equals(item.Extension, extension, StringComparison.OrdinalIgnoreCase));
        if (selected is null)
        {
            ErrorMessage = "Seleccione una extensión de la lista.";
            return Page();
        }

        AppUser? user;
        try
        {
            user = await _authService.ChooseExtensionAsync(selected.Extension, selected.Branch, cancellationToken);
        }
        catch (Exception)
        {
            ErrorMessage = "No se pudo guardar la extensión. Intente de nuevo.";
            return Page();
        }

        if (user is null)
        {
            ErrorMessage = "No se pudo guardar la extensión. Intente de nuevo.";
            return Page();
        }

        return RedirectToPage("/Dashboard/Index");
    }
}
