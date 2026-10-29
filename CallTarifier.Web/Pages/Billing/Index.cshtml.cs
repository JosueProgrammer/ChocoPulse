using System.Security.Claims;
using CallTarifier.Web.Models;
using CallTarifier.Web.Services;
using Microsoft.AspNetCore.Mvc;
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

    public string Extension { get; private set; } = string.Empty;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var (extension, invoices, period) = await LoadAsync(cancellationToken);
        Extension = extension;
        Invoices = invoices;
        CurrentPeriod = period;
    }

    public async Task<IActionResult> OnGetPdfAsync(CancellationToken cancellationToken)
    {
        var (extension, invoices, period) = await LoadAsync(cancellationToken);
        var bytes = BillingPdf.Build(
            extension,
            DisplayHelpers.BranchFor(extension),
            period.Year,
            period.Month,
            invoices);
        var fileName = $"facturacion-{extension}-{period.Year}-{period.Month:00}.pdf";
        return File(bytes, "application/pdf", fileName);
    }

    private async Task<(string Extension, IReadOnlyList<Invoice> Invoices, MonthlyBilling Period)> LoadAsync(CancellationToken cancellationToken)
    {
        var extension = User.FindFirstValue("extension") ?? string.Empty;
        var now = DateTime.Now;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var all = await _invoiceService.GetInvoicesAsync(cancellationToken);
        var scoped = all.Where(invoice => invoice.IssuedAt >= monthStart || invoice.CreatedAt >= monthStart);
        if (!string.Equals(extension, "1000", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(extension))
        {
            scoped = scoped.Where(invoice => string.Equals(invoice.Source, extension, StringComparison.OrdinalIgnoreCase));
        }

        var invoices = scoped.OrderByDescending(invoice => invoice.IssuedAt).ToList();
        var period = new MonthlyBilling
        {
            Extension = extension,
            Year = now.Year,
            Month = now.Month,
            CallCount = invoices.Count,
            Subtotal = invoices.Sum(invoice => invoice.Subtotal),
            Tax = invoices.Sum(invoice => invoice.Tax),
            Total = invoices.Sum(invoice => invoice.Total),
            Status = invoices.Any(invoice => invoice.Status == Models.Enums.InvoiceStatus.Pending)
                ? Models.Enums.InvoiceStatus.Pending
                : Models.Enums.InvoiceStatus.Paid
        };

        return (extension, invoices, period);
    }
}
