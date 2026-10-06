using CallTarifier.Integration.Configuration;
using Microsoft.Extensions.Options;

namespace CallTarifier.Integration.Services.Firebase;

public sealed class FirebaseService : IFirebaseService
{
    private readonly ApplicationOptions _application;
    private readonly ILogger<FirebaseService> _logger;

    public FirebaseService(
        IOptions<ApplicationOptions> application,
        ILogger<FirebaseService> logger)
    {
        _application = application.Value;
        _logger = logger;
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(!_application.UseMockData);
    }

    public Task SynchronizeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_application.UseMockData)
        {
            _logger.LogInformation("Firebase synchronization skipped (mock mode)");
            return Task.CompletedTask;
        }

        _logger.LogWarning("Firebase real synchronization not implemented yet (Phase 7)");
        return Task.CompletedTask;
    }
}
