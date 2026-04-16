using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TBE.BookingService.Application.Cars;

namespace TBE.BookingService.Infrastructure.Configurations;

/// <summary>
/// EF Core mapping for <see cref="CarBooking"/>. Dedicated <c>Booking</c> schema to match
/// the other booking aggregates. Money is <c>decimal(18,4)</c>; Currency capped to 3;
/// SupplierRef capped at 64 (typical supplier confirmation code length).
/// </summary>
public sealed class CarBookingMap : IEntityTypeConfiguration<CarBooking>
{
    public void Configure(EntityTypeBuilder<CarBooking> b)
    {
        b.ToTable("CarBooking", "Booking");
        b.HasKey(x => x.BookingId);

        b.Property(x => x.UserId).HasMaxLength(64).IsRequired();
        b.Property(x => x.SupplierRef).HasMaxLength(64);
        b.Property(x => x.BookingReference).HasMaxLength(32).IsRequired();
        b.Property(x => x.VendorName).HasMaxLength(100).IsRequired();
        b.Property(x => x.PickupLocation).HasMaxLength(200).IsRequired();
        b.Property(x => x.DropoffLocation).HasMaxLength(200).IsRequired();
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.GuestEmail).HasMaxLength(256).IsRequired();
        b.Property(x => x.GuestFullName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Status).HasMaxLength(20).IsRequired();

        b.Property(x => x.TotalAmount).HasColumnType("decimal(18,4)");

        b.Property(x => x.Version).IsRowVersion();

        b.HasIndex(x => x.UserId);
        b.HasIndex(x => x.Status);
    }
}
