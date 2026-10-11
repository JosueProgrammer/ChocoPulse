using CallTarifier.Integration.Configuration;
using CallTarifier.Integration.Models;
using CallTarifier.Integration.Services.Cdr;
using CallTarifier.Integration.Services.Firebase;
using CallTarifier.Integration.Services.Invoices;
using CallTarifier.Integration.Services.Tarification;
using Microsoft.Extensions.Options;

namespace CallTarifier.Integration.Services.Calls;

public sealed class CallProcessor : ICallProcessor
{
    private readonly ICdrQueue _cdrQueue;
    private readonly ITarificationService _tarification;
    private readonly IInvoiceService _invoiceService;
    private readonly IFirebaseService _firebase;
    private readonly TariffOptions _tariffs;
    private readonly ILogger<CallProcessor> _logger;
    private Task? _consumerTask;
    private readonly object _startLock = new();

    public CallProcessor(
        ICdrQueue cdrQueue,
        ITarificationService tarification,
        IInvoiceService invoiceService,
        IFirebaseService firebase,
        IOptions<TariffOptions> tariffs,
        ILogger<CallProcessor> logger)
    {
        _cdrQueue = cdrQueue;
        _tarification = tarification;
        _invoiceService = invoiceService;
        _firebase = firebase;
        _tariffs = tariffs.Value;
        _logger = logger;
    }

    public Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        lock (_startLock)
        {
            _consumerTask ??= Task.Run(() => ConsumeAsync(cancellationToken), CancellationToken.None);
        }

        return Task.CompletedTask;
    }

    private async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        await foreach (var cdr in _cdrQueue.ReadAllAsync(cancellationToken))
        {
            try
            {
                await ProcessCdrAsync(cdr, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process CDR UniqueId={UniqueId}", cdr.UniqueId);
            }
        }
    }

    private async Task ProcessCdrAsync(CdrRecord cdr, CancellationToken cancellationToken)
    {
        if (!CdrProcessor.IsFinalized(cdr))
        {
            _logger.LogWarning("CDR incomplete ignored UniqueId={UniqueId}", cdr.UniqueId);
            return;
        }

        if (!string.IsNullOrWhiteSpace(cdr.UniqueId) &&
            await _firebase.CallExistsByUniqueIdAsync(cdr.UniqueId, cancellationToken))
        {
            _logger.LogInformation("CDR already processed UniqueId={UniqueId}", cdr.UniqueId);
            return;
        }

        var status = CdrProcessor.MapStatus(cdr);
        var answered = status == CallStatuses.Completed;
        var duration = answered
            ? (cdr.BillableSeconds > 0 ? cdr.BillableSeconds : cdr.DurationSeconds)
            : 0;

        var ringCharge = answered || _tariffs.ChargeRingForUnansweredCalls
            ? _tariffs.RingCharge
            : 0m;

        if (!answered)
        {
            duration = 0;
            if (!_tariffs.ChargeRingForUnansweredCalls)
            {
                ringCharge = 0m;
            }
        }

        var tariff = _tarification.Calculate(duration, ringCharge, _tariffs.PricePerSecond, _tariffs.TaxRate);
        var callId = string.IsNullOrWhiteSpace(cdr.UniqueId)
            ? Guid.NewGuid().ToString("N")
            : cdr.UniqueId.Replace('.', '-');

        var call = new CallRecord
        {
            Id = callId,
            Source = cdr.Source ?? string.Empty,
            Destination = cdr.Destination ?? string.Empty,
            StartedAt = cdr.StartTime ?? DateTime.Now,
            RingingAt = cdr.StartTime,
            AnsweredAt = answered ? cdr.AnswerTime : null,
            EndedAt = cdr.EndTime ?? DateTime.Now,
            Status = status,
            DurationSeconds = duration,
            RingCharge = tariff.RingCharge,
            CallCharge = tariff.CallCharge,
            Subtotal = tariff.Subtotal,
            Tax = tariff.Tax,
            Total = tariff.Total,
            UniqueId = cdr.UniqueId,
            LinkedId = cdr.LinkedId,
            CreatedAt = DateTime.UtcNow
        };

        string? invoiceNumber = null;
        if (call.Total > 0m || answered)
        {
            invoiceNumber = await _invoiceService.CreateForCallAsync(call, cancellationToken);
            call.InvoiceId = invoiceNumber;
        }

        await _firebase.SaveCallAsync(call, cancellationToken);
        _logger.LogInformation(
            "Call processed UniqueId={UniqueId} Status={Status} Total={Total} Invoice={Invoice}",
            call.UniqueId,
            call.Status,
            call.Total,
            call.InvoiceId);
    }
}
