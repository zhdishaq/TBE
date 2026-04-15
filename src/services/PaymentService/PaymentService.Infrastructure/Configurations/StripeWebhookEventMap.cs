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
    }
}
