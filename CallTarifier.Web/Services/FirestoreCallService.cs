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
        var calls = await GetCallsAsync(new CallFilter { Extension = extension }, cancellationToken);
        var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

        var db = await _dbFactory.GetDbAsync(cancellationToken);
        var pendingSnap = await db.Collection("invoices")
            .WhereEqualTo("status", InvoiceStatus.Pending)
            .GetSnapshotAsync(cancellationToken);

        return new DashboardSummary
        {
            UserName = string.Empty,
            Extension = extension,
            Branch = DisplayHelpers.BranchFor(extension),
            OutgoingCalls = calls.Count(c => c.Source == extension),
            IncomingCalls = calls.Count(c => c.Destination == extension),
            MonthCalls = calls.Count(c => c.StartedAt >= monthStart),
            AccumulatedCost = calls
                .Where(c => c.Source == extension && c.Status == CallStatus.Completed)
                .Sum(c => c.Total),
            PendingInvoices = pendingSnap.Count,
            RecentCalls = calls.OrderByDescending(c => c.StartedAt).Take(5).ToList()
        };
    }
}
