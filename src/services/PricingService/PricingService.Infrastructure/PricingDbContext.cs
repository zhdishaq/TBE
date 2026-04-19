using MassTransit;
using Microsoft.EntityFrameworkCore;
using TBE.PricingService.Application.Agency;
using TBE.PricingService.Application.Rules.Models;

namespace TBE.PricingService.Infrastructure;

public class PricingDbContext : DbContext
{
    public PricingDbContext(DbContextOptions<PricingDbContext> options) : base(options)
    {
    }

    public DbSet<MarkupRule> MarkupRules => Set<MarkupRule>();

    /// <summary>Plan 05-02 / D-36 — per-agency markup rules (max 2 active rows per agency).</summary>
    public DbSet<AgencyMarkupRule> AgencyMarkupRules => Set<AgencyMarkupRule>();

    /// <summary>Plan 06-03 / D-52 — immutable audit trail for every AgencyMarkupRule mutation.</summary>
    public DbSet<MarkupRuleAuditLogRow> MarkupRuleAuditLog => Set<MarkupRuleAuditLogRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // MassTransit outbox tables
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        modelBuilder.Entity<MarkupRule>(b =>
        {
            b.HasKey(r => r.Id);
            b.Property(r => r.ProductType).IsRequired().HasMaxLength(10);
            b.Property(r => r.AirlineCode).HasMaxLength(3);
            b.Property(r => r.RouteOrigin).HasMaxLength(3);
            b.Property(r => r.Channel).IsRequired().HasMaxLength(5);
            b.Property(r => r.Value).HasColumnType("decimal(18,4)");
            b.Property(r => r.MaxAmount).HasColumnType("decimal(18,4)");
            b.HasIndex(r => new { r.ProductType, r.Channel, r.IsActive });
        });

        // Plan 05-02 / D-36: AgencyMarkupRules with composite PK (AgencyId, RouteClass).
        // Filtered unique index on IsActive=1 enforces the max-2-active-rows invariant
        // (one base row with RouteClass NULL + at most one RouteClass override).
        modelBuilder.Entity<AgencyMarkupRule>(b =>
        {
            b.ToTable("AgencyMarkupRules", "pricing");
            // D-36: surrogate PK (see AgencyMarkupRule.Id XMLDOC) — max-2-active-rows
            // invariant enforced by the filtered unique index below.
            b.HasKey(r => r.Id);
            b.Property(r => r.RouteClass).HasMaxLength(32);
            b.Property(r => r.FlatAmount).HasColumnType("decimal(18,4)").IsRequired();
            b.Property(r => r.PercentOfNet).HasColumnType("decimal(5,4)").IsRequired();
            b.Property(r => r.IsActive).IsRequired();
            b.Property(r => r.CreatedAt).HasColumnType("datetime2").IsRequired();
            b.Property(r => r.UpdatedAt).HasColumnType("datetime2").IsRequired();
            // D-36: filtered UNIQUE index on (AgencyId, RouteClass) where IsActive=1.
            // SQL Server treats two NULL RouteClass values as equal within a unique
            // index, so this enforces at most ONE active base row + ONE active
            // override row per (agency, routeclass) tuple.
            b.HasIndex(r => new { r.AgencyId, r.RouteClass })
                .IsUnique()
                .HasDatabaseName("IX_AgencyMarkupRules_Active")
                .HasFilter("[IsActive] = 1");
        });

        // Plan 06-03 / D-52: immutable audit trail for AgencyMarkupRules mutations.
        // CHECK constraint mirrors the migration — code-level change without a
        // matching migration drop/recreate is the only path that could widen the
        // Action enum, so the guard is redundant-but-defensive.
        modelBuilder.Entity<MarkupRuleAuditLogRow>(b =>
        {
            b.ToTable("MarkupRuleAuditLog", "pricing", t =>
            {
                t.HasCheckConstraint(
                    "CK_MarkupRuleAuditLog_Action",
                    "[Action] IN ('Created','Updated','Deactivated','Deleted')");
            });
            b.HasKey(r => r.Id);
            b.Property(r => r.Id).ValueGeneratedOnAdd();
            b.Property(r => r.Action).IsRequired().HasMaxLength(32);
            b.Property(r => r.Actor).IsRequired().HasMaxLength(128);
            b.Property(r => r.Reason).IsRequired().HasMaxLength(500);
            b.Property(r => r.BeforeJson).HasColumnType("nvarchar(max)");
            b.Property(r => r.AfterJson).HasColumnType("nvarchar(max)");
            b.Property(r => r.ChangedAt).HasColumnType("datetime2").IsRequired();
            b.HasIndex(r => new { r.AgencyId, r.ChangedAt })
                .IsDescending(false, true)
                .HasDatabaseName("IX_MarkupRuleAuditLog_AgencyId_ChangedAt");
            b.HasIndex(r => new { r.RuleId, r.ChangedAt })
                .IsDescending(false, true)
                .HasDatabaseName("IX_MarkupRuleAuditLog_RuleId_ChangedAt");
            b.HasIndex(r => new { r.Actor, r.ChangedAt })
                .IsDescending(false, true)
                .HasDatabaseName("IX_MarkupRuleAuditLog_Actor_ChangedAt");
        });
    }
}
