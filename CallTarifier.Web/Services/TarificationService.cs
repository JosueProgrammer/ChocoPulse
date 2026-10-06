using CallTarifier.Web.Models;

namespace CallTarifier.Web.Services;

public sealed class TarificationService : ITarificationService
{
    public TarificationResult Calculate(
        int durationSeconds,
        decimal ringCharge,
        decimal pricePerSecond,
        decimal taxRate)
    {
        if (durationSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationSeconds), "La duración no puede ser negativa.");
        }

        if (ringCharge < 0 || pricePerSecond < 0 || taxRate < 0)
        {
            throw new ArgumentOutOfRangeException("Los valores monetarios y la tasa no pueden ser negativos.");
        }

        var billableSeconds = durationSeconds;
        var callCharge = RoundMoney(billableSeconds * pricePerSecond);
        var appliedRing = RoundMoney(ringCharge);
        var subtotal = RoundMoney(appliedRing + callCharge);
        var tax = RoundMoney(subtotal * taxRate);
        var total = RoundMoney(subtotal + tax);

        return new TarificationResult
        {
            RingCharge = appliedRing,
            CallCharge = callCharge,
            Subtotal = subtotal,
            Tax = tax,
            Total = total
        };
    }

    private static decimal RoundMoney(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
