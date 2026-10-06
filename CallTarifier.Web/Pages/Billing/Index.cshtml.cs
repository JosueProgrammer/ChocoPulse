using System.Security.Claims;
using CallTarifier.Web.Models;
using CallTarifier.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CallTarifier.Web.Pages.Billing;

public class IndexModel : PageModel
{
    private readonly IInvoiceService _invoiceService;

    public IndexModel(IInvoiceService invoiceService)
    {
        _invoiceService = invoiceService;
    }

    public IReadOnlyList<Invoice> Invoices { get; private set; } = Array.Empty<Invoice>();

    public MonthlyBilling? CurrentPeriod { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var extension = User.FindFirstValue("extension") ?? "4000";
        Invoices = await _invoiceService.GetInvoicesAsync(cancellationToken);
        CurrentPeriod = await _invoiceService.GetCurrentPeriodAsync(extension, cancellationToken);
    }
}
