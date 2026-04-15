using MassTransit;
using Microsoft.EntityFrameworkCore;
using TBE.PaymentService.Infrastructure.Configurations;
using TBE.PaymentService.Infrastructure.Stripe;
using TBE.PaymentService.Infrastructure.Wallet;

namespace TBE.PaymentService.Infrastructure;

public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options)
    {
    }

    public DbSet<StripeWebhookEvent> StripeWebhookEvents => Set<StripeWebhookEvent>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // MassTransit outbox tables
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        modelBuilder.ApplyConfiguration(new StripeWebhookEventMap());
        modelBuilder.ApplyConfiguration(new WalletTransactionMap());
    }
}
