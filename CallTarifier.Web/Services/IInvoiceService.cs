using CallTarifier.Web.Models;

namespace CallTarifier.Web.Services;

public interface IInvoiceService
{
    Task<IReadOnlyList<Invoice>> GetInvoicesAsync(CancellationToken cancellationToken = default);

    Task<Invoice?> GetByNumberAsync(string number, CancellationToken cancellationToken = default);

    Task<MonthlyBilling?> GetCurrentPeriodAsync(string extension, CancellationToken cancellationToken = default);
}
