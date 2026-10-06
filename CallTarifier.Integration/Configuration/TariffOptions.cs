namespace CallTarifier.Integration.Configuration;

public sealed class TariffOptions
{
    public const string SectionName = "Tariffs";

    public decimal RingCharge { get; set; } = 0.50m;

    public decimal PricePerSecond { get; set; } = 0.05m;

    public decimal PricePerMinute { get; set; } = 3.00m;

    public decimal TaxRate { get; set; } = 0.15m;

    public bool ChargeRingForUnansweredCalls { get; set; }
}
