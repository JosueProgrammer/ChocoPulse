using CallTarifier.Web.Models;
using Google.Cloud.Firestore;

namespace CallTarifier.Web.Services;

public sealed class FirestoreInvoiceService : IInvoiceService
{
    private readonly IFirestoreDbFactory _dbFactory;
    private readonly ILogger<FirestoreInvoiceService> _logger;

    public FirestoreInvoiceService(IFirestoreDbFactory dbFactory, ILogger<FirestoreInvoiceService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Invoice>> GetInvoicesAsync(CancellationToken cancellationToken = default)
    {
        var db = await _dbFactory.GetDbAsync(cancellationToken);
        var snap = await db.Collection("invoices").OrderByDescending("issuedAt").Limit(200).GetSnapshotAsync(cancellationToken);
        var invoices = snap.Documents.Select(FirestoreMappers.MapInvoice).ToList();
        _logger.LogDebug("Firestore invoices returned {Count}", invoices.Count);
        return invoices;
    }

    public async Task<Invoice?> GetByNumberAsync(string number, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            return null;
        }

        var db = await _dbFactory.GetDbAsync(cancellationToken);
        var snap = await db.Collection("invoices")
            .WhereEqualTo("number", number)
            .Limit(1)
            .GetSnapshotAsync(cancellationToken);

        var doc = snap.Documents.FirstOrDefault();
        return doc is null ? null : FirestoreMappers.MapInvoice(doc);
    }

    public async Task<MonthlyBilling?> GetCurrentPeriodAsync(string extension, CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now;
        var id = $"{extension}_{now.Year}_{now.Month:00}";
        var db = await _dbFactory.GetDbAsync(cancellationToken);
        var snap = await db.Collection("monthlyBilling").Document(id).GetSnapshotAsync(cancellationToken);
        return snap.Exists ? FirestoreMappers.MapMonthlyBilling(snap) : null;
    }
}
