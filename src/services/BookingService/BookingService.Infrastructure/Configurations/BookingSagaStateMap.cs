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
        // Plan 05-02 Task 2 — map the renamed string field back to the
        // existing "Channel" column (Phase-3 compat; no table rename needed)
        // and add a new int-backed ChannelKind column for the typed enum used
        // by the B2B IfElse branch.
        b.Property(x => x.ChannelText).HasColumnName("Channel").HasMaxLength(5).IsRequired();
        b.Property(x => x.Channel).HasColumnName("ChannelKind").HasConversion<int>().HasDefaultValue(TBE.Contracts.Enums.Channel.B2C);
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

        // Plan 05-02 Task 2 — B2B agency pricing + customer-contact snapshot.
        b.Property(x => x.AgencyNetFare).HasColumnType("decimal(18,4)");
        b.Property(x => x.AgencyMarkupAmount).HasColumnType("decimal(18,4)");
        b.Property(x => x.AgencyGrossAmount).HasColumnType("decimal(18,4)");
        b.Property(x => x.AgencyCommissionAmount).HasColumnType("decimal(18,4)");
        b.Property(x => x.AgencyMarkupOverride).HasColumnType("decimal(18,4)");
        b.Property(x => x.CustomerName).HasMaxLength(200);
        b.Property(x => x.CustomerEmail).HasMaxLength(320);
        b.Property(x => x.CustomerPhone).HasMaxLength(32);
        b.Property(x => x.FailureReason).HasMaxLength(64);

        b.Property(x => x.Warn24HSent).HasDefaultValue(false);
        b.Property(x => x.Warn2HSent).HasDefaultValue(false);

        // Plan 06-01 Task 5 — BO-03 staff cancel/modify metadata. Column
        // constraints live on the migration (AddCancellationColumns) — EF
        // Core check constraints are best-effort in 9.0; the SQL CHECK is
        // authoritative.
        b.Property(x => x.CancelledByStaff).HasDefaultValue(false);
        b.Property(x => x.CancellationReasonCode).HasMaxLength(64);
        b.Property(x => x.CancellationReason).HasMaxLength(500);
        b.Property(x => x.CancellationRequestedBy).HasMaxLength(128);
        b.Property(x => x.CancellationApprovedBy).HasMaxLength(128);

        b.HasIndex(x => x.TicketingDeadlineUtc);
        // D-34 — agency-wide booking list query hits this index.
        b.HasIndex(x => x.AgencyId).HasDatabaseName("IX_BookingSagaState_AgencyId");
        b.HasIndex(x => x.Channel).HasDatabaseName("IX_BookingSagaState_Channel");
    }
}
