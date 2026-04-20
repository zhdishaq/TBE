using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TBE.Contracts.Events;

namespace TBE.BookingService.Infrastructure.Consumers;

/// <summary>
/// Plan 06-04 Task 3 / COMP-03 / D-57 — BookingService-side GDPR erasure
/// fan-out. Subscribes to <see cref="CustomerErasureRequested"/> alongside
/// the CRM consumer (different queue, same event) and NULLs the customer
/// contact snapshot on every <c>Saga.BookingSagaState</c> row that belongs
/// to the erased customer.
///
/// <para>
/// Columns NULLed: <c>CustomerName</c>, <c>CustomerEmail</c>,
/// <c>CustomerPhone</c>. The plan outline also mentions
/// <c>PassportNumber</c> / <c>DateOfBirth</c>, but those columns do NOT
/// exist on <see cref="TBE.BookingService.Application.Saga.BookingSagaState"/>
/// — passenger-level PII lives inside the opaque <c>ItineraryJson</c>
/// blob (added in Plan 06-02 for manual bookings). Passport / DOB erasure
/// inside the JSON payload is deliberately scoped out here and documented
/// as a deviation in the Plan 06-04 SUMMARY; a follow-up plan will add a
/// JSON-walk step once the itinerary schema is stabilised.
/// </para>
///
/// <para>
/// <strong>D-49 preserved:</strong> <c>BookingEvents</c> is append-only
/// and immutable. This consumer MUST NOT touch
/// <see cref="TBE.BookingService.Infrastructure.BookingEventsDbContext"/>
/// — the event log retains its historical accuracy; the PII inside it is
/// the same customer-contact snapshot that was written at booking time,
/// and we treat the log itself as an audit artefact that is exempt from
/// erasure (backed by <c>BookingEventsDbContext.AppendOnlyRoleGrant</c>
/// which forbids UPDATE/DELETE at the SQL grant level).
/// </para>
///
/// <para>
/// Uses <see cref="RelationalQueryableExtensions.ExecuteUpdateAsync"/> so
/// the NULL-out happens as a single set-based UPDATE with no
/// change-tracker materialisation — important because an ops-admin
/// erasing a high-volume agency contact could otherwise pull thousands
/// of saga rows into memory.
/// </para>
///
/// <para>
/// Idempotent — replaying the same event is cheap: subsequent runs
/// update rows whose <c>CustomerName</c> / <c>CustomerEmail</c> /
/// <c>CustomerPhone</c> are already NULL, yielding affected-row-count
/// zero without any logical effect.
/// </para>
/// </summary>
public sealed class CustomerErasureRequestedConsumer : IConsumer<CustomerErasureRequested>
{
    private readonly BookingDbContext _db;
    private readonly ILogger<CustomerErasureRequestedConsumer> _log;

    public CustomerErasureRequestedConsumer(
        BookingDbContext db,
        ILogger<CustomerErasureRequestedConsumer> log)
    {
        _db = db;
        _log = log;
    }

    public async Task Consume(ConsumeContext<CustomerErasureRequested> ctx)
    {
        var m = ctx.Message;
        var ct = ctx.CancellationToken;

        // Backed by IX_BookingSagaState_CustomerId filtered WHERE
        // CustomerId IS NOT NULL (migration 20260604100000). Set-based
        // UPDATE — the change tracker never materialises the rows.
        var affected = await _db.BookingSagaStates
            .Where(s => s.CustomerId == m.CustomerId)
            .ExecuteUpdateAsync(set => set
                .SetProperty(s => s.CustomerName, (string?)null)
                .SetProperty(s => s.CustomerEmail, (string?)null)
                .SetProperty(s => s.CustomerPhone, (string?)null),
                ct);

        _log.LogInformation(
            "booking erasure applied customer={CustomerId} hash={EmailHash} rows={Affected} by={Actor}",
            m.CustomerId, m.EmailHash, affected, m.RequestedBy);

        // D-49: BookingEvents is append-only; no UPDATE is issued against
        // the event log. The CustomerErased observer event is published
        // by the CRM-side consumer (single publisher per event type to
        // avoid duplicate downstream fan-out).
    }
}
