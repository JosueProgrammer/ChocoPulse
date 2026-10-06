using System.Security.Claims;
using CallTarifier.Web.Models;
using CallTarifier.Web.Models.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace CallTarifier.Web.Services.Mock;

public sealed class MockAuthService : IAuthService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly MockDataStore _store;
    private readonly ILogger<MockAuthService> _logger;

    public MockAuthService(
        IHttpContextAccessor httpContextAccessor,
        MockDataStore store,
        ILogger<MockAuthService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _store = store;
        _logger = logger;
    }

    public bool IsAuthenticated
        => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;

    public bool UsesFirebase => false;

    public Task<AppUser?> SignInWithIdTokenAsync(
        string idToken,
        bool rememberMe,
        CancellationToken cancellationToken = default)
        => Task.FromResult<AppUser?>(null);

    public async Task<AppUser?> SignInAsync(
        string email,
        string password,
        bool rememberMe,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        // Phase 1 mock: accept any non-empty credentials; map known demo email to seeded user.
        var user = _store.CurrentUser;
        if (!email.Equals(user.Email, StringComparison.OrdinalIgnoreCase) &&
            !email.Equals("admin@calltarifier.local", StringComparison.OrdinalIgnoreCase))
        {
            user = new AppUser
            {
                Uid = "mock-user-guest",
                Name = email.Split('@')[0],
                Email = email,
                Extension = "4000",
                Branch = "Carazo",
                Role = UserRole.User
            };
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Uid),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email),
            new("extension", user.Extension),
            new("branch", user.Branch),
            new(ClaimTypes.Role, user.Role)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var properties = new AuthenticationProperties
        {
            IsPersistent = rememberMe,
            ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(14) : DateTimeOffset.UtcNow.AddHours(8)
        };

        var httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("HttpContext no disponible.");

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            properties);

        _logger.LogInformation("Mock sign-in successful for {Email}", user.Email);
        return user;
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            _logger.LogInformation("Mock sign-out completed");
        }
    }

    public Task<AppUser?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return Task.FromResult<AppUser?>(null);
        }

        var user = new AppUser
        {
            Uid = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            Name = principal.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
            Email = principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            Extension = principal.FindFirstValue("extension") ?? string.Empty,
            Branch = principal.FindFirstValue("branch") ?? string.Empty,
            Role = principal.FindFirstValue(ClaimTypes.Role) ?? UserRole.User
        };

        return Task.FromResult<AppUser?>(user);
    }
}
