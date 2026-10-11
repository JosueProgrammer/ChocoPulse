using CallTarifier.Integration.Services.Ami;
using CallTarifier.Integration.Services.Calls;
using CallTarifier.Integration.Services.Cdr;
using CallTarifier.Integration.Services.Firebase;
using CallTarifier.Integration.Services.Invoices;

namespace CallTarifier.Integration;

public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IAmiListenerService _amiListener;
    private readonly ICdrProcessor _cdrProcessor;
    private readonly ICallProcessor _callProcessor;
    private readonly IInvoiceService _invoiceService;
    private readonly IFirebaseService _firebaseService;
    private readonly ICdrBackfillService _cdrBackfill;

    public Worker(
        ILogger<Worker> logger,
        IAmiListenerService amiListener,
        ICdrProcessor cdrProcessor,
        ICallProcessor callProcessor,
        IInvoiceService invoiceService,
        IFirebaseService firebaseService,
        ICdrBackfillService cdrBackfill)
    {
        _logger = logger;
        _amiListener = amiListener;
        _cdrProcessor = cdrProcessor;
        _callProcessor = callProcessor;
        _invoiceService = invoiceService;
        _firebaseService = firebaseService;
        _cdrBackfill = cdrBackfill;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CallTarifier.Integration started (live AMI/CDR/Firebase)");

        try
        {
            await _firebaseService.EnsureSeedAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Firestore seed failed; continuing AMI loop");
        }

        await _amiListener.StartAsync(stoppingToken);
        await _callProcessor.ProcessPendingAsync(stoppingToken);

        try
        {
            await _cdrBackfill.RunOnceAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CDR backfill skipped");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _cdrProcessor.ProcessAsync(stoppingToken);
                await _invoiceService.GeneratePendingAsync(stoppingToken);
                await _firebaseService.SynchronizeAsync(stoppingToken);

                if (!_amiListener.IsConnected)
                {
                    _logger.LogWarning("AMI not connected");
                }
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
