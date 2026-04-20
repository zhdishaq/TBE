using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TBE.Contracts.Events;
using TBE.CrmService.Application.Projections;
using TBE.CrmService.Infrastructure;

namespace TBE.CrmService.Infrastructure.Consumers;

/// <summary>
/// Plan 06-04 Task 3 / COMP-03 / D-57 — applies a GDPR erasure on the
/// CRM projection side:
/// <list type="number">
///   <item>Writes a <see cref="CustomerErasureTombstoneRow"/> keyed on
///         <c>EmailHash</c> (dedup; UNIQUE at DB level).</item>
///   <item>NULLs the PII columns on <see cref="CustomerProjection"/>
///         (<c>Email</c> / <c>Name</c> / <c>Phone</c>) and flips
///         <c>IsErased=true</c> + <c>ErasedAt</c>. Keeps the row so
///         downstream booking history tabs still have something to
///         join against (D-57 — row survives, PII does not).</item>
///   <item>Publishes <see cref="CustomerErased"/> for downstream
///         observers (audit / alerting) — the write path does not
///         depend on any subscriber.</item>
/// </list>
/// <para>
/// Idempotent via a defensive <c>AnyAsync(EmailHash)</c> guard — a replay
/// with the same <c>RequestId</c> is dropped by MassTransit's
/// <c>InboxState</c> dedup before we ever get here; a replay with a
/// DIFFERENT <c>RequestId</c> but the SAME <c>EmailHash</c> (e.g. the
/// same person re-registers and requests another erasure) is detected
/// by this guard and treated as a no-op. The DB-level UNIQUE index is
/// the belt-and-braces backup.
/// </para>
/// <para>
/// Namespace kept as <c>TBE.CrmService.Infrastructure.Consumers</c> —
/// same convention as the other six CRM consumers in this folder (file
/// physically under <c>CrmService.Application/Consumers/</c> to mirror
/// the plan manifest; the class still needs <see cref="CrmDbContext"/>
/// which is in <c>.Infrastructure</c>, so it sits in the Infrastructure
/// namespace to keep the dependency direction clean).
/// </para>
/// </summary>
public sealed class CustomerErasureRequestedConsumer : IConsumer<CustomerErasureRequested>
{
    private readonly CrmDbContext _db;
    private readonly ILogger<CustomerErasureRequestedConsumer> _log;

    public CustomerErasureRequestedConsumer(
        CrmDbContext db,
        ILogger<CustomerErasureRequestedConsumer> log)
    {
        _db = db;
        _log = log;
    }

    public async Task Consume(ConsumeContext<CustomerErasureRequested> ctx)
    {
        var m = ctx.Message;
        var ct = ctx.CancellationToken;

        // Dedup guard — same EmailHash means we already processed an
        // erasure for this person (either this request on a retry path
        // or a prior erasure). D-57 policy: one tombstone per email.
        var exists = await _db.CustomerErasureTombstones
            .AnyAsync(t => t.EmailHash == m.EmailHash, ct);
        if (exists)
        {
            _log.LogInformation(
                "crm erasure skipped — tombstone already exists hash={EmailHash} request={RequestId}",
                m.EmailHash, m.RequestId);
            return;
        }

        _db.CustomerErasureTombstones.Add(new CustomerErasureTombstoneRow
        {
            Id = Guid.NewGuid(),
            OriginalCustomerId = m.CustomerId,
            EmailHash = m.EmailHash,
            ErasedAt = m.At,
            ErasedBy = m.RequestedBy,
            Reason = m.Reason,
        });

        // Null PII on the projection. The row survives — IsErased=true
        // drives the client-side "Anonymised Customer" banner and the
        // booking history tab can still render on the customer id.
        var cust = await _db.Customers.FirstOrDefaultAsync(c => c.Id == m.CustomerId, ct);
        if (cust is not null)
        {
            cust.Email = null;
            cust.Name = null;
            cust.Phone = null;
            cust.IsErased = true;
            cust.ErasedAt = m.At;
        }
        else
        {
            // Race with UserRegistered: erasure requested before the
            // seed event lands. We still write the tombstone — the
            // tombstone IS the audit record that "we received the
            // request" and, by UNIQUE(EmailHash), prevents a later
            // UserRegistered for the same email from re-seeding the
            // same customer through the usual flow without bumping
            // into the erasure check (handled in the register route).
            _log.LogWarning(
                "crm erasure: no Customer row for id={CustomerId} hash={EmailHash} — tombstone still written",
                m.CustomerId, m.EmailHash);
        }

        await _db.SaveChangesAsync(ct);

        // Publish observer event — downstream subscribers are async and
        // don't block the erasure write path.
        await ctx.Publish(new CustomerErased(
            CustomerId: m.CustomerId,
            EmailHash: m.EmailHash,
            ErasedAt: m.At));

        _log.LogInformation(
            "crm erasure applied customer={CustomerId} hash={EmailHash} by={Actor}",
            m.CustomerId, m.EmailHash, m.RequestedBy);
    }
}
