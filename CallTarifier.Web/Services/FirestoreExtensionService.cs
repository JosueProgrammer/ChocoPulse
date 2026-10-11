using CallTarifier.Web.Models;

namespace CallTarifier.Web.Services;

public sealed class FirestoreExtensionService : IExtensionService
{
    private static readonly IReadOnlyList<ExtensionInfo> Fallback =
    [
        new() { Extension = "4000", Branch = "Carazo", Status = "Disponible" },
        new() { Extension = "4021", Branch = "Masaya", Status = "Disponible" },
        new() { Extension = "4041", Branch = "Rivas", Status = "Disponible" },
        new() { Extension = "4061", Branch = "Granada", Status = "Disponible" },
        new() { Extension = "4101", Branch = "León", Status = "Disponible" },
        new() { Extension = "4121", Branch = "Boaco", Status = "Disponible" },
        new() { Extension = "4141", Branch = "Managua", Status = "Disponible" }
    ];

    private readonly IFirestoreDbFactory _dbFactory;
    private readonly ILogger<FirestoreExtensionService> _logger;

    public FirestoreExtensionService(IFirestoreDbFactory dbFactory, ILogger<FirestoreExtensionService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ExtensionInfo>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var db = await _dbFactory.GetDbAsync(cancellationToken);
            var snap = await db.Collection("extensions").GetSnapshotAsync(cancellationToken);
            if (snap.Count == 0)
            {
                _logger.LogWarning("No extensions in Firestore; using built-in directory");
                return Fallback;
            }

            return snap.Documents
                .Select(FirestoreMappers.MapExtension)
                .OrderBy(e => e.Extension)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load extensions from Firestore; using built-in directory");
            return Fallback;
        }
    }

    public async Task<ExtensionInfo?> GetByExtensionAsync(string extension, CancellationToken cancellationToken = default)
    {
        var all = await GetAllAsync(cancellationToken);
        return all.FirstOrDefault(e => e.Extension == extension);
    }

    public bool IsValidExtension(string extension)
        => Fallback.Any(e => e.Extension == extension);
}
