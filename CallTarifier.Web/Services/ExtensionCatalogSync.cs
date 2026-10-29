using Google.Cloud.Firestore;

namespace CallTarifier.Web.Services;

public sealed class ExtensionCatalogSync : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExtensionCatalogSync> _logger;

    public ExtensionCatalogSync(IServiceScopeFactory scopeFactory, ILogger<ExtensionCatalogSync> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        await AssignE1OwnerAsync(scope.ServiceProvider, cancellationToken);

        try
        {
            var extensions = scope.ServiceProvider.GetRequiredService<IExtensionService>();
            var loaded = await extensions.RefreshFromPbxAsync(cancellationToken);
            if (loaded is null)
            {
                _logger.LogWarning("Issabel extension catalog was not refreshed at startup");
                return;
            }

            _logger.LogInformation(
                "Issabel extension catalog: {Catalog}",
                string.Join(" | ", loaded.Select(item => $"{item.Extension} {item.Branch}")));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Issabel extension catalog was not refreshed at startup");
        }
    }

    private async Task AssignE1OwnerAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        try
        {
            var db = await services.GetRequiredService<IFirestoreDbFactory>().GetDbAsync(cancellationToken);
            var snap = await db.Collection("users").GetSnapshotAsync(cancellationToken);
            _logger.LogInformation("Firestore users found: {Count}", snap.Count);
            var updated = 0;
            foreach (var doc in snap.Documents)
            {
                var email = ReadText(doc, "email");
                var name = ReadText(doc, "name");
                var extension = ReadText(doc, "extension");
                if (!IsIsraelProfile(email, name, extension))
                {
                    continue;
                }

                _logger.LogInformation("Matched E1 owner profile {Email} {Name} previous extension {Extension}", email, name, extension);

                await doc.Reference.SetAsync(new Dictionary<string, object>
                {
                    ["extension"] = E1Owner.Extension,
                    ["branch"] = E1Owner.Branch,
                    ["extensionChosen"] = true
                }, SetOptions.MergeAll, cancellationToken);
                updated++;
                _logger.LogInformation("Removed previous extension and assigned E1 1000 to {Email}", email);
            }

            if (updated == 0)
            {
                _logger.LogWarning("No Firestore user matched the E1 owner email");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not assign the E1 owner extension");
        }
    }

    private static bool IsIsraelProfile(string email, string name, string extension)
    {
        if (E1Owner.IsOwner(email))
        {
            return true;
        }

        var text = (email + " " + name).ToLowerInvariant();
        var looksLikeIsrael = text.Contains("israel", StringComparison.Ordinal) && text.Contains("ramos", StringComparison.Ordinal);
        return looksLikeIsrael && extension is "0001" or "1000" or "";
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static string ReadText(DocumentSnapshot doc, string field)
    {
        if (!doc.ContainsField(field))
        {
            return string.Empty;
        }

        var value = doc.GetValue<object>(field);
        return value?.ToString()?.Trim() ?? string.Empty;
    }
}
