using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TBE.Contracts.Events;
using TBE.CrmService.Application.Projections;

namespace TBE.CrmService.Infrastructure.Consumers;

/// <summary>
/// Plan 06-04 Task 1 — seed <see cref="CustomerProjection"/> on
/// <see cref="UserRegistered"/> fan-out. Idempotent via MT
/// <c>InboxState</c>; a second delivery with the same MessageId is a
/// no-op.
/// </summary>
public sealed class UserRegisteredConsumer : IConsumer<UserRegistered>
{
    private readonly CrmDbContext _db;
    private readonly ILogger<UserRegisteredConsumer> _log;

    public UserRegisteredConsumer(CrmDbContext db, ILogger<UserRegisteredConsumer> log)
    {
        _db = db;
        _log = log;
    }

    public async Task Consume(ConsumeContext<UserRegistered> ctx)
    {
        var m = ctx.Message;
        var ct = ctx.CancellationToken;

        var existing = await _db.Customers.FirstOrDefaultAsync(c => c.Id == m.UserId, ct);
        if (existing is not null)
        {
            _log.LogInformation("crm customer already seeded user={UserId}", m.UserId);
            return;
        }

        _db.Customers.Add(new CustomerProjection
        {
            Id = m.UserId,
            Email = m.Email,
            Name = m.Name,
            CreatedAt = m.At,
            IsErased = false,
            LifetimeBookingsCount = 0,
            LifetimeGross = 0m,
        });

        await _db.SaveChangesAsync(ct);
        _log.LogInformation("crm customer seeded user={UserId} email={Email}", m.UserId, m.Email);
    }
}
