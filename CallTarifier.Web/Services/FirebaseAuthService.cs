using System.Security.Claims;
using CallTarifier.Web.Configuration;
using CallTarifier.Web.Models;
using CallTarifier.Web.Models.Enums;
using FirebaseAdmin.Auth;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace CallTarifier.Web.Services;

public sealed class FirebaseAuthService : IAuthService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly FirebaseAdminBootstrap _bootstrap;
    private readonly FirebaseOptions _firebaseOptions;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<FirebaseAuthService> _logger;

    public FirebaseAuthService(
        IHttpContextAccessor httpContextAccessor,
        FirebaseAdminBootstrap bootstrap,
        IOptions<FirebaseOptions> firebaseOptions,
        IHostEnvironment environment,
        ILogger<FirebaseAuthService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _bootstrap = bootstrap;
        _firebaseOptions = firebaseOptions.Value;
        _environment = environment;
        _logger = logger;
    }

    public bool IsAuthenticated
        => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;

    public bool UsesFirebase => true;

    public Task<AppUser?> SignInAsync(
        string email,
        string password,
        bool rememberMe,
        CancellationToken cancellationToken = default)
    {
        // Password auth happens in the browser via Firebase JS SDK.
        // The server only accepts verified ID tokens.
        _logger.LogWarning("Password sign-in attempted on server; use ID token flow instead");
        return Task.FromResult<AppUser?>(null);
    }

    public async Task<AppUser?> SignInWithIdTokenAsync(
        string idToken,
        bool rememberMe,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(idToken))
        {
            return null;
        }

        if (!_bootstrap.TryInitialize())
        {
            _logger.LogError("Firebase Admin is not initialized; cannot verify ID token");
            return null;
        }

        FirebaseToken decoded;
        try
        {
            decoded = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(idToken, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid Firebase ID token");
            return null;
        }

        var email = GetClaim(decoded, "email") ?? string.Empty;
        var name = GetClaim(decoded, "name");
        if (string.IsNullOrWhiteSpace(name))
        {
            name = string.IsNullOrWhiteSpace(email) ? "Usuario" : email.Split('@')[0];
        }

        var user = new AppUser
        {
            Uid = decoded.Uid,
            Name = name,
            Email = email,
            Extension = string.Empty,
            Branch = string.Empty,
            Role = UserRole.User
        };

        user = await EnrichFromFirestoreAsync(user, cancellationToken) ?? user;
        if (IsE1Owner(user.Email))
        {
            user.Extension = E1Owner.Extension;
            user.Branch = E1Owner.Branch;
            await SaveChosenExtensionAsync(user, cancellationToken);
        }

        await CreateCookieAsync(user, rememberMe);
        _logger.LogInformation("Firebase sign-in successful for {Email}", user.Email);
        return user;
    }

    public async Task<AppUser?> ChooseExtensionAsync(string extension, string branch, CancellationToken cancellationToken = default)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        if (user is null || string.IsNullOrWhiteSpace(user.Uid))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(extension))
        {
            return null;
        }

        if (IsE1Owner(user.Email))
        {
            extension = E1Owner.Extension;
            branch = E1Owner.Branch;
        }

        user.Extension = extension.Trim();
        user.Branch = branch.Trim();
        await SaveChosenExtensionAsync(user, cancellationToken);

        var httpContext = _httpContextAccessor.HttpContext;
        var rememberMe = false;
        if (httpContext is not null)
        {
            var auth = await httpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            rememberMe = auth.Properties?.IsPersistent == true;
        }

        await CreateCookieAsync(user, rememberMe);
        _logger.LogInformation("Extension {Extension} chosen for {Email}", user.Extension, user.Email);
        return user;
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            _logger.LogInformation("Firebase session cookie cleared");
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

    private async Task<AppUser?> EnrichFromFirestoreAsync(AppUser user, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_firebaseOptions.ProjectId) ||
                string.IsNullOrWhiteSpace(_firebaseOptions.ServiceAccountPath))
            {
                return user;
            }

            var path = Path.IsPathRooted(_firebaseOptions.ServiceAccountPath)
                ? _firebaseOptions.ServiceAccountPath
                : Path.GetFullPath(Path.Combine(_environment.ContentRootPath, _firebaseOptions.ServiceAccountPath));

            if (!File.Exists(path))
            {
                return user;
            }

            var db = await new FirestoreDbBuilder
            {
                ProjectId = _firebaseOptions.ProjectId,
                Credential = Google.Apis.Auth.OAuth2.GoogleCredential.FromFile(path)
            }.BuildAsync();

            var snap = await db.Collection("users").Document(user.Uid).GetSnapshotAsync(cancellationToken);
            if (!snap.Exists)
            {
                _logger.LogInformation("No Firestore profile for uid {Uid}; using defaults", user.Uid);
                return user;
            }

            if (snap.ContainsField("name") && snap.GetValue<string>("name") is { Length: > 0 } name)
            {
                user.Name = name;
            }

            if (snap.ContainsField("email") && snap.GetValue<string>("email") is { Length: > 0 } email)
            {
                user.Email = email;
            }

            var chosen = false;
            if (snap.ContainsField("extensionChosen"))
            {
                chosen = snap.GetValue<object>("extensionChosen") is true;
            }
            if (chosen)
            {
                if (snap.ContainsField("extension") && snap.GetValue<string>("extension") is { Length: > 0 } extension)
                {
                    user.Extension = extension;
                }

                if (snap.ContainsField("branch") && snap.GetValue<string>("branch") is { Length: > 0 } branch)
                {
                    user.Branch = branch;
                }
            }

            if (snap.ContainsField("role") && snap.GetValue<string>("role") is { Length: > 0 } role)
            {
                user.Role = role;
            }

            return user;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load Firestore user profile; using token defaults");
            return user;
        }
    }

    private static bool IsE1Owner(string? email) => E1Owner.IsOwner(email);

    private async Task SaveChosenExtensionAsync(AppUser user, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_firebaseOptions.ProjectId) ||
            string.IsNullOrWhiteSpace(_firebaseOptions.ServiceAccountPath))
        {
            return;
        }

        var path = Path.IsPathRooted(_firebaseOptions.ServiceAccountPath)
            ? _firebaseOptions.ServiceAccountPath
            : Path.GetFullPath(Path.Combine(_environment.ContentRootPath, _firebaseOptions.ServiceAccountPath));

        if (!File.Exists(path))
        {
            return;
        }

        var db = await new FirestoreDbBuilder
        {
            ProjectId = _firebaseOptions.ProjectId,
            Credential = Google.Apis.Auth.OAuth2.GoogleCredential.FromFile(path)
        }.BuildAsync();

        await db.Collection("users").Document(user.Uid).SetAsync(new Dictionary<string, object>
        {
            ["name"] = user.Name,
            ["email"] = user.Email,
            ["extension"] = user.Extension,
            ["branch"] = user.Branch,
            ["extensionChosen"] = true
        }, SetOptions.MergeAll, cancellationToken);
    }

    private async Task CreateCookieAsync(AppUser user, bool rememberMe)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Uid),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email),
            new("extension", user.Extension),
            new("branch", user.Branch),
            new("extensionChosen", string.IsNullOrWhiteSpace(user.Extension) ? "false" : "true"),
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
    }

    private static string? GetClaim(FirebaseToken token, string key)
        => token.Claims.TryGetValue(key, out var value) ? value?.ToString() : null;
}
