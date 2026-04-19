using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TBE.PaymentService.Infrastructure.Stripe;

namespace TBE.PaymentService.Infrastructure.Configurations;

public sealed class StripeWebhookEventMap : IEntityTypeConfiguration<StripeWebhookEvent>
{
    public void Configure(EntityTypeBuilder<StripeWebhookEvent> b)
    {
        b.ToTable("StripeWebhookEvents", "payment");
        b.HasKey(x => x.EventId);
        b.Property(x => x.EventId).HasMaxLength(100);
        b.Property(x => x.EventType).HasMaxLength(100).IsRequired();
        b.Property(x => x.ReceivedAtUtc).IsRequired();
        // Plan 06-02 Task 3 (BO-06 / D-55) — full Stripe event body
        // persisted for reconciliation; Processed flag guarded by SLA.
        b.Property(x => x.RawPayload).HasColumnType("nvarchar(max)").IsRequired();
        b.Property(x => x.Processed).HasDefaultValue(false);
        // Reconciliation hot path: find unprocessed events older than 1h.
        b.HasIndex(x => new { x.Processed, x.ReceivedAtUtc })
            .HasDatabaseName("IX_StripeWebhookEvents_Processed_ReceivedAt");
    }
}
