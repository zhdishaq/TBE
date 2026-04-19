using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TBE.Contracts.Events;
using TBE.CrmService.Application.Projections;

namespace TBE.CrmService.Infrastructure.Consumers;

/// <summary>
/// Plan 06-04 Task 1 — idempotent persistence of a communication-log
/// entry. The <c>CommunicationLogController</c> writes directly for the
/// first-party case (ops staff via portal); this consumer handles
/// cross-service log publishes from future sources (e.g., NotificationService
/// emitting a "customer contacted" note when an email bounces).
/// </summary>
public sealed class CustomerCommunicationLoggedConsumer : IConsumer<CustomerCommunicationLogged>
{
    private readonly CrmDbContext _db;
    private readonly ILogger<CustomerCommunicationLoggedConsumer> _log;

    public CustomerCommunicationLoggedConsumer(CrmDbContext db, ILogger<CustomerCommunicationLoggedConsumer> log)
    {
        _db = db;
        _log = log;
    }

    public async Task Consume(ConsumeContext<CustomerCommunicationLogged> ctx)
    {
        var m = ctx.Message;
        var ct = ctx.CancellationToken;

        var exists = await _db.CommunicationLog.AnyAsync(x => x.LogId == m.LogId, ct);
        if (exists)
        {
            _log.LogInformation("crm communication log already persisted id={LogId}", m.LogId);
            return;
        }

        _db.CommunicationLog.Add(new CommunicationLogRow
        {
            LogId = m.LogId,
            EntityType = m.EntityType,
            EntityId = m.EntityId,
            CreatedBy = m.CreatedBy,
            CreatedAt = m.At,
            Body = m.BodyMarkdown,
        });

        await _db.SaveChangesAsync(ct);
    }
}
