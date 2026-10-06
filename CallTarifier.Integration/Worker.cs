using CallTarifier.Integration.Configuration;
using CallTarifier.Integration.Services.Ami;
using CallTarifier.Integration.Services.Calls;
using CallTarifier.Integration.Services.Cdr;
using CallTarifier.Integration.Services.Firebase;
using CallTarifier.Integration.Services.Invoices;
using Microsoft.Extensions.Options;

namespace CallTarifier.Integration;

public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ApplicationOptions _application;
    private readonly IAmiListenerService _amiListener;
    private readonly ICdrProcessor _cdrProcessor;
    private readonly ICallProcessor _callProcessor;
    private readonly IInvoiceService _invoiceService;
    private readonly IFirebaseService _firebaseService;

    public Worker(
        ILogger<Worker> logger,
        IOptions<ApplicationOptions> application,
        IAmiListenerService amiListener,
        ICdrProcessor cdrProcessor,
        ICallProcessor callProcessor,
        IInvoiceService invoiceService,
        IFirebaseService firebaseService)
    {
        _logger = logger;
        _application = application.Value;
        _amiListener = amiListener;
        _cdrProcessor = cdrProcessor;
        _callProcessor = callProcessor;
        _invoiceService = invoiceService;
        _firebaseService = firebaseService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "CallTarifier.Integration started. UseMockData={UseMockData}",
            _application.UseMockData);

        await _amiListener.StartAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _cdrProcessor.ProcessAsync(stoppingToken);
                await _callProcessor.ProcessPendingAsync(stoppingToken);
                await _invoiceService.GeneratePendingAsync(stoppingToken);
                await _firebaseService.SynchronizeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Integration loop error");
            }

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }

        await _amiListener.StopAsync(CancellationToken.None);
        _logger.LogInformation("CallTarifier.Integration stopped");
    }
}
