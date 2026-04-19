using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TBE.PaymentService.Application.Wallet;

namespace TBE.PaymentService.Infrastructure.Configurations;

/// <summary>
/// Plan 06-04 / CRM-02 / D-61 / T-6-59 — EF mapping for
/// <c>payment.CreditLimitAuditLog</c>. One row per
/// <c>AgencyCreditLimitController.PATCH</c> call.
/// </summary>
public sealed class CreditLimitAuditLogMap : IEntityTypeConfiguration<CreditLimitAuditLogRow>
{
    public void Configure(EntityTypeBuilder<CreditLimitAuditLogRow> b)
    {
        b.ToTable("CreditLimitAuditLog", "payment");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        b.Property(x => x.AgencyId).IsRequired();
        b.Property(x => x.OldLimit).HasColumnType("decimal(18,4)").IsRequired();
        b.Property(x => x.NewLimit).HasColumnType("decimal(18,4)").IsRequired();
        b.Property(x => x.ChangedBy).HasMaxLength(128).IsRequired();
        b.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        b.Property(x => x.ChangedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()").IsRequired();

        // List hot path: per-agency history, most-recent-first.
        b.HasIndex(x => new { x.AgencyId, x.ChangedAtUtc })
            .IsDescending(false, true)
            .HasDatabaseName("IX_CreditLimitAuditLog_AgencyId_ChangedAtUtc");
    }
}
