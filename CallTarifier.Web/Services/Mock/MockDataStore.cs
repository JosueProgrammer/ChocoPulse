using CallTarifier.Web.Models;
using CallTarifier.Web.Models.Enums;

namespace CallTarifier.Web.Services.Mock;

/// <summary>
/// In-memory seed data used when Application:UseMockData is true.
/// </summary>
public sealed class MockDataStore
{
    private static readonly Lazy<MockDataStore> InstanceFactory = new(() => new MockDataStore());

    public static MockDataStore Instance => InstanceFactory.Value;

    public AppUser CurrentUser { get; }

    public IReadOnlyList<ExtensionInfo> Extensions { get; }

    public List<Call> Calls { get; }

    public List<Invoice> Invoices { get; }

    public List<MonthlyBilling> MonthlyBillings { get; }

    private MockDataStore()
    {
        CurrentUser = new AppUser
        {
            Uid = "mock-user-001",
            Name = "Juan Pérez",
            Email = "juan.perez@chocopulse.local",
            Extension = "4000",
            Branch = "Carazo",
            Role = UserRole.Admin
        };

        Extensions =
        [
            new ExtensionInfo { Extension = "4000", Branch = "Carazo", Status = "Disponible" },
            new ExtensionInfo { Extension = "4021", Branch = "Masaya", Status = "Disponible" },
            new ExtensionInfo { Extension = "4041", Branch = "Rivas", Status = "Disponible" },
            new ExtensionInfo { Extension = "4061", Branch = "Granada", Status = "Disponible" },
            new ExtensionInfo { Extension = "4101", Branch = "León", Status = "Disponible" },
            new ExtensionInfo { Extension = "4121", Branch = "Boaco", Status = "Disponible" },
            new ExtensionInfo { Extension = "4141", Branch = "Managua", Status = "Disponible" }
        ];

        Calls =
        [
            CreateCall("call-001", "4000", "4021", CallStatus.Completed, 150, "E1001", daysAgo: 0, hour: 20, minute: 30),
            CreateCall("call-002", "4000", "4061", CallStatus.Completed, 95, "E1002", daysAgo: 0, hour: 14, minute: 12),
            CreateCall("call-003", "4021", "4000", CallStatus.Missed, 0, null, daysAgo: 1, hour: 9, minute: 5),
            CreateCall("call-004", "4000", "4101", CallStatus.Completed, 210, "E1003", daysAgo: 2, hour: 11, minute: 40),
            CreateCall("call-005", "4000", "4141", CallStatus.Busy, 0, null, daysAgo: 3, hour: 16, minute: 22),
            CreateCall("call-006", "4041", "4000", CallStatus.Rejected, 0, null, daysAgo: 4, hour: 10, minute: 0),
            CreateCall("call-007", "4000", "4121", CallStatus.Completed, 60, "E1004", daysAgo: 5, hour: 15, minute: 18),
            CreateCall("call-008", "4000", "4021", CallStatus.Failed, 0, null, daysAgo: 6, hour: 8, minute: 45)
        ];

        Invoices =
        [
            CreateInvoice("inv-001", "E1001", Calls[0], InvoiceStatus.Pending),
            CreateInvoice("inv-002", "E1002", Calls[1], InvoiceStatus.Paid),
            CreateInvoice("inv-003", "E1003", Calls[3], InvoiceStatus.Pending),
            CreateInvoice("inv-004", "E1004", Calls[6], InvoiceStatus.Paid)
        ];

        var now = DateTime.Now;
        MonthlyBillings =
        [
            new MonthlyBilling
            {
                Id = "mb-001",
                Extension = "4000",
                Year = now.Year,
                Month = now.Month,
                CallCount = 4,
                Subtotal = 26.00m,
                Tax = 3.90m,
                Total = 29.90m,
                Status = InvoiceStatus.Pending,
                CreatedAt = new DateTime(now.Year, now.Month, 1)
            }
        ];
    }

    private static Call CreateCall(
        string id,
        string source,
        string destination,
        string status,
        int durationSeconds,
        string? invoiceId,
        int daysAgo,
        int hour,
        int minute)
    {
        var started = DateTime.Today.AddDays(-daysAgo).AddHours(hour).AddMinutes(minute);
        DateTime? answered = durationSeconds > 0 ? started.AddSeconds(5) : null;
        DateTime? ended = answered?.AddSeconds(durationSeconds) ?? started.AddSeconds(20);

        decimal ringCharge = 0m;
        decimal callCharge = 0m;
        decimal subtotal = 0m;
        decimal tax = 0m;
        decimal total = 0m;

        if (status == CallStatus.Completed && durationSeconds > 0)
        {
            ringCharge = 0.50m;
            callCharge = Math.Round(durationSeconds * 0.05m, 2, MidpointRounding.AwayFromZero);
            subtotal = Math.Round(ringCharge + callCharge, 2, MidpointRounding.AwayFromZero);
            tax = Math.Round(subtotal * 0.15m, 2, MidpointRounding.AwayFromZero);
            total = Math.Round(subtotal + tax, 2, MidpointRounding.AwayFromZero);
        }

        return new Call
        {
            Id = id,
            Source = source,
            Destination = destination,
            StartedAt = started,
            RingingAt = started.AddSeconds(1),
            AnsweredAt = answered,
            EndedAt = ended,
            Status = status,
            DurationSeconds = durationSeconds,
            RingCharge = ringCharge,
            CallCharge = callCharge,
            Subtotal = subtotal,
            Tax = tax,
            Total = total,
            InvoiceId = invoiceId,
            UniqueId = $"mock-{id}",
            LinkedId = $"mock-linked-{id}",
            CreatedAt = started
        };
    }

    private static Invoice CreateInvoice(string id, string number, Call call, string status)
    {
        return new Invoice
        {
            Id = id,
            Number = number,
            CallId = call.Id,
            Source = call.Source,
            Destination = call.Destination,
            DurationSeconds = call.DurationSeconds,
            RingCharge = call.RingCharge,
            CallCharge = call.CallCharge,
            Subtotal = call.Subtotal,
            Tax = call.Tax,
            Total = call.Total,
            Status = status,
            IssuedAt = call.EndedAt ?? call.StartedAt,
            PaidAt = status == InvoiceStatus.Paid ? (call.EndedAt ?? call.StartedAt).AddDays(1) : null,
            CreatedAt = call.CreatedAt
        };
    }
}
