using CallTarifier.Web.Models;
using CallTarifier.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CallTarifier.Web.Pages.Billing;

public class InvoiceModel : PageModel
{
    private readonly IInvoiceService _invoiceService;

    public InvoiceModel(IInvoiceService invoiceService)
    {
        _invoiceService = invoiceService;
    }

    [BindProperty(SupportsGet = true)]
    public string? Number { get; set; }

    public Invoice? Invoice { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(Number))
        {
            Invoice = await _invoiceService.GetByNumberAsync(Number, cancellationToken);
        }
    }
}
