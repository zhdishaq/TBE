using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TBE.BookingService.Application.Baskets;

namespace TBE.BookingService.Infrastructure.Configurations;

/// <summary>
/// EF Core mapping for <see cref="Basket"/> (Plan 04-04 Task 1). Lives in the dedicated
/// <c>Booking</c> schema next to <c>HotelBookingSagaState</c> per 04-PATTERNS §BasketMap.
/// Applies the locked conventions:
/// <list type="bullet">
///   <item>Money columns — <c>decimal(18,4)</c>.</item>
///   <item>Currency — 3-char ISO.</item>
///   <item>Version — <c>IsRowVersion()</c> optimistic concurrency token.</item>
///   <item>StripePaymentIntentId — single nullable column (D-08 — never FlightPaymentIntentId / HotelPaymentIntentId).</item>
///   <item>Indexes on UserId, Status, FlightBookingId, HotelBookingId for dashboard + saga correlation lookups.</item>
/// </list>
/// </summary>
public class BasketMap : IEntityTypeConfiguration<Basket>
{
    public void Configure(EntityTypeBuilder<Basket> b)
    {
        b.ToTable("Basket", "Booking");
        b.HasKey(x => x.BasketId);
        b.Property(x => x.Version).IsRowVersion();

        b.Property(x => x.UserId).HasMaxLength(64).IsRequired();
        b.Property(x => x.Status).HasMaxLength(20).IsRequired();
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();

        b.Property(x => x.TotalAmount).HasColumnType("decimal(18,4)");
        b.Property(x => x.FlightSubtotal).HasColumnType("decimal(18,4)");
        b.Property(x => x.HotelSubtotal).HasColumnType("decimal(18,4)");
        b.Property(x => x.ChargedAmount).HasColumnType("decimal(18,4)");
        b.Property(x => x.RefundedAmount).HasColumnType("decimal(18,4)");

        // D-08: SINGLE PaymentIntent per basket — one column, no per-leg split.
        b.Property(x => x.StripePaymentIntentId).HasMaxLength(50);

        b.Property(x => x.GuestEmail).HasMaxLength(256).IsRequired();
        b.Property(x => x.GuestFullName).HasMaxLength(200).IsRequired();

        b.HasIndex(x => x.UserId);
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.FlightBookingId);
        b.HasIndex(x => x.HotelBookingId);
    }
}
