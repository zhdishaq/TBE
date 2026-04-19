using MassTransit;
using Microsoft.EntityFrameworkCore;
using TBE.PaymentService.Application.Wallet;
using TBE.PaymentService.Infrastructure.Configurations;
using TBE.PaymentService.Infrastructure.Reconciliation;
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

    /// <summary>
    /// Plan 06-02 Task 3 (BO-06) — reconciliation discrepancies queue.
    /// Populated nightly by <c>PaymentReconciliationService</c>; worked
    /// down by ops-finance via the portal.
    /// </summary>
    public DbSet<PaymentReconciliationItem> ReconciliationQueue => Set<PaymentReconciliationItem>();

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

        modelBuilder.Entity<PaymentReconciliationItem>(e =>
        {
            e.ToTable("PaymentReconciliationQueue", "payment");
            e.HasKey(x => x.Id);
            e.Property(x => x.DiscrepancyType).HasMaxLength(32).IsRequired();
            e.Property(x => x.Severity).HasMaxLength(16).IsRequired();
            e.Property(x => x.StripeEventId).HasMaxLength(100);
            e.Property(x => x.Details).HasColumnType("nvarchar(max)").IsRequired();
            e.Property(x => x.Status).HasMaxLength(16).IsRequired().HasDefaultValue("Pending");
            e.Property(x => x.ResolvedBy).HasMaxLength(128);
            e.Property(x => x.ResolutionNotes).HasMaxLength(2000);
            // List hot path: pending items by DetectedAtUtc DESC.
            e.HasIndex(x => new { x.Status, x.DetectedAtUtc })
                .IsDescending(false, true)
                .HasDatabaseName("IX_PaymentReconciliationQueue_Status_DetectedAt");
            // Idempotent rescans: avoid duplicate OrphanStripeEvent / AmountDrift
            // rows for the same StripeEventId.
            e.HasIndex(x => new { x.DiscrepancyType, x.StripeEventId })
                .HasFilter("[StripeEventId] IS NOT NULL")
                .HasDatabaseName("IX_PaymentReconciliationQueue_Type_StripeEventId");
            e.HasIndex(x => new { x.DiscrepancyType, x.BookingId })
                .HasFilter("[BookingId] IS NOT NULL")
                .HasDatabaseName("IX_PaymentReconciliationQueue_Type_BookingId");
            e.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_PaymentReconciliationQueue_DiscrepancyType",
                    "[DiscrepancyType] IN ('OrphanStripeEvent','OrphanWalletRow','AmountDrift','UnprocessedEvent')");
                t.HasCheckConstraint(
                    "CK_PaymentReconciliationQueue_Severity",
                    "[Severity] IN ('Low','Medium','High')");
                t.HasCheckConstraint(
                    "CK_PaymentReconciliationQueue_Status",
                    "[Status] IN ('Pending','Resolved')");
            });
        });
    }
}
