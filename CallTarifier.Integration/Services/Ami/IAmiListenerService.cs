namespace CallTarifier.Integration.Services.Ami;

/// <summary>
/// Phase 1 stub. Real AMI connection is implemented in Phase 4.
/// </summary>
public interface IAmiListenerService
{
    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);

    bool IsConnected { get; }
}
