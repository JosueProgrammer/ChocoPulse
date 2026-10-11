using CallTarifier.Integration.Configuration;
using CallTarifier.Integration.Models;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Options;

namespace CallTarifier.Integration.Services.Firebase;

public sealed class FirebaseService : IFirebaseService
{
    private readonly FirebaseOptions _options;
    private readonly TariffOptions _tariffs;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<FirebaseService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private FirestoreDb? _db;

    public FirebaseService(
        IOptions<FirebaseOptions> options,
        IOptions<TariffOptions> tariffs,
        IHostEnvironment environment,
        ILogger<FirebaseService> logger)
    {
        _options = options.Value;
        _tariffs = tariffs.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        try
        {
            var db = await GetDbAsync(cancellationToken);
            await db.Collection("settings").Limit(1).GetSnapshotAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Firebase not available");
            return false;
        }
    }

    public async Task SynchronizeAsync(CancellationToken cancellationToken)
    {
        var available = await IsAvailableAsync(cancellationToken);
        if (available)
        {
            _logger.LogDebug("Firebase synchronization successful");
        }
    }

    public async Task EnsureSeedAsync(CancellationToken cancellationToken)
    {
        var db = await GetDbAsync(cancellationToken);

        var extensions = new (string Ext, string Branch)[]
        {
            ("4000", "Carazo"),
            ("4021", "Masaya"),
            ("4041", "Rivas"),
            ("4061", "Granada"),
            ("4101", "León"),
            ("4121", "Boaco"),
            ("4141", "Managua")
        };

        foreach (var (ext, branch) in extensions)
        {
            var doc = db.Collection("extensions").Document(ext);
            var snap = await doc.GetSnapshotAsync(cancellationToken);
            if (!snap.Exists)
            {
                await doc.SetAsync(new Dictionary<string, object>
                {
                    ["extension"] = ext,
                    ["branch"] = branch,
                    ["status"] = "Disponible"
                }, cancellationToken: cancellationToken);
            }
        }

        var tariffDoc = db.Collection("settings").Document("tariffs");
        if (!(await tariffDoc.GetSnapshotAsync(cancellationToken)).Exists)
        {
            await tariffDoc.SetAsync(new Dictionary<string, object>
            {
                ["ringCharge"] = (double)_tariffs.RingCharge,
                ["pricePerSecond"] = (double)_tariffs.PricePerSecond,
                ["pricePerMinute"] = (double)_tariffs.PricePerMinute,
                ["taxRate"] = (double)_tariffs.TaxRate,
                ["chargeRingForUnansweredCalls"] = _tariffs.ChargeRingForUnansweredCalls
            }, cancellationToken: cancellationToken);
        }

        var counterDoc = db.Collection("settings").Document("invoiceCounter");
        if (!(await counterDoc.GetSnapshotAsync(cancellationToken)).Exists)
        {
            await counterDoc.SetAsync(new Dictionary<string, object>
            {
                ["lastNumber"] = 1000
            }, cancellationToken: cancellationToken);
        }
    }

    public async Task<bool> CallExistsByUniqueIdAsync(string uniqueId, CancellationToken cancellationToken)
    {
        var db = await GetDbAsync(cancellationToken);
        var snap = await db.Collection("calls")
            .WhereEqualTo("uniqueId", uniqueId)
            .Limit(1)
            .GetSnapshotAsync(cancellationToken);
        return snap.Count > 0;
    }

    public async Task SaveCallAsync(CallRecord call, CancellationToken cancellationToken)
    {
        var db = await GetDbAsync(cancellationToken);
        var data = new Dictionary<string, object>
        {
            ["source"] = call.Source,
            ["destination"] = call.Destination,
            ["startedAt"] = Timestamp.FromDateTime(DateTime.SpecifyKind(call.StartedAt.ToUniversalTime(), DateTimeKind.Utc)),
            ["status"] = call.Status,
            ["durationSeconds"] = call.DurationSeconds,
            ["ringCharge"] = (double)call.RingCharge,
            ["callCharge"] = (double)call.CallCharge,
            ["subtotal"] = (double)call.Subtotal,
            ["tax"] = (double)call.Tax,
            ["total"] = (double)call.Total,
            ["createdAt"] = Timestamp.FromDateTime(DateTime.UtcNow)
        };

        if (call.RingingAt.HasValue)
        {
            data["ringingAt"] = Timestamp.FromDateTime(DateTime.SpecifyKind(call.RingingAt.Value.ToUniversalTime(), DateTimeKind.Utc));
        }

        if (call.AnsweredAt.HasValue)
        {
            data["answeredAt"] = Timestamp.FromDateTime(DateTime.SpecifyKind(call.AnsweredAt.Value.ToUniversalTime(), DateTimeKind.Utc));
        }

        if (call.EndedAt.HasValue)
        {
            data["endedAt"] = Timestamp.FromDateTime(DateTime.SpecifyKind(call.EndedAt.Value.ToUniversalTime(), DateTimeKind.Utc));
        }

        if (!string.IsNullOrWhiteSpace(call.InvoiceId))
        {
            data["invoiceId"] = call.InvoiceId;
        }

        if (!string.IsNullOrWhiteSpace(call.UniqueId))
        {
            data["uniqueId"] = call.UniqueId;
        }

        if (!string.IsNullOrWhiteSpace(call.LinkedId))
        {
            data["linkedId"] = call.LinkedId;
        }

        await db.Collection("calls").Document(call.Id).SetAsync(data, cancellationToken: cancellationToken);
        await UpdateMonthlyBillingAsync(call, cancellationToken);
    }

    public async Task SaveInvoiceAsync(InvoiceRecord invoice, CancellationToken cancellationToken)
    {
        var db = await GetDbAsync(cancellationToken);
        await db.Collection("invoices").Document(invoice.Id).SetAsync(new Dictionary<string, object>
        {
            ["number"] = invoice.Number,
            ["callId"] = invoice.CallId,
            ["source"] = invoice.Source,
            ["destination"] = invoice.Destination,
            ["durationSeconds"] = invoice.DurationSeconds,
            ["ringCharge"] = (double)invoice.RingCharge,
            ["callCharge"] = (double)invoice.CallCharge,
            ["subtotal"] = (double)invoice.Subtotal,
            ["tax"] = (double)invoice.Tax,
            ["total"] = (double)invoice.Total,
            ["status"] = invoice.Status,
            ["issuedAt"] = Timestamp.FromDateTime(DateTime.SpecifyKind(invoice.IssuedAt.ToUniversalTime(), DateTimeKind.Utc)),
            ["createdAt"] = Timestamp.FromDateTime(DateTime.UtcNow)
        }, cancellationToken: cancellationToken);
    }

    public async Task<string> NextInvoiceNumberAsync(CancellationToken cancellationToken)
    {
        var db = await GetDbAsync(cancellationToken);
        var doc = db.Collection("settings").Document("invoiceCounter");

        return await db.RunTransactionAsync(async transaction =>
        {
            var snap = await transaction.GetSnapshotAsync(doc, cancellationToken);
            var last = snap.Exists && snap.ContainsField("lastNumber")
                ? snap.GetValue<long>("lastNumber")
                : 1000L;
            var next = last + 1;
            transaction.Set(doc, new Dictionary<string, object> { ["lastNumber"] = next });
            return $"E{next}";
        }, cancellationToken: cancellationToken);
    }

    private async Task UpdateMonthlyBillingAsync(CallRecord call, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(call.Source))
        {
            return;
        }

        var local = call.StartedAt.ToLocalTime();
        var id = $"{call.Source}_{local.Year}_{local.Month:00}";
        var db = await GetDbAsync(cancellationToken);
        var doc = db.Collection("monthlyBilling").Document(id);

        await db.RunTransactionAsync(async transaction =>
        {
            var snap = await transaction.GetSnapshotAsync(doc, cancellationToken);
            var callCount = snap.Exists && snap.ContainsField("callCount") ? snap.GetValue<long>("callCount") : 0;
            var subtotal = snap.Exists && snap.ContainsField("subtotal") ? Convert.ToDecimal(snap.GetValue<double>("subtotal")) : 0m;
            var tax = snap.Exists && snap.ContainsField("tax") ? Convert.ToDecimal(snap.GetValue<double>("tax")) : 0m;
            var total = snap.Exists && snap.ContainsField("total") ? Convert.ToDecimal(snap.GetValue<double>("total")) : 0m;

            callCount += 1;
            subtotal += call.Subtotal;
            tax += call.Tax;
            total += call.Total;

            transaction.Set(doc, new Dictionary<string, object>
            {
                ["extension"] = call.Source,
                ["year"] = local.Year,
                ["month"] = local.Month,
                ["callCount"] = callCount,
                ["subtotal"] = (double)Math.Round(subtotal, 2, MidpointRounding.AwayFromZero),
                ["tax"] = (double)Math.Round(tax, 2, MidpointRounding.AwayFromZero),
                ["total"] = (double)Math.Round(total, 2, MidpointRounding.AwayFromZero),
                ["status"] = "PENDING",
                ["createdAt"] = Timestamp.FromDateTime(DateTime.UtcNow)
            });
        }, cancellationToken: cancellationToken);
    }

    private async Task<FirestoreDb> GetDbAsync(CancellationToken cancellationToken)
    {
        if (_db is not null)
        {
            return _db;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_db is not null)
            {
                return _db;
            }

            var path = ResolvePath(_options.ServiceAccountPath);
            if (string.IsNullOrWhiteSpace(_options.ProjectId) || !File.Exists(path))
            {
                throw new InvalidOperationException("Firebase service account not configured for Integration.");
            }

            _db = await new FirestoreDbBuilder
            {
                ProjectId = _options.ProjectId,
                Credential = GoogleCredential.FromFile(path)
            }.BuildAsync(cancellationToken);

            return _db;
        }
        finally
        {
            _lock.Release();
        }
    }

    private string ResolvePath(string relativeOrAbsolute)
    {
        if (string.IsNullOrWhiteSpace(relativeOrAbsolute))
        {
            return string.Empty;
        }

        return Path.IsPathRooted(relativeOrAbsolute)
            ? relativeOrAbsolute
            : Path.GetFullPath(Path.Combine(_environment.ContentRootPath, relativeOrAbsolute));
    }
}
