using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TBE.PaymentService.Application.Wallet;

namespace TBE.PaymentService.Infrastructure.Configurations;

/// <summary>
/// Plan 05-03 Task 2 — EF mapping for the per-agency wallet metadata table
/// (<c>payment.AgencyWallets</c>). The append-only
/// <c>payment.WalletTransactions</c> ledger stays untouched; this table only
/// owns threshold + low-balance hysteresis state.
/// </summary>
public sealed class AgencyWalletMap : IEntityTypeConfiguration<AgencyWallet>
{
    public void Configure(EntityTypeBuilder<AgencyWallet> b)
    {
        b.ToTable("AgencyWallets", "payment");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(x => x.AgencyId).IsRequired();
        b.Property(x => x.Currency).HasColumnType("char(3)").IsRequired();
        b.Property(x => x.LowBalanceThresholdAmount)
            .HasColumnType("decimal(18,4)")
            .HasDefaultValue(500m)
            .IsRequired();
        b.Property(x => x.LowBalanceEmailSent).HasDefaultValue(false).IsRequired();
        b.Property(x => x.LastLowBalanceEmailAtUtc);

        // Plan 06-04 / CRM-02 / D-61 — overdraft allowance. Default 0 so
        // the 20260604200000_AddAgencyCreditLimit migration is a safe
        // backfill for existing wallets (no overdraft until ops-finance
        // explicitly raises it via AgencyCreditLimitController).
        b.Property(x => x.CreditLimit)
            .HasColumnType("decimal(18,4)")
            .HasDefaultValue(0m)
            .IsRequired();

        b.Property(x => x.UpdatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()").IsRequired();

        // UNIQUE on AgencyId so cross-tenant mis-writes are a loud failure mode.
        b.HasIndex(x => x.AgencyId).IsUnique();
    }
}
