namespace CallTarifier.Integration.Services.Invoices;

public interface IInvoiceService
{
    Task GeneratePendingAsync(CancellationToken cancellationToken);
}
