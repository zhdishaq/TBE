using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace TBE.BookingService.Infrastructure;

public class BookingDbContext : DbContext
{
    public BookingDbContext(DbContextOptions<BookingDbContext> options) : base(options)
    {
    }

    // Booking domain entities will be added in Phase 3
    // Phase 1: Only outbox tables are created here

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // MassTransit outbox tables
        // Creates: InboxState, OutboxMessage, OutboxState
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
