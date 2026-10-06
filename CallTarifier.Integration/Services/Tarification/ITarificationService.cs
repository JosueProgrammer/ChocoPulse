namespace CallTarifier.Integration.Services.Tarification;

public interface ITarificationService
{
    Models.TarificationResult Calculate(
        int durationSeconds,
        decimal ringCharge,
        decimal pricePerSecond,
        decimal taxRate);
}
