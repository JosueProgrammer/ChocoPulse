using CallTarifier.Integration.Models;

namespace CallTarifier.Integration.Services.Tarification;

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
            throw new ArgumentOutOfRangeException(nameof(durationSeconds));
        }

        var callCharge = RoundMoney(durationSeconds * pricePerSecond);
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
