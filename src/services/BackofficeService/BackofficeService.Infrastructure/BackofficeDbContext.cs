using MassTransit;
using Microsoft.EntityFrameworkCore;
using TBE.BackofficeService.Application.Entities;

namespace TBE.BackofficeService.Infrastructure;

public class BackofficeDbContext : DbContext
{
    public BackofficeDbContext(DbContextOptions<BackofficeDbContext> options) : base(options)
    {
    }

    public DbSet<DeadLetterQueueRow> DeadLetterQueue => Set<DeadLetterQueueRow>();
    public DbSet<WalletCreditRequest> WalletCreditRequests => Set<WalletCreditRequest>();
    public DbSet<CancellationRequest> CancellationRequests => Set<CancellationRequest>();

    /// <summary>
    /// Plan 06-02 Task 2 (BO-07) — supplier negotiated-rate contracts.
    /// Soft-deleted rows (IsDeleted=true) are filtered by the controller
    /// List query, not a global query filter, so that ops-admin can still
    /// recover / audit them via Get-by-Id if needed.
    /// </summary>
    public DbSet<SupplierContract> SupplierContracts => Set<SupplierContract>();

    /// <summary>
    /// Plan 06-01 Task 7 (BO-01) — cross-schema read model of
    /// <c>Saga.BookingSagaState</c> owned by BookingService. Backoffice
    /// staff are authorised to see every agency's bookings per T-6-05,
    /// so there is intentionally NO agency_id filter on this DbSet.
    /// </summary>
    public DbSet<BookingReadRow> BookingReadModel => Set<BookingReadRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // MassTransit outbox tables — Plan 03-01 pattern.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        modelBuilder.Entity<DeadLetterQueueRow>(e =>
        {
            e.HasIndex(x => x.MessageId)
                .HasFilter("[ResolvedAt] IS NULL")
                .HasDatabaseName("IX_DeadLetterQueue_MessageId_Unresolved");
            e.HasIndex(x => x.FirstFailedAt)
                .IsDescending()
                .HasDatabaseName("IX_DeadLetterQueue_FirstFailedAt");
            e.Property(x => x.RequeueCount).HasDefaultValue(0);
        });

        modelBuilder.Entity<WalletCreditRequest>(e =>
        {
            e.Property(x => x.Amount).HasColumnType("decimal(18,4)");
            e.Property(x => x.RequestedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.Property(x => x.Status).HasDefaultValue("PendingApproval");
            e.HasIndex(x => new { x.Status, x.RequestedAt })
                .IsDescending(false, true)
                .HasDatabaseName("IX_WalletCreditRequests_Status_RequestedAt");
            // CHECK constraints for D-53 reason codes + status enum + amount bounds.
            e.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_WalletCreditRequests_Amount",
                    "[Amount] > 0 AND [Amount] <= 100000");
                t.HasCheckConstraint(
                    "CK_WalletCreditRequests_ReasonCode",
                    "[ReasonCode] IN ('RefundedBooking','GoodwillCredit','DisputeResolution','SupplierRefundPassthrough')");
                t.HasCheckConstraint(
                    "CK_WalletCreditRequests_Status",
                    "[Status] IN ('PendingApproval','Approved','Denied','Expired')");
            });
        });

        modelBuilder.Entity<CancellationRequest>(e =>
        {
            e.Property(x => x.RequestedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.Property(x => x.Status).HasDefaultValue("PendingApproval");
            e.HasIndex(x => new { x.BookingId, x.Status })
                .HasDatabaseName("IX_CancellationRequests_BookingId_Status");
            e.HasIndex(x => new { x.Status, x.RequestedAt })
                .IsDescending(false, true)
                .HasDatabaseName("IX_CancellationRequests_Status_RequestedAt");
            e.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_CancellationRequests_ReasonCode",
                    "[ReasonCode] IN ('CustomerRequest','SupplierInitiated','FareRuleViolation','FraudSuspected','DuplicateBooking','Other')");
                t.HasCheckConstraint(
                    "CK_CancellationRequests_Status",
                    "[Status] IN ('PendingApproval','Approved','Denied','Expired')");
            });
        });

        modelBuilder.Entity<SupplierContract>(e =>
        {
            e.Property(x => x.NetRate).HasColumnType("decimal(18,4)");
            e.Property(x => x.CommissionPercent).HasColumnType("decimal(5,2)");
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.Property(x => x.IsDeleted).HasDefaultValue(false);
            // Hot paths: list filtered by ProductType + IsDeleted; secondary
            // list order-by ValidTo DESC. Composite covers both.
            e.HasIndex(x => new { x.IsDeleted, x.ProductType, x.ValidTo })
                .HasDatabaseName("IX_SupplierContracts_IsDeleted_ProductType_ValidTo");
            e.ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_SupplierContracts_ProductType",
                    "[ProductType] IN ('Flight','Hotel','Car','Package')");
                t.HasCheckConstraint(
                    "CK_SupplierContracts_NetRate",
                    "[NetRate] >= 0");
                t.HasCheckConstraint(
                    "CK_SupplierContracts_CommissionPercent",
                    "[CommissionPercent] >= 0 AND [CommissionPercent] <= 100");
                // Validity window must be coherent. Enforced at DB layer
                // so direct SQL tools can't create malformed rows.
                t.HasCheckConstraint(
                    "CK_SupplierContracts_Validity",
                    "[ValidTo] >= [ValidFrom]");
            });
        });
    }
}
