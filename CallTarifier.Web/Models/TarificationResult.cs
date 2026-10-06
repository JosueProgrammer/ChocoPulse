namespace CallTarifier.Web.Models;

public sealed class TarificationResult
{
    public decimal RingCharge { get; init; }

    public decimal CallCharge { get; init; }

    public decimal Subtotal { get; init; }

    public decimal Tax { get; init; }

    public decimal Total { get; init; }
}
