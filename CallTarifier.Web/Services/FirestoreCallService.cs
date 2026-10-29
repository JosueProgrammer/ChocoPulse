using CallTarifier.Web.Models;
using CallTarifier.Web.Models.Enums;

namespace CallTarifier.Web.Services;

public sealed class FirestoreCallService : ICallService
{
    private readonly IFirestoreDbFactory _dbFactory;
    private readonly ILogger<FirestoreCallService> _logger;

    public FirestoreCallService(IFirestoreDbFactory dbFactory, ILogger<FirestoreCallService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Call>> GetCallsAsync(CallFilter? filter = null, CancellationToken cancellationToken = default)
    {
        var db = await _dbFactory.GetDbAsync(cancellationToken);
        var snap = await db.Collection("calls").OrderByDescending("startedAt").Limit(500).GetSnapshotAsync(cancellationToken);
        IEnumerable<Call> query = snap.Documents.Select(FirestoreMappers.MapCall);

        if (filter is not null)
        {
            if (filter.From.HasValue)
            {
                query = query.Where(c => c.StartedAt >= filter.From.Value);
            }

            if (filter.To.HasValue)
            {
                var to = filter.To.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(c => c.StartedAt <= to);
            }

            if (!string.IsNullOrWhiteSpace(filter.Extension))
            {
                query = query.Where(c => c.Source == filter.Extension || c.Destination == filter.Extension);
            }

            if (!string.IsNullOrWhiteSpace(filter.Source))
            {
                query = query.Where(c => c.Source == filter.Source);
            }

            if (!string.IsNullOrWhiteSpace(filter.Destination))
            {
                query = query.Where(c => c.Destination == filter.Destination);
            }

            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                query = query.Where(c => c.Status.Equals(filter.Status, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var term = filter.Search.Trim();
                query = query.Where(c =>
                    c.Source.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    c.Destination.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (c.InvoiceId?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    c.Id.Contains(term, StringComparison.OrdinalIgnoreCase));
            }
        }

        var result = query.ToList();
        _logger.LogDebug("Firestore calls query returned {Count}", result.Count);
        return result;
    }

    public async Task<Call?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var db = await _dbFactory.GetDbAsync(cancellationToken);
        var snap = await db.Collection("calls").Document(id).GetSnapshotAsync(cancellationToken);
        return snap.Exists ? FirestoreMappers.MapCall(snap) : null;
    }

    public async Task<DashboardSummary> GetDashboardAsync(string extension, CancellationToken cancellationToken = default)
    {
        var calls = await GetCallsAsync(cancellationToken: cancellationToken);
        var now = DateTime.Now;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var month = calls.Where(c => c.StartedAt >= monthStart).ToList();
        var central = extension == "1000";
        var own = string.IsNullOrWhiteSpace(extension)
            ? month
            : month.Where(c => c.Source == extension || c.Destination == extension).ToList();
        var billed = central ? month : own.Where(c => c.Source == extension).ToList();

        var db = await _dbFactory.GetDbAsync(cancellationToken);
        var pendingSnap = await db.Collection("invoices")
            .WhereEqualTo("status", InvoiceStatus.Pending)
            .GetSnapshotAsync(cancellationToken);
        var pending = pendingSnap.Documents.Select(FirestoreMappers.MapInvoice);
        if (!central && !string.IsNullOrWhiteSpace(extension))
        {
            pending = pending.Where(invoice => invoice.Source == extension);
        }

        var breakdownSource = central ? month : billed;
        var byExtension = breakdownSource
            .GroupBy(c => string.IsNullOrWhiteSpace(c.Source) ? "—" : c.Source)
            .Select(group => new ExtensionConsumption
            {
                Extension = group.Key,
                Name = DisplayHelpers.BranchFor(group.Key),
                Calls = group.Count(),
                Subtotal = group.Sum(c => c.Subtotal),
                Tax = group.Sum(c => c.Tax),
                Total = group.Sum(c => c.Total)
            })
            .OrderBy(row => row.Extension)
            .ToList();

        return new DashboardSummary
        {
            UserName = string.Empty,
            Extension = extension,
            Branch = DisplayHelpers.BranchFor(extension),
            OutgoingCalls = own.Count(c => c.Source == extension),
            IncomingCalls = own.Count(c => c.Destination == extension),
            MonthCalls = billed.Count,
            MonthSubtotal = billed.Sum(c => c.Subtotal),
            MonthTax = billed.Sum(c => c.Tax),
            AccumulatedCost = billed.Sum(c => c.Total),
            DaysUntilBilling = DateTime.DaysInMonth(now.Year, now.Month) - now.Day,
            ShowsAllExtensions = central,
            PendingInvoices = pending.Count(),
            RecentCalls = own.OrderByDescending(c => c.StartedAt).Take(8).ToList(),
            ByExtension = byExtension
        };
    }
}
