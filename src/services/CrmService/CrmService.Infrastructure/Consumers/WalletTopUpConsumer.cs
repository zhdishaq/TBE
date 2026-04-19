using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TBE.Contracts.Events;

namespace TBE.CrmService.Infrastructure.Consumers;

/// <summary>
/// Plan 06-04 Task 1 — observer consumer for <see cref="WalletToppedUp"/>.
/// CRM doesn't own wallet state (PaymentService is authoritative); this
/// consumer just bumps the agency's <c>LastBookingAt</c> heartbeat so
/// the Agency 360 page can show a "last activity" chip without an RPC
/// back to PaymentService.
/// </summary>
public sealed class WalletTopUpConsumer : IConsumer<WalletToppedUp>
{
    private readonly CrmDbContext _db;
    private readonly ILogger<WalletTopUpConsumer> _log;

    public WalletTopUpConsumer(CrmDbContext db, ILogger<WalletTopUpConsumer> log)
    {
        _db = db;
        _log = log;
    }

    public async Task Consume(ConsumeContext<WalletToppedUp> ctx)
    {
        var m = ctx.Message;
        var ct = ctx.CancellationToken;

        if (m.AgencyId is not Guid agencyId)
        {
            // B2C wallet top-ups have AgencyId == null; nothing to do.
            return;
        }

        var a = await _db.Agencies.FirstOrDefaultAsync(x => x.Id == agencyId, ct);
        if (a is null)
        {
            // No projection yet — leave for the first AgentBookingDetailsCaptured
            // or manual agency-create to materialise.
            _log.LogInformation("crm wallet top-up for unknown agency={AgencyId}; skipping projection", agencyId);
            return;
        }

        a.LastBookingAt = m.At.UtcDateTime;
        await _db.SaveChangesAsync(ct);
    }
}
