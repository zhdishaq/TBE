using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TBE.BookingService.Application.Saga;

namespace TBE.BookingService.Infrastructure.Configurations;

/// <summary>
/// EF Core mapping for <see cref="BookingSagaState"/>. Table lives in the dedicated
/// <c>Saga</c> schema per D-01 to isolate infrastructure state from the domain schema.
/// Version is mapped as a row-version concurrency token to enforce D-01 optimistic locking.
/// </summary>
public class BookingSagaStateMap : IEntityTypeConfiguration<BookingSagaState>
{
    public void Configure(EntityTypeBuilder<BookingSagaState> b)
    {
        b.ToTable("BookingSagaState", "Saga");
        b.HasKey(x => x.CorrelationId);
        b.Property(x => x.Version).IsRowVersion();

        b.Property(x => x.BookingReference).HasMaxLength(32).IsRequired();
        b.Property(x => x.ProductType).HasMaxLength(10).IsRequired();
        b.Property(x => x.Channel).HasMaxLength(5).IsRequired();
        b.Property(x => x.UserId).HasMaxLength(64).IsRequired();
        b.Property(x => x.TotalAmount).HasColumnType("decimal(18,4)");
        b.Property(x => x.BaseFareAmount).HasColumnType("decimal(18,4)");
        b.Property(x => x.SurchargeAmount).HasColumnType("decimal(18,4)");
        b.Property(x => x.TaxAmount).HasColumnType("decimal(18,4)");
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.PaymentMethod).HasMaxLength(10).IsRequired();
        b.Property(x => x.OfferToken).HasMaxLength(200);
        b.Property(x => x.GdsPnr).HasMaxLength(12);
        b.Property(x => x.StripePaymentIntentId).HasMaxLength(50);
        b.Property(x => x.TicketNumber).HasMaxLength(32);
        b.Property(x => x.LastSuccessfulStep).HasMaxLength(32);

        b.Property(x => x.Warn24HSent).HasDefaultValue(false);
        b.Property(x => x.Warn2HSent).HasDefaultValue(false);

        b.HasIndex(x => x.TicketingDeadlineUtc);
    }
}
