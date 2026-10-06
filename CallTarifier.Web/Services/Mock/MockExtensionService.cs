using CallTarifier.Web.Models;

namespace CallTarifier.Web.Services.Mock;

public sealed class MockExtensionService : IExtensionService
{
    private readonly MockDataStore _store;

    public MockExtensionService(MockDataStore store)
    {
        _store = store;
    }

    public Task<IReadOnlyList<ExtensionInfo>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_store.Extensions);
    }

    public Task<ExtensionInfo?> GetByExtensionAsync(string extension, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var item = _store.Extensions.FirstOrDefault(e => e.Extension == extension);
        return Task.FromResult(item);
    }

    public bool IsValidExtension(string extension)
        => _store.Extensions.Any(e => e.Extension == extension);
}
