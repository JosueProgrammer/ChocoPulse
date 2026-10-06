namespace CallTarifier.Integration.Services.Cdr;

/// <summary>
/// Phase 5 placeholder for CDR processing.
/// </summary>
public interface ICdrProcessor
{
    Task ProcessAsync(CancellationToken cancellationToken);
}
