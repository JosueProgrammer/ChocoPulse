using CallTarifier.Web.Configuration;
using CallTarifier.Web.Models;

namespace CallTarifier.Web.Services;

public enum CallKind
{
    Local,
    Interlocal,
    EntreE1,
    Claro,
    Tigo,
    Internacional
}

public static class CallPricing
{
    public static CallKind KindFor(string source, string destination)
    {
        var origin = Classify(source);
        var target = Classify(destination);
        if (origin == Party.CasaPellas || target == Party.CasaPellas)
        {
            return CallKind.EntreE1;
        }

        if (target == Party.Claro)
        {
            return CallKind.Claro;
        }

        if (target == Party.Tigo)
        {
            return CallKind.Tigo;
        }

        if (origin == Party.Foreign || target is Party.Foreign or Party.External)
        {
            return CallKind.Internacional;
        }

        if (origin == target)
        {
            return CallKind.Local;
        }

        return CallKind.Interlocal;
    }

    public static TarificationResult Quote(
        TariffOptions tariffs,
        CallKind kind,
        bool answered,
        int durationSeconds,
        int priorLocalAnsweredSeconds = 0)
    {
        var service = new TarificationService();
        if (!answered)
        {
            return service.Calculate(0, tariffs.UnansweredAttempt, 0, tariffs.TaxRate);
        }

        if (kind == CallKind.Local)
        {
            var flat = priorLocalAnsweredSeconds >= tariffs.LocalFreeMinutes * 60
                ? tariffs.LocalAfterQuota
                : 0m;
            return service.Calculate(0, flat, 0, tariffs.TaxRate);
        }

        var perSecond = PerMinute(tariffs, kind) / 60m;
        var result = service.Calculate(Math.Max(0, durationSeconds), 0, perSecond, tariffs.TaxRate);
        if (result.Subtotal < tariffs.MinimumAnswered)
        {
            return service.Calculate(0, tariffs.MinimumAnswered, 0, tariffs.TaxRate);
        }

        return result;
    }

    public static string Label(CallKind kind) => kind switch
    {
        CallKind.Local => "Local",
        CallKind.Interlocal => "Interlocal",
        CallKind.EntreE1 => "Entre E1",
        CallKind.Claro => "Claro",
        CallKind.Tigo => "Tigo",
        _ => "Internacional"
    };

    private static decimal PerMinute(TariffOptions tariffs, CallKind kind) => kind switch
    {
        CallKind.Interlocal => tariffs.InterlocalPerMinute,
        CallKind.EntreE1 => tariffs.EntreE1PerMinute,
        CallKind.Claro => tariffs.ClaroPerMinute,
        CallKind.Tigo => tariffs.TigoPerMinute,
        _ => tariffs.InternacionalPerMinute
    };

    private enum Party
    {
        Bac,
        Granada,
        CasaPellas,
        Foreign,
        Claro,
        Tigo,
        External
    }

    private static Party Classify(string number)
    {
        var digits = new string((number ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits is "82361811")
        {
            return Party.Claro;
        }

        if (digits is "83922921")
        {
            return Party.Tigo;
        }

        if (digits is "9000")
        {
            return Party.CasaPellas;
        }

        if (digits is "2000" or "2030" or "2060" or "2090" or "2120")
        {
            return Party.Foreign;
        }

        if (digits is "1000" or "0000" or "0001")
        {
            return Party.Bac;
        }

        if (int.TryParse(digits, out var value))
        {
            if (value is >= 1010 and <= 1017)
            {
                return Party.Bac;
            }

            if (value is >= 1040 and <= 1047)
            {
                return Party.Granada;
            }
        }

        return Party.External;
    }
}
