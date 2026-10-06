using CallTarifier.Web.Configuration;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Options;

namespace CallTarifier.Web.Services;

public sealed class FirebaseAdminBootstrap
{
    private readonly FirebaseOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<FirebaseAdminBootstrap> _logger;
    private readonly object _sync = new();
    private bool _initialized;

    public FirebaseAdminBootstrap(
        IOptions<FirebaseOptions> options,
        IHostEnvironment environment,
        ILogger<FirebaseAdminBootstrap> logger)
    {
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public bool TryInitialize()
    {
        if (_initialized || FirebaseApp.DefaultInstance is not null)
        {
            _initialized = true;
            return true;
        }

        lock (_sync)
        {
            if (_initialized || FirebaseApp.DefaultInstance is not null)
            {
                _initialized = true;
                return true;
            }

            var path = ResolveServiceAccountPath();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                _logger.LogError("Firebase service account not found at {Path}", path);
                return false;
            }

            FirebaseApp.Create(new AppOptions
            {
                Credential = GoogleCredential.FromFile(path),
                ProjectId = _options.ProjectId
            });

            _initialized = true;
            _logger.LogInformation("Firebase Admin initialized for project {ProjectId}", _options.ProjectId);
            return true;
        }
    }

    private string ResolveServiceAccountPath()
    {
        if (string.IsNullOrWhiteSpace(_options.ServiceAccountPath))
        {
            return string.Empty;
        }

        if (Path.IsPathRooted(_options.ServiceAccountPath))
        {
            return _options.ServiceAccountPath;
        }

        return Path.GetFullPath(Path.Combine(_environment.ContentRootPath, _options.ServiceAccountPath));
    }
}
