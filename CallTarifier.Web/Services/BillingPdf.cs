using System.Globalization;
using System.Text;
using CallTarifier.Web.Models;

namespace CallTarifier.Web.Services;

public static class BillingPdf
{
    private static readonly Encoding WinAnsi = Encoding.Latin1;
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("es-NI");

    public static byte[] Build(string extension, string extensionName, int year, int month, IReadOnlyList<Invoice> invoices)
    {
        var now = DateTime.Now;
        var periodStart = new DateTime(year, month, 1);
        var monthName = Capitalize(periodStart.ToString("MMMM", Culture));
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var daysLeft = now.Year == year && now.Month == month
            ? Math.Max(0, daysInMonth - now.Day)
            : 0;
        var calls = invoices.OrderBy(item => item.IssuedAt).ToList();
        var byType = Enum.GetValues<CallKind>()
            .Select(kind =>
            {
                var rows = calls.Where(item => CallPricing.KindFor(item.Source, item.Destination) == kind).ToList();
                return (Label: CallPricing.Label(kind), Rows: rows);
            })
            .ToList();

        var subtotal = calls.Sum(item => item.Subtotal);
        var tax = calls.Sum(item => item.Tax);
        var total = calls.Sum(item => item.Total);
        var central = string.IsNullOrWhiteSpace(extension) || extension == "1000";
        var billedTo = central
            ? "Extensión central 1000 · E1"
            : $"Extensión {extension} · {extensionName}";

        var pages = new List<Canvas> { new() };
        var page = pages[0];
        DrawHeader(page, billedTo, now);
        var y = 724d;

        page.Text("F2", 13, 0.11, 0.14, 0.19, 40, y, $"Facturación de {monthName} {year}");
        y -= 18;
        page.Text("F1", 10, 0.36, 0.42, 0.49, 40, y, now.ToString("dddd d 'de' MMMM 'de' yyyy, HH:mm", Culture));
        y -= 16;
        page.Text("F1", 10, 0.36, 0.42, 0.49, 40, y, billedTo);
        y -= 22;

        page.Fill(0.97, 0.98, 0.99, 36, y - 52, 523, 46);
        page.Fill(0.82, 0.07, 0.10, 36, y - 52, 4, 46);
        var closing = daysLeft == 0
            ? "El mes ya llegó a su cierre. Este total es el que corresponde pagar."
            : daysLeft == 1
                ? "Falta 1 día para terminar el mes. Por el momento no se cobra; el pago es al cierre."
                : $"Faltan {daysLeft} días para terminar el mes. Por el momento no se cobra; el pago es al cierre.";
        page.Text("F2", 11, 0.11, 0.14, 0.19, 50, y - 18, $"Mes de {monthName} {year}");
        page.Text("F1", 10, 0.36, 0.42, 0.49, 50, y - 36, closing);
        y -= 68;

        page.Text("F2", 12, 0.11, 0.14, 0.19, 40, y, "Detalle por tipo de llamada");
        y -= 8;
        page.Fill(0.11, 0.14, 0.19, 36, y - 18, 523, 18);
        page.Text("F2", 9, 1, 1, 1, 44, y - 13, "Tipo");
        page.Text("F2", 9, 1, 1, 1, 180, y - 13, "Llamadas");
        page.Text("F2", 9, 1, 1, 1, 280, y - 13, "Subtotal");
        page.Text("F2", 9, 1, 1, 1, 390, y - 13, "IVA");
        page.Text("F2", 9, 1, 1, 1, 480, y - 13, "Total");
        y -= 18;

        for (var index = 0; index < byType.Count; index++)
        {
            var row = byType[index];
            if (index % 2 == 0)
            {
                page.Fill(0.96, 0.97, 0.98, 36, y - 16, 523, 16);
            }

            page.Text("F1", 9, 0.11, 0.14, 0.19, 44, y - 12, row.Label);
            page.Text("F1", 9, 0.11, 0.14, 0.19, 180, y - 12, row.Rows.Count.ToString(Culture));
            page.Text("F1", 9, 0.11, 0.14, 0.19, 280, y - 12, Money(row.Rows.Sum(item => item.Subtotal)));
            page.Text("F1", 9, 0.11, 0.14, 0.19, 390, y - 12, Money(row.Rows.Sum(item => item.Tax)));
            page.Text("F2", 9, 0.11, 0.14, 0.19, 480, y - 12, Money(row.Rows.Sum(item => item.Total)));
            y -= 16;
        }

        y -= 16;
        page.Fill(0.99, 0.93, 0.93, 36, y - 62, 523, 62);
        page.Fill(0.82, 0.07, 0.10, 36, y, 523, 4);
        page.Text("F1", 10, 0.42, 0.16, 0.18, 50, y - 18, "Total a cobrar al cierre del mes");
        page.Text("F2", 22, 0.82, 0.07, 0.10, 50, y - 46, Money(total));
        page.Text("F1", 9, 0.36, 0.42, 0.49, 280, y - 24, $"Llamadas realizadas: {calls.Count}");
        page.Text("F1", 9, 0.36, 0.42, 0.49, 280, y - 40, $"Subtotal {Money(subtotal)}    IVA 15% {Money(tax)}");
        y -= 82;

        y = DrawCallHeading(page, y);
        var callIndex = 0;
        foreach (var call in calls)
        {
            if (y < 64)
            {
                pages.Add(new Canvas());
                page = pages[^1];
                DrawSlimHeader(page);
                y = DrawCallHeading(page, 724);
            }

            if (callIndex % 2 == 0)
            {
                page.Fill(0.96, 0.97, 0.98, 36, y - 16, 523, 16);
            }

            page.Text("F1", 8, 0.11, 0.14, 0.19, 40, y - 12, Fit(call.Number, 12));
            page.Text("F1", 8, 0.11, 0.14, 0.19, 112, y - 12, call.Source);
            page.Text("F1", 8, 0.11, 0.14, 0.19, 168, y - 12, Fit(call.Destination, 12));
            page.Text("F1", 8, 0.11, 0.14, 0.19, 250, y - 12, CallPricing.Label(CallPricing.KindFor(call.Source, call.Destination)));
            page.Text("F1", 8, 0.11, 0.14, 0.19, 340, y - 12, DisplayHelpers.FormatDuration(call.DurationSeconds));
            page.Text("F2", 8, 0.11, 0.14, 0.19, 470, y - 12, Money(call.Total));
            y -= 16;
            callIndex++;
        }

        if (calls.Count == 0)
        {
            page.Text("F1", 10, 0.36, 0.42, 0.49, 40, y - 16, "No hay llamadas registradas en este mes.");
        }

        for (var index = 0; index < pages.Count; index++)
        {
            pages[index].Text("F1", 8, 0.36, 0.42, 0.49, 36, 28, "ChocoPulse · E1 pertenece a BAC");
            pages[index].Text("F1", 8, 0.36, 0.42, 0.49, 430, 28, $"Página {index + 1} de {pages.Count}");
        }

        return Compose(pages.Select(item => item.ToStream()).ToList());
    }

    private static void DrawHeader(Canvas page, string billedTo, DateTime now)
    {
        page.Fill(0.11, 0.14, 0.19, 0, 754, 595, 88);
        page.Fill(0.82, 0.07, 0.10, 0, 748, 595, 6);
        page.Text("F2", 22, 1, 1, 1, 36, 802, "ChocoPulse");
        page.Text("F1", 12, 1, 1, 1, 36, 782, "E1 pertenece a BAC");
        page.Text("F1", 9, 0.84, 0.87, 0.92, 36, 764, "Sistema de tarificación · empresa BAC");
        page.Text("F1", 9, 1, 1, 1, 360, 804, billedTo);
        page.Text("F1", 9, 0.84, 0.87, 0.92, 360, 788, now.ToString("dddd", Culture));
        page.Text("F1", 9, 0.84, 0.87, 0.92, 360, 774, now.ToString("d 'de' MMMM 'de' yyyy", Culture));
        page.Text("F1", 9, 0.84, 0.87, 0.92, 360, 760, now.ToString("HH:mm", Culture));
    }

    private static void DrawSlimHeader(Canvas page)
    {
        page.Fill(0.11, 0.14, 0.19, 0, 790, 595, 52);
        page.Fill(0.82, 0.07, 0.10, 0, 784, 595, 6);
        page.Text("F2", 14, 1, 1, 1, 36, 812, "ChocoPulse");
        page.Text("F1", 10, 1, 1, 1, 150, 812, "E1 pertenece a BAC");
        page.Text("F1", 9, 0.84, 0.87, 0.92, 36, 796, "Llamadas realizadas");
    }

    private static double DrawCallHeading(Canvas page, double y)
    {
        page.Text("F2", 12, 0.11, 0.14, 0.19, 40, y, "Llamadas realizadas");
        y -= 8;
        page.Fill(0.11, 0.14, 0.19, 36, y - 18, 523, 18);
        page.Text("F2", 8, 1, 1, 1, 40, y - 13, "Número");
        page.Text("F2", 8, 1, 1, 1, 112, y - 13, "Origen");
        page.Text("F2", 8, 1, 1, 1, 168, y - 13, "Destino");
        page.Text("F2", 8, 1, 1, 1, 250, y - 13, "Tipo");
        page.Text("F2", 8, 1, 1, 1, 340, y - 13, "Duración");
        page.Text("F2", 8, 1, 1, 1, 470, y - 13, "Total");
        return y - 18;
    }

    private static string Money(decimal amount) => "C$" + amount.ToString("N2", Culture);

    private static string Capitalize(string value)
        => string.IsNullOrEmpty(value) ? value : char.ToUpper(value[0], Culture) + value[1..];

    private static string Fit(string value, int width)
    {
        value ??= string.Empty;
        return value.Length <= width ? value : value[..width];
    }

    private static byte[] Compose(IReadOnlyList<string> pageStreams)
    {
        var pageCount = pageStreams.Count;
        var font1 = 3 + pageCount * 2;
        var font2 = font1 + 1;
        var objects = new List<byte[]>();

        void Add(string value) => objects.Add(WinAnsi.GetBytes(value));

        Add("<< /Type /Catalog /Pages 2 0 R >>");
        var kids = string.Join(" ", Enumerable.Range(0, pageCount).Select(index => $"{3 + index * 2} 0 R"));
        Add($"<< /Type /Pages /Count {pageCount} /Kids [{kids}] >>");

        for (var index = 0; index < pageCount; index++)
        {
            var contentId = 4 + index * 2;
            Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents {contentId} 0 R /Resources << /Font << /F1 {font1} 0 R /F2 {font2} 0 R >> >> >>");
            var body = WinAnsi.GetBytes(pageStreams[index]);
            var header = WinAnsi.GetBytes($"<< /Length {body.Length} >>\nstream\n");
            var footer = WinAnsi.GetBytes("\nendstream");
            objects.Add(header.Concat(body).Concat(footer).ToArray());
        }

        Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");

        using var output = new MemoryStream();
        void Write(string value)
        {
            var bytes = WinAnsi.GetBytes(value);
            output.Write(bytes, 0, bytes.Length);
        }

        Write("%PDF-1.4\n");
        var offsets = new int[objects.Count + 1];
        for (var index = 0; index < objects.Count; index++)
        {
            offsets[index + 1] = (int)output.Length;
            Write($"{index + 1} 0 obj\n");
            output.Write(objects[index], 0, objects[index].Length);
            Write("\nendobj\n");
        }

        var xref = (int)output.Length;
        Write($"xref\n0 {objects.Count + 1}\n");
        Write("0000000000 65535 f \n");
        for (var index = 1; index <= objects.Count; index++)
        {
            Write(offsets[index].ToString("0000000000", CultureInfo.InvariantCulture) + " 00000 n \n");
        }

        Write($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        return output.ToArray();
    }

    private sealed class Canvas
    {
        private readonly StringBuilder _stream = new();

        public void Fill(double r, double g, double b, double x, double y, double w, double h)
        {
            _stream.AppendLine("q");
            _stream.AppendLine($"{N(r)} {N(g)} {N(b)} rg");
            _stream.AppendLine($"{N(x)} {N(y)} {N(w)} {N(h)} re f");
            _stream.AppendLine("Q");
        }

        public void Text(string font, double size, double r, double g, double b, double x, double y, string text)
        {
            _stream.AppendLine("BT");
            _stream.AppendLine($"{N(r)} {N(g)} {N(b)} rg");
            _stream.AppendLine($"/{font} {N(size)} Tf");
            _stream.AppendLine($"1 0 0 1 {N(x)} {N(y)} Tm");
            _stream.Append('(').Append(Escape(text)).AppendLine(") Tj");
            _stream.AppendLine("ET");
        }

        public string ToStream() => _stream.ToString();

        private static string N(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        private static string Escape(string value)
        {
            var encoded = WinAnsi.GetString(WinAnsi.GetBytes(value ?? string.Empty));
            return encoded
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("(", "\\(", StringComparison.Ordinal)
                .Replace(")", "\\)", StringComparison.Ordinal);
        }
    }
}
