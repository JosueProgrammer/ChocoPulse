using CallTarifier.Integration.Configuration;
using Microsoft.Extensions.Options;

namespace CallTarifier.Integration.Services.Ami;

public sealed class AmiListenerService : IAmiListenerService
{
    private readonly AsteriskOptions _options;
    private readonly ApplicationOptions _application;
    private readonly ILogger<AmiListenerService> _logger;

    public AmiListenerService(
        IOptions<AsteriskOptions> options,
        IOptions<ApplicationOptions> application,
        ILogger<AmiListenerService> logger)
    {
        _options = options.Value;
        _application = application.Value;
        _logger = logger;
    }

    public bool IsConnected { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_application.UseMockData)
        {
            _logger.LogInformation("AMI listener idle (UseMockData=true). Host={Host}:{Port}", _options.Host, _options.Port);
            IsConnected = false;
            return Task.CompletedTask;
        }

        // Phase 4 will connect using real Asterisk AMI events.
        _logger.LogWarning("AMI real connection not implemented yet. Remaining in idle mode.");
        IsConnected = false;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        IsConnected = false;
        _logger.LogInformation("AMI disconnected");
        return Task.CompletedTask;
    }
}
