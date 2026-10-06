namespace CallTarifier.Integration.Services.Firebase;

public interface IFirebaseService
{
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);

    Task SynchronizeAsync(CancellationToken cancellationToken);
}
