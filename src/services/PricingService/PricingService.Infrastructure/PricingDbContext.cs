using MassTransit;
using Microsoft.EntityFrameworkCore;
using TBE.PricingService.Application.Rules.Models;

namespace TBE.PricingService.Infrastructure;

public class PricingDbContext : DbContext
{
    public PricingDbContext(DbContextOptions<PricingDbContext> options) : base(options)
    {
    }

    public DbSet<MarkupRule> MarkupRules => Set<MarkupRule>();

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
    }
}
