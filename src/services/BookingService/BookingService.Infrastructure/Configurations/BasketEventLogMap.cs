using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TBE.BookingService.Application.Baskets;

namespace TBE.BookingService.Infrastructure.Configurations;

/// <summary>
/// EF Core mapping for <see cref="BasketEventLog"/> — the per-basket inbox table that
/// backs <see cref="BasketPaymentOrchestrator"/> idempotency (T-04-04-04). Unique index
/// on <c>(BasketId, EventId)</c> is the authoritative duplicate-detection mechanism.
/// </summary>
public class BasketEventLogMap : IEntityTypeConfiguration<BasketEventLog>
{
    public void Configure(EntityTypeBuilder<BasketEventLog> b)
    {
        b.ToTable("BasketEventLog", "Booking");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();

        b.Property(x => x.EventType).HasMaxLength(64).IsRequired();

        b.HasIndex(x => new { x.BasketId, x.EventId }).IsUnique();
    }
}
