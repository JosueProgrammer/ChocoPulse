using CallTarifier.Web.Models;

namespace CallTarifier.Web.Services;

public interface IAuthService
{
    Task<AppUser?> SignInAsync(string email, string password, bool rememberMe, CancellationToken cancellationToken = default);

    Task<AppUser?> SignInWithIdTokenAsync(string idToken, bool rememberMe, CancellationToken cancellationToken = default);

    Task SignOutAsync(CancellationToken cancellationToken = default);

    Task<AppUser?> GetCurrentUserAsync(CancellationToken cancellationToken = default);

    bool IsAuthenticated { get; }

    bool UsesFirebase { get; }
}
