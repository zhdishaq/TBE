using Microsoft.EntityFrameworkCore;
using TBE.BookingService.Application.Saga;

namespace TBE.BookingService.Infrastructure;

/// <summary>
/// Plan 06-01 Task 5 — writer-only DbContext for the append-only
/// <c>dbo.BookingEvents</c> audit table.
///
/// Pitfall 1 (EF1): the saga persistence pipeline must never be able to
/// trigger an UPDATE against a row we DENY'd. By keeping this context
/// writer-only (single <c>DbSet&lt;BookingEvent&gt;</c>, no saga state,
/// no outbox/inbox maps) and registering it under a distinct connection
/// string that maps to the <c>booking_events_writer</c> role, the only
/// permitted SQL is INSERT / SELECT. Any attempt to attach-and-update a
/// BookingEvent will fail cleanly at the engine (SqlException 229).
///
/// PATTERNS.md Pattern F: the ChangeTracker behaviour is
/// <c>QueryTrackingBehavior.NoTracking</c> — every Add is a fresh insert
/// (writer only), and reads (rare; audit log viewer) never bring rows
/// back into the tracker.
/// </summary>
public sealed class BookingEventsDbContext : DbContext
{
    public BookingEventsDbContext(DbContextOptions<BookingEventsDbContext> options)
        : base(options)
    {
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    public DbSet<BookingEvent> Events => Set<BookingEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var e = modelBuilder.Entity<BookingEvent>();
        e.ToTable("BookingEvents", "dbo");
        e.HasKey(x => x.EventId);
        e.Property(x => x.EventId).ValueGeneratedNever();
        e.Property(x => x.BookingId).IsRequired();
        e.Property(x => x.EventType).HasMaxLength(64).IsRequired();
        e.Property(x => x.OccurredAt).IsRequired();
        e.Property(x => x.Actor).HasMaxLength(128).IsRequired();
        e.Property(x => x.CorrelationId).IsRequired();
        e.Property(x => x.Snapshot).HasColumnType("nvarchar(max)").IsRequired();

        e.HasIndex(x => x.BookingId).HasDatabaseName("IX_BookingEvents_BookingId");
        e.HasIndex(x => new { x.BookingId, x.OccurredAt })
            .HasDatabaseName("IX_BookingEvents_BookingId_OccurredAt");
    }
}
