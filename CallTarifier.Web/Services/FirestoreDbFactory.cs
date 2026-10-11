using CallTarifier.Web.Configuration;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Options;

namespace CallTarifier.Web.Services;

public interface IFirestoreDbFactory
{
    Task<FirestoreDb> GetDbAsync(CancellationToken cancellationToken = default);
}

public sealed class FirestoreDbFactory : IFirestoreDbFactory
{
    private readonly FirebaseOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<FirestoreDbFactory> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private FirestoreDb? _db;

    public FirestoreDbFactory(
        IOptions<FirebaseOptions> options,
        IHostEnvironment environment,
        ILogger<FirestoreDbFactory> logger)
    {
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task<FirestoreDb> GetDbAsync(CancellationToken cancellationToken = default)
    {
        if (_db is not null)
        {
            return _db;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_db is not null)
            {
                return _db;
            }

            var path = ResolvePath(_options.ServiceAccountPath);
            if (string.IsNullOrWhiteSpace(_options.ProjectId) || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                throw new InvalidOperationException("Firebase/Firestore no está configurado correctamente.");
            }

            _db = await new FirestoreDbBuilder
            {
                ProjectId = _options.ProjectId,
                Credential = GoogleCredential.FromFile(path)
            }.BuildAsync(cancellationToken);

            _logger.LogInformation("Firestore client ready for project {ProjectId}", _options.ProjectId);
            return _db;
        }
        finally
        {
            _lock.Release();
        }
    }

    private string ResolvePath(string relativeOrAbsolute)
    {
        if (string.IsNullOrWhiteSpace(relativeOrAbsolute))
        {
            return string.Empty;
        }

        return Path.IsPathRooted(relativeOrAbsolute)
            ? relativeOrAbsolute
            : Path.GetFullPath(Path.Combine(_environment.ContentRootPath, relativeOrAbsolute));
    }
}
