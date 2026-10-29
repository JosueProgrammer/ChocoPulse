using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CallTarifier.Web.Pages.Account;

public class BacAccessModel : PageModel
{
    public const string CookieName = "CallTarifier.Bac";
    private const string SharedPassword = "123456";
    private static readonly Regex BacEmail = new(@"^[^@\s]+@bac\.com$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    public string? ErrorMessage { get; private set; }

    public IActionResult OnGet()
    {
        if (Request.Cookies.ContainsKey(CookieName))
        {
            return RedirectToPage("/Dashboard/Index");
        }

        return Page();
    }

    public IActionResult OnPost()
    {
        var email = (Email ?? string.Empty).Trim();
        if (!BacEmail.IsMatch(email))
        {
            ErrorMessage = "Debes acceder mediante un dominio proporcionado de BAC.";
            return Page();
        }

        if (!string.Equals(Password, SharedPassword, StringComparison.Ordinal))
        {
            ErrorMessage = "La contraseña no es válida.";
            return Page();
        }

        Response.Cookies.Append(CookieName, "ok", new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(14)
        });

        return RedirectToPage("/Dashboard/Index");
    }
}
