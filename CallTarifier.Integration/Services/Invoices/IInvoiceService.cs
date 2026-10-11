using CallTarifier.Integration.Models;

namespace CallTarifier.Integration.Services.Invoices;

public interface IInvoiceService
{
    Task GeneratePendingAsync(CancellationToken cancellationToken);

    Task<string> CreateForCallAsync(CallRecord call, CancellationToken cancellationToken);
}
