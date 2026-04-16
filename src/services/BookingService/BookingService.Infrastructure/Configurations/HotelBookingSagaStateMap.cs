using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TBE.BookingService.Application.Saga;

namespace TBE.BookingService.Infrastructure.Configurations;

/// <summary>
/// EF Core mapping for <see cref="HotelBookingSagaState"/>. Table lives in the
/// dedicated <c>Booking</c> schema (distinct from the Flight Saga schema) per
/// 04-PATTERNS §BasketMap — hotel aggregates are domain-owned while flight
/// sagas are state-machine-infrastructure. Version mapped as a row-version
/// concurrency token (D-01 optimistic locking parity).
/// </summary>
public class HotelBookingSagaStateMap : IEntityTypeConfiguration<HotelBookingSagaState>
{
    public void Configure(EntityTypeBuilder<HotelBookingSagaState> b)
    {
        b.ToTable("HotelBookingSagaState", "Booking");
        b.HasKey(x => x.CorrelationId);
        b.Property(x => x.Version).IsRowVersion();

        b.Property(x => x.UserId).HasMaxLength(64).IsRequired();
        b.Property(x => x.BookingReference).HasMaxLength(32).IsRequired();
        b.Property(x => x.SupplierRef).HasMaxLength(64);
        b.Property(x => x.PropertyName).HasMaxLength(200).IsRequired();
        b.Property(x => x.AddressLine).HasMaxLength(400).IsRequired();

        b.Property(x => x.TotalAmount).HasColumnType("decimal(18,4)");
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();

        b.Property(x => x.GuestEmail).HasMaxLength(256).IsRequired();
        b.Property(x => x.GuestFullName).HasMaxLength(200).IsRequired();

        b.Property(x => x.Status).HasMaxLength(20).IsRequired();
        b.Property(x => x.FailureCause).HasMaxLength(500);
        b.Property(x => x.StripePaymentIntentId).HasMaxLength(50);

        // HOTB-05 — supplier_ref is the hotel dashboard's fast-lookup key.
        b.HasIndex(x => x.SupplierRef);
        // Fast customer-scoped listing for /customers/me/hotel-bookings.
        b.HasIndex(x => x.UserId);
        // Dashboard status filters + saga projections.
        b.HasIndex(x => x.Status);
    }
}
