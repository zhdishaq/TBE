using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace TBE.NotificationService.Application.Persistence;

public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options)
    {
    }

    public DbSet<EmailIdempotencyLog> EmailIdempotencyLogs => Set<EmailIdempotencyLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // MassTransit outbox/inbox tables (pattern copied from PricingDbContext).
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        modelBuilder.Entity<EmailIdempotencyLog>(b =>
        {
            b.ToTable("EmailIdempotencyLog", schema: "notification");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedOnAdd();
            b.Property(x => x.EventId).IsRequired();
            b.Property(x => x.EmailType).IsRequired().HasMaxLength(64);
            b.Property(x => x.Recipient).IsRequired().HasMaxLength(256);
            b.Property(x => x.ProviderMessageId).HasMaxLength(128);
            b.Property(x => x.SentAtUtc).IsRequired();

            // NOTF-06: unique (EventId, EmailType) enforces exactly-one email per (event, email-type).
            b.HasIndex(x => new { x.EventId, x.EmailType })
                .IsUnique()
                .HasDatabaseName("IX_EmailIdempotencyLog_EventId_EmailType");
        });
    }
}
