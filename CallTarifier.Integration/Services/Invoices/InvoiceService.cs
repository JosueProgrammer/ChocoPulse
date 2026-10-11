using CallTarifier.Integration.Models;
using CallTarifier.Integration.Services.Firebase;

namespace CallTarifier.Integration.Services.Invoices;

public sealed class InvoiceService : IInvoiceService
{
    private readonly IFirebaseService _firebase;
    private readonly ILogger<InvoiceService> _logger;

    public InvoiceService(IFirebaseService firebase, ILogger<InvoiceService> logger)
    {
        _firebase = firebase;
        _logger = logger;
    }

    public Task GeneratePendingAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public async Task<string> CreateForCallAsync(CallRecord call, CancellationToken cancellationToken)
    {
        var number = await _firebase.NextInvoiceNumberAsync(cancellationToken);
        var invoice = new InvoiceRecord
        {
            Id = number,
            Number = number,
            CallId = call.Id,
            Source = call.Source,
            Destination = call.Destination,
            DurationSeconds = call.DurationSeconds,
            RingCharge = call.RingCharge,
            CallCharge = call.CallCharge,
            Subtotal = call.Subtotal,
            Tax = call.Tax,
            Total = call.Total,
            Status = "PENDING",
            IssuedAt = call.EndedAt ?? DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        await _firebase.SaveInvoiceAsync(invoice, cancellationToken);
        _logger.LogInformation("Invoice generated {Number} for call {CallId}", number, call.Id);
        return number;
    }
}
