using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TBE.PaymentService.Infrastructure.Wallet;

namespace TBE.PaymentService.Infrastructure.Configurations;

public sealed class WalletTransactionMap : IEntityTypeConfiguration<WalletTransaction>
{
    public void Configure(EntityTypeBuilder<WalletTransaction> b)
    {
        b.ToTable("WalletTransactions", "payment");
        b.HasKey(x => x.TxId);
        b.Property(x => x.TxId).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(x => x.WalletId).IsRequired();
        b.Property(x => x.EntryType).HasConversion<byte>().IsRequired();
        b.Property(x => x.Amount).HasColumnType("decimal(18,4)").IsRequired();
        b.Property(x => x.SignedAmount)
            .HasColumnType("decimal(18,4)")
            .HasComputedColumnSql("CASE WHEN [EntryType] IN (1,2) THEN -[Amount] ELSE [Amount] END", stored: true);
        b.Property(x => x.Currency).HasColumnType("char(3)").IsRequired();
        b.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
        b.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");

        // D-39 / Plan 06-01 Task 6 — audit columns for ManualCredit +
        // (future) CommissionPayout rows. Both NULL for system entries.
        b.Property(x => x.ApprovedBy).HasMaxLength(128);
        b.Property(x => x.ApprovalNotes).HasMaxLength(500);

        b.HasIndex(x => x.IdempotencyKey).IsUnique();
        b.HasIndex(x => new { x.WalletId, x.CreatedAtUtc });
    }
}
