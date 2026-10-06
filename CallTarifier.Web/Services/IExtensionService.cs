using CallTarifier.Web.Models;

namespace CallTarifier.Web.Services;

public interface IExtensionService
{
    Task<IReadOnlyList<ExtensionInfo>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ExtensionInfo?> GetByExtensionAsync(string extension, CancellationToken cancellationToken = default);

    bool IsValidExtension(string extension);
}
