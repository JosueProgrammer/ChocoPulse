namespace CallTarifier.Integration.Configuration;

public sealed class TariffOptions
{
    public const string SectionName = "Tariffs";

    public decimal RingCharge { get; set; } = 1.00m;

    public decimal PricePerSecond { get; set; } = 0.0333m;

    public decimal PricePerMinute { get; set; } = 2.00m;

    public decimal TaxRate { get; set; } = 0.15m;

    public bool ChargeRingForUnansweredCalls { get; set; } = true;

    public decimal LocalPerMinute { get; set; }

    public int LocalFreeMinutes { get; set; } = 200;

    public decimal LocalAfterQuota { get; set; } = 0.50m;

    public decimal InterlocalPerMinute { get; set; } = 3.00m;

    public decimal EntreE1PerMinute { get; set; } = 3.60m;

    public decimal ClaroPerMinute { get; set; } = 4.00m;

    public decimal TigoPerMinute { get; set; } = 6.00m;

    public decimal InternacionalPerMinute { get; set; } = 10.00m;

    public decimal MinimumAnswered { get; set; } = 0.50m;

    public decimal UnansweredAttempt { get; set; } = 0.20m;
}
