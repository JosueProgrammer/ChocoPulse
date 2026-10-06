using CallTarifier.Web.Models;

namespace CallTarifier.Web.Services;

public interface ITarificationService
{
    TarificationResult Calculate(
        int durationSeconds,
        decimal ringCharge,
        decimal pricePerSecond,
        decimal taxRate);
}
