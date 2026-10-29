using System.Globalization;
using CallTarifier.Web.Models;
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

    public static string BranchFor(string extension)
        => ExtensionDirectory.NameFor(extension);
}

internal static class ExtensionDirectory
{
    private static Dictionary<string, string> _names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["1000"] = "E1",
        ["9000"] = "E1 Casa Pellas",
        ["1010"] = "Central Carazo Bac",
        ["1011"] = "Gerencia de sucursal",
        ["1012"] = "Atención al cliente",
        ["1013"] = "Caja Central",
        ["1014"] = "Creditos",
        ["1015"] = "Seguridad",
        ["1016"] = "Contabilidad",
        ["1017"] = "Soporte Tecnico",
        ["1040"] = "Granada BAC",
        ["1041"] = "Gerencia de sucursal",
        ["1042"] = "Atención al cliente GR",
        ["1043"] = "Caja Central GR",
        ["1044"] = "Creditos GR",
        ["1045"] = "Seguridad GR",
        ["1046"] = "Contabilidad GR",
        ["1047"] = "Soporte Tecnico GR",
        ["2000"] = "Oficina de Estados unidos",
        ["2030"] = "Oficina de Costa Rica",
        ["2060"] = "Oficina de Panama",
        ["2090"] = "Oficina de España",
        ["2120"] = "Oficina de México",
        ["82361811"] = "Claro",
        ["83922921"] = "Tigo"
    };

    public static void Publish(IEnumerable<ExtensionInfo> extensions)
    {
        var next = new Dictionary<string, string>(_names, StringComparer.OrdinalIgnoreCase);
        foreach (var ext in extensions)
        {
            if (string.IsNullOrWhiteSpace(ext.Extension) || string.IsNullOrWhiteSpace(ext.Branch))
            {
                continue;
            }

            var branch = ext.Branch.Trim();
            if (string.Equals(branch, ext.Extension, StringComparison.Ordinal)
                && next.TryGetValue(ext.Extension, out var existing)
                && !string.Equals(existing, ext.Extension, StringComparison.Ordinal))
            {
                continue;
            }

            next[ext.Extension] = branch;
        }

        _names = next;
    }

    public static string NameFor(string extension)
        => _names.TryGetValue(extension ?? string.Empty, out var name) ? name : "Desconocida";
}
