using MassTransit;
using Microsoft.EntityFrameworkCore;
using TBE.PaymentService.Application.Wallet;
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

    /// <summary>
    /// Plan 05-03 Task 2 — per-agency threshold + low-balance hysteresis state.
    /// The ledger itself is still <see cref="WalletTransactions"/>.
    /// </summary>
    public DbSet<AgencyWallet> AgencyWallets => Set<AgencyWallet>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // MassTransit outbox tables
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        modelBuilder.ApplyConfiguration(new StripeWebhookEventMap());
        modelBuilder.ApplyConfiguration(new WalletTransactionMap());
        modelBuilder.ApplyConfiguration(new AgencyWalletMap());
    }
}
