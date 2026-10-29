using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CallTarifier.Web.Configuration;
using CallTarifier.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace CallTarifier.Web.Pages.Account;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly IAuthService _authService;
    private readonly FirebaseOptions _firebaseOptions;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(
        IAuthService authService,
        IOptions<FirebaseOptions> firebaseOptions,
        ILogger<LoginModel> logger)
    {
        _authService = authService;
        _firebaseOptions = firebaseOptions.Value;
        _logger = logger;
    }

    [BindProperty]
    public LoginInput Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public FirebaseOptions Firebase => _firebaseOptions;

    public void OnGet(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var chosen = string.Equals(User.FindFirst("extensionChosen")?.Value, "true", StringComparison.OrdinalIgnoreCase);
            Response.Redirect(chosen ? "/dashboard" : "/account/choose-extension");
            return;
        }

        ReturnUrl = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        ReturnUrl = returnUrl;

        if (string.IsNullOrWhiteSpace(Input.IdToken))
        {
            ModelState.AddModelError(string.Empty, "No se recibió el token de autenticación.");
            return Page();
        }

        var user = await _authService.SignInWithIdTokenAsync(Input.IdToken, Input.RememberMe, cancellationToken);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Sesión de Firebase inválida o expirada.");
            _logger.LogWarning("Firebase ID token verification failed");
            return Page();
        }

        if (string.IsNullOrWhiteSpace(user.Extension))
        {
            return RedirectToPage("/Account/ChooseExtension");
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToPage("/Dashboard/Index");
    }

    public sealed class LoginInput
    {
        [EmailAddress(ErrorMessage = "Ingrese un correo válido.")]
        public string Email { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public string? IdToken { get; set; }

        public bool RememberMe { get; set; }
    }
}
