using CallTarifier.Web.Models;
using Google.Cloud.Firestore;

namespace CallTarifier.Web.Services;

internal static class FirestoreMappers
{
    public static Call MapCall(DocumentSnapshot snap)
    {
        var data = snap.ToDictionary();
        return new Call
        {
            Id = snap.Id,
            Source = GetString(data, "source"),
            Destination = GetString(data, "destination"),
            StartedAt = GetDateTime(data, "startedAt") ?? DateTime.MinValue,
            RingingAt = GetDateTime(data, "ringingAt"),
            AnsweredAt = GetDateTime(data, "answeredAt"),
            EndedAt = GetDateTime(data, "endedAt"),
            Status = GetString(data, "status"),
            DurationSeconds = GetInt(data, "durationSeconds"),
            RingCharge = GetDecimal(data, "ringCharge"),
            CallCharge = GetDecimal(data, "callCharge"),
            Subtotal = GetDecimal(data, "subtotal"),
            Tax = GetDecimal(data, "tax"),
            Total = GetDecimal(data, "total"),
            InvoiceId = GetNullableString(data, "invoiceId"),
            UniqueId = GetNullableString(data, "uniqueId"),
            LinkedId = GetNullableString(data, "linkedId"),
            CreatedAt = GetDateTime(data, "createdAt") ?? DateTime.UtcNow
        };
    }

    public static Invoice MapInvoice(DocumentSnapshot snap)
    {
        var data = snap.ToDictionary();
        return new Invoice
        {
            Id = snap.Id,
            Number = GetString(data, "number"),
            CallId = GetString(data, "callId"),
            Source = GetString(data, "source"),
            Destination = GetString(data, "destination"),
            DurationSeconds = GetInt(data, "durationSeconds"),
            RingCharge = GetDecimal(data, "ringCharge"),
            CallCharge = GetDecimal(data, "callCharge"),
            Subtotal = GetDecimal(data, "subtotal"),
            Tax = GetDecimal(data, "tax"),
            Total = GetDecimal(data, "total"),
            Status = GetString(data, "status"),
            IssuedAt = GetDateTime(data, "issuedAt") ?? DateTime.UtcNow,
            PaidAt = GetDateTime(data, "paidAt"),
            CreatedAt = GetDateTime(data, "createdAt") ?? DateTime.UtcNow
        };
    }

    public static ExtensionInfo MapExtension(DocumentSnapshot snap)
    {
        var data = snap.ToDictionary();
        return new ExtensionInfo
        {
            Extension = GetString(data, "extension"),
            Branch = GetString(data, "branch"),
            Status = string.IsNullOrWhiteSpace(GetString(data, "status")) ? "Disponible" : GetString(data, "status")
        };
    }

    public static MonthlyBilling MapMonthlyBilling(DocumentSnapshot snap)
    {
        var data = snap.ToDictionary();
        return new MonthlyBilling
        {
            Id = snap.Id,
            Extension = GetString(data, "extension"),
            Year = GetInt(data, "year"),
            Month = GetInt(data, "month"),
            CallCount = GetInt(data, "callCount"),
            Subtotal = GetDecimal(data, "subtotal"),
            Tax = GetDecimal(data, "tax"),
            Total = GetDecimal(data, "total"),
            Status = GetString(data, "status"),
            CreatedAt = GetDateTime(data, "createdAt") ?? DateTime.UtcNow
        };
    }

    private static string GetString(IReadOnlyDictionary<string, object> data, string key)
        => data.TryGetValue(key, out var value) && value is not null ? value.ToString() ?? string.Empty : string.Empty;

    private static string? GetNullableString(IReadOnlyDictionary<string, object> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        var text = value.ToString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static int GetInt(IReadOnlyDictionary<string, object> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value is null)
        {
            return 0;
        }

        return value switch
        {
            long l => (int)l,
            int i => i,
            double d => (int)d,
            float f => (int)f,
            string s when int.TryParse(s, out var parsed) => parsed,
            _ => Convert.ToInt32(value)
        };
    }

    private static decimal GetDecimal(IReadOnlyDictionary<string, object> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value is null)
        {
            return 0m;
        }

        return value switch
        {
            double d => Convert.ToDecimal(d),
            float f => Convert.ToDecimal(f),
            long l => l,
            int i => i,
            string s when decimal.TryParse(s, out var parsed) => parsed,
            _ => Convert.ToDecimal(value)
        };
    }

    private static DateTime? GetDateTime(IReadOnlyDictionary<string, object> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            Timestamp ts => ts.ToDateTime().ToLocalTime(),
            DateTime dt => dt.Kind == DateTimeKind.Utc ? dt.ToLocalTime() : dt,
            string s when DateTime.TryParse(s, out var parsed) => parsed,
            _ => null
        };
    }
}
