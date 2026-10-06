namespace CallTarifier.Integration.Services.Calls;

public interface ICallProcessor
{
    Task ProcessPendingAsync(CancellationToken cancellationToken);
}
