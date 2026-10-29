using CallTarifier.Web.Configuration;
using CallTarifier.Web.Models;
using CallTarifier.Web.Models.Enums;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Options;

namespace CallTarifier.Web.Services;

public sealed class PlacedCallRecorder
{
    private readonly IFirestoreDbFactory _dbFactory;
    private readonly ITarificationService _tarification;
    private readonly TariffOptions _tariffs;
    private readonly ILogger<PlacedCallRecorder> _logger;

    public PlacedCallRecorder(
        IFirestoreDbFactory dbFactory,
        ITarificationService tarification,
        IOptions<TariffOptions> tariffs,
        ILogger<PlacedCallRecorder> logger)
    {
        _dbFactory = dbFactory;
        _tarification = tarification;
        _tariffs = tariffs.Value;
        _logger = logger;
    }

    public async Task SaveAsync(FinishedCall call, CancellationToken cancellationToken = default)
    {
        var ended = DateTime.UtcNow;
        var answered = call.ConnectedUtc.HasValue;
        var duration = answered
            ? (int)Math.Max(0, (ended - call.ConnectedUtc!.Value.UtcDateTime).TotalSeconds)
            : 0;
        var status = answered
            ? CallStatus.Completed
            : call.Failed ? CallStatus.Failed : CallStatus.NoAnswer;
        var kind = CallPricing.KindFor(call.CallerExtension, call.Destination);
        var id = string.IsNullOrWhiteSpace(call.UniqueId)
            ? Guid.NewGuid().ToString("N")
            : call.UniqueId.Replace('.', '-');

        var db = await _dbFactory.GetDbAsync(cancellationToken);
        var priorLocal = answered && kind == CallKind.Local
            ? await LocalAnsweredSecondsAsync(db, call.CallerExtension, cancellationToken)
            : 0;
        var tariff = CallPricing.Quote(_tariffs, kind, answered, duration, priorLocal);
        if (!string.IsNullOrWhiteSpace(call.UniqueId))
        {
            var existing = await db.Collection("calls")
                .WhereEqualTo("uniqueId", call.UniqueId)
                .Limit(1)
                .GetSnapshotAsync(cancellationToken);
            if (existing.Count > 0)
            {
                return;
            }
        }

        string? invoiceId = null;
        if (tariff.Total > 0m)
        {
            invoiceId = await NextInvoiceNumberAsync(db, cancellationToken);
            await db.Collection("invoices").Document(invoiceId).SetAsync(new Dictionary<string, object>
            {
                ["number"] = invoiceId,
                ["callId"] = id,
                ["source"] = call.CallerExtension,
                ["destination"] = call.Destination,
                ["callType"] = CallPricing.Label(kind),
                ["durationSeconds"] = duration,
                ["ringCharge"] = (double)tariff.RingCharge,
                ["callCharge"] = (double)tariff.CallCharge,
                ["subtotal"] = (double)tariff.Subtotal,
                ["tax"] = (double)tariff.Tax,
                ["total"] = (double)tariff.Total,
                ["status"] = InvoiceStatus.Pending,
                ["issuedAt"] = Timestamp.FromDateTime(ended),
                ["createdAt"] = Timestamp.FromDateTime(ended)
            }, cancellationToken: cancellationToken);
        }

        var data = new Dictionary<string, object>
        {
            ["source"] = call.CallerExtension,
            ["destination"] = call.Destination,
            ["callType"] = CallPricing.Label(kind),
            ["startedAt"] = Timestamp.FromDateTime(call.StartedUtc.UtcDateTime),
            ["endedAt"] = Timestamp.FromDateTime(ended),
            ["status"] = status,
            ["durationSeconds"] = duration,
            ["ringCharge"] = (double)tariff.RingCharge,
            ["callCharge"] = (double)tariff.CallCharge,
            ["subtotal"] = (double)tariff.Subtotal,
            ["tax"] = (double)tariff.Tax,
            ["total"] = (double)tariff.Total,
            ["createdAt"] = Timestamp.FromDateTime(ended)
        };

        if (call.RingingUtc.HasValue)
        {
            data["ringingAt"] = Timestamp.FromDateTime(call.RingingUtc.Value.UtcDateTime);
        }

        if (answered)
        {
            data["answeredAt"] = Timestamp.FromDateTime(call.ConnectedUtc!.Value.UtcDateTime);
        }

        if (!string.IsNullOrWhiteSpace(invoiceId))
        {
            data["invoiceId"] = invoiceId;
        }

        if (!string.IsNullOrWhiteSpace(call.UniqueId))
        {
            data["uniqueId"] = call.UniqueId;
        }

        await db.Collection("calls").Document(id).SetAsync(data, cancellationToken: cancellationToken);
        await UpdateMonthlyBillingAsync(db, call.CallerExtension, call.StartedUtc, tariff, cancellationToken);
        _logger.LogInformation(
            "Call saved {Source}->{Destination} status {Status} duration {Duration}s total {Total}",
            call.CallerExtension,
            call.Destination,
            status,
            duration,
            tariff.Total);
    }

    private static async Task<int> LocalAnsweredSecondsAsync(FirestoreDb db, string extension, CancellationToken cancellationToken)
    {
        var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        var snap = await db.Collection("calls").WhereEqualTo("source", extension).GetSnapshotAsync(cancellationToken);
        var seconds = 0;
        foreach (var doc in snap.Documents)
        {
            var call = FirestoreMappers.MapCall(doc);
            if (call.StartedAt < monthStart || call.Status != CallStatus.Completed)
            {
                continue;
            }

            if (CallPricing.KindFor(call.Source, call.Destination) == CallKind.Local)
            {
                seconds += call.DurationSeconds;
            }
        }

        return seconds;
    }

    private static async Task<string> NextInvoiceNumberAsync(FirestoreDb db, CancellationToken cancellationToken)
    {
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

    private static async Task UpdateMonthlyBillingAsync(
        FirestoreDb db,
        string extension,
        DateTimeOffset started,
        TarificationResult tariff,
        CancellationToken cancellationToken)
    {
        var local = started.ToLocalTime();
        var id = $"{extension}_{local.Year}_{local.Month:00}";
        var doc = db.Collection("monthlyBilling").Document(id);
        await db.RunTransactionAsync(async transaction =>
        {
            var snap = await transaction.GetSnapshotAsync(doc, cancellationToken);
            var callCount = snap.Exists && snap.ContainsField("callCount") ? snap.GetValue<long>("callCount") : 0;
            var subtotal = snap.Exists && snap.ContainsField("subtotal") ? Convert.ToDecimal(snap.GetValue<double>("subtotal")) : 0m;
            var tax = snap.Exists && snap.ContainsField("tax") ? Convert.ToDecimal(snap.GetValue<double>("tax")) : 0m;
            var total = snap.Exists && snap.ContainsField("total") ? Convert.ToDecimal(snap.GetValue<double>("total")) : 0m;

            transaction.Set(doc, new Dictionary<string, object>
            {
                ["extension"] = extension,
                ["year"] = local.Year,
                ["month"] = local.Month,
                ["callCount"] = callCount + 1,
                ["subtotal"] = (double)Math.Round(subtotal + tariff.Subtotal, 2, MidpointRounding.AwayFromZero),
                ["tax"] = (double)Math.Round(tax + tariff.Tax, 2, MidpointRounding.AwayFromZero),
                ["total"] = (double)Math.Round(total + tariff.Total, 2, MidpointRounding.AwayFromZero),
                ["status"] = InvoiceStatus.Pending,
                ["createdAt"] = Timestamp.FromDateTime(DateTime.UtcNow)
            });
        }, cancellationToken: cancellationToken);
    }
}

public sealed record FinishedCall(
    string CallerExtension,
    string Destination,
    DateTimeOffset StartedUtc,
    DateTimeOffset? RingingUtc,
    DateTimeOffset? ConnectedUtc,
    string? UniqueId,
    bool Failed);
