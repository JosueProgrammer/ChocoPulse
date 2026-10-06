using CallTarifier.Web.Models;
using CallTarifier.Web.Models.Enums;

namespace CallTarifier.Web.Services.Mock;

public sealed class MockCallService : ICallService
{
    private readonly MockDataStore _store;
    private readonly ILogger<MockCallService> _logger;

    public MockCallService(MockDataStore store, ILogger<MockCallService> logger)
    {
        _store = store;
        _logger = logger;
    }

    public Task<IReadOnlyList<Call>> GetCallsAsync(CallFilter? filter = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IEnumerable<Call> query = _store.Calls.OrderByDescending(c => c.StartedAt);

        if (filter is not null)
        {
            if (filter.From.HasValue)
            {
                query = query.Where(c => c.StartedAt >= filter.From.Value);
            }

            if (filter.To.HasValue)
            {
                query = query.Where(c => c.StartedAt <= filter.To.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.Extension))
            {
                query = query.Where(c =>
                    c.Source == filter.Extension || c.Destination == filter.Extension);
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
                    (c.InvoiceId?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
            }
        }

        IReadOnlyList<Call> result = query.ToList();
        _logger.LogDebug("Mock call query returned {Count} records", result.Count);
        return Task.FromResult(result);
    }

    public Task<Call?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var call = _store.Calls.FirstOrDefault(c => c.Id == id);
        return Task.FromResult(call);
    }

    public Task<DashboardSummary> GetDashboardAsync(string extension, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = _store.CurrentUser;
        var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        var related = _store.Calls
            .Where(c => c.Source == extension || c.Destination == extension)
            .ToList();

        var summary = new DashboardSummary
        {
            UserName = user.Name,
            Extension = user.Extension,
            Branch = user.Branch,
            OutgoingCalls = related.Count(c => c.Source == extension),
            IncomingCalls = related.Count(c => c.Destination == extension),
            MonthCalls = related.Count(c => c.StartedAt >= monthStart),
            AccumulatedCost = related
                .Where(c => c.Source == extension && c.Status == CallStatus.Completed)
                .Sum(c => c.Total),
            PendingInvoices = _store.Invoices.Count(i => i.Status == InvoiceStatus.Pending),
            RecentCalls = related.OrderByDescending(c => c.StartedAt).Take(5).ToList()
        };

        return Task.FromResult(summary);
    }
}
