using System.Globalization;
using CallTarifier.Web.Models.Enums;

namespace CallTarifier.Web;

public static class DisplayHelpers
{
    private static readonly CultureInfo NiCulture = CultureInfo.GetCultureInfo("es-NI");

    public static string FormatMoney(decimal amount)
        => amount.ToString("C", NiCulture);

    public static string FormatDuration(int totalSeconds)
    {
        if (totalSeconds < 0)
        {
            totalSeconds = 0;
        }

        var ts = TimeSpan.FromSeconds(totalSeconds);
        return ts.TotalHours >= 1
            ? ts.ToString(@"hh\:mm\:ss")
            : ts.ToString(@"mm\:ss");
    }

    public static string StatusLabel(string status) => status switch
    {
        CallStatus.Completed => "Completada",
        CallStatus.Missed => "Perdida",
        CallStatus.Rejected => "Rechazada",
        CallStatus.Failed => "Fallida",
        CallStatus.Busy => "Ocupada",
        CallStatus.NoAnswer => "Sin respuesta",
        InvoiceStatus.Pending => "Pendiente",
        InvoiceStatus.Paid => "Pagada",
        InvoiceStatus.Cancelled => "Cancelada",
        _ => status
    };

    public static string StatusCss(string status) => status switch
    {
        CallStatus.Completed or InvoiceStatus.Paid => "badge-success",
        CallStatus.Missed or CallStatus.NoAnswer or InvoiceStatus.Pending => "badge-warning",
        CallStatus.Rejected or CallStatus.Failed or CallStatus.Busy or InvoiceStatus.Cancelled => "badge-danger",
        _ => "badge-neutral"
    };

    public static string BranchFor(string extension) => extension switch
    {
        "4000" => "Carazo",
        "4021" => "Masaya",
        "4041" => "Rivas",
        "4061" => "Granada",
        "4101" => "León",
        "4121" => "Boaco",
        "4141" => "Managua",
        _ => "Desconocida"
    };
}
