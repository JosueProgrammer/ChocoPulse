using CallTarifier.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CallTarifier.Web.Pages.Account;

public class LogoutModel : PageModel
{
    private readonly IAuthService _authService;

    public LogoutModel(IAuthService authService)
    {
        _authService = authService;
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        await _authService.SignOutAsync(cancellationToken);
        return RedirectToPage("/Account/Login");
    }

    public IActionResult OnGet() => RedirectToPage("/Dashboard/Index");
}
