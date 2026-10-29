using CallTarifier.Web.Models;
using Google.Cloud.Firestore;

namespace CallTarifier.Web.Services;

public sealed class FirestoreExtensionService : IExtensionService
{
    private static readonly IReadOnlyList<ExtensionInfo> Fallback =
    [
        new() { Extension = "1000", Branch = "E1", Status = "Disponible" },
        new() { Extension = "1010", Branch = "Central Carazo Bac", Status = "Disponible" },
        new() { Extension = "1011", Branch = "Gerencia de sucursal", Status = "Disponible" },
        new() { Extension = "1012", Branch = "Atención al cliente", Status = "Disponible" },
        new() { Extension = "1013", Branch = "Caja Central", Status = "Disponible" },
        new() { Extension = "1014", Branch = "Creditos", Status = "Disponible" },
        new() { Extension = "1015", Branch = "Seguridad", Status = "Disponible" },
        new() { Extension = "1016", Branch = "Contabilidad", Status = "Disponible" },
        new() { Extension = "1017", Branch = "Soporte Tecnico", Status = "Disponible" },
        new() { Extension = "1040", Branch = "Granada BAC", Status = "Disponible" },
        new() { Extension = "1041", Branch = "Gerencia de sucursal", Status = "Disponible" },
        new() { Extension = "1042", Branch = "Atención al cliente GR", Status = "Disponible" },
        new() { Extension = "1043", Branch = "Caja Central GR", Status = "Disponible" },
        new() { Extension = "1044", Branch = "Creditos GR", Status = "Disponible" },
        new() { Extension = "1045", Branch = "Seguridad GR", Status = "Disponible" },
        new() { Extension = "1046", Branch = "Contabilidad GR", Status = "Disponible" },
        new() { Extension = "1047", Branch = "Soporte Tecnico GR", Status = "Disponible" },
        new() { Extension = "2000", Branch = "Oficina de Estados unidos", Status = "Disponible" },
        new() { Extension = "2030", Branch = "Oficina de Costa Rica", Status = "Disponible" },
        new() { Extension = "2060", Branch = "Oficina de Panama", Status = "Disponible" },
        new() { Extension = "2090", Branch = "Oficina de España", Status = "Disponible" },
        new() { Extension = "2120", Branch = "Oficina de México", Status = "Disponible" },
        new() { Extension = "82361811", Branch = "Claro", Status = "Disponible" },
        new() { Extension = "83922921", Branch = "Tigo", Status = "Disponible" }
    ];

    private readonly IFirestoreDbFactory _dbFactory;
    private readonly IssabelExtensionRefresher _refresher;
    private readonly ILogger<FirestoreExtensionService> _logger;

    public FirestoreExtensionService(
        IFirestoreDbFactory dbFactory,
        IssabelExtensionRefresher refresher,
        ILogger<FirestoreExtensionService> logger)
    {
        _dbFactory = dbFactory;
        _refresher = refresher;
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

            var fromDb = snap.Documents
                .Select(FirestoreMappers.MapExtension)
                .Where(ext => !string.IsNullOrWhiteSpace(ext.Extension))
                .OrderBy(ext => ext.Extension)
                .ToList();
            ExtensionDirectory.Publish(fromDb);
            return fromDb;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load extensions from Firestore; using built-in directory");
            return Fallback;
        }
    }

    public async Task<IReadOnlyList<ExtensionInfo>?> RefreshFromPbxAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var loaded = await _refresher.TryLoadAsync(cancellationToken);
            if (loaded is null || loaded.Count == 0)
            {
                return null;
            }

            _logger.LogInformation(
                "Issabel read {Count} extensions: {Catalog}",
                loaded.Count,
                string.Join(" | ", loaded.Select(item => $"{item.Extension} {item.Branch}")));

            var db = await _dbFactory.GetDbAsync(cancellationToken);
            var keep = loaded.Select(ext => ext.Extension).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var ext in loaded)
            {
                await db.Collection("extensions").Document(ext.Extension).SetAsync(new Dictionary<string, object>
                {
                    ["extension"] = ext.Extension,
                    ["branch"] = ext.Branch,
                    ["status"] = string.IsNullOrWhiteSpace(ext.Status) ? "Disponible" : ext.Status
                }, SetOptions.MergeAll, cancellationToken);
            }

            var existing = await db.Collection("extensions").GetSnapshotAsync(cancellationToken);
            foreach (var doc in existing.Documents)
            {
                if (!keep.Contains(doc.Id))
                {
                    await doc.Reference.DeleteAsync(cancellationToken: cancellationToken);
                }
            }

            ExtensionDirectory.Publish(loaded);
            return loaded;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not refresh extensions from Issabel");
            return null;
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
