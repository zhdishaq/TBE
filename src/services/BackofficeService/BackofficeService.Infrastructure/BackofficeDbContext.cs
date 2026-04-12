using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace TBE.BackofficeService.Infrastructure;

public class BackofficeDbContext : DbContext
{
    public BackofficeDbContext(DbContextOptions<BackofficeDbContext> options) : base(options)
    {
    }

    // Domain entities added in later phases

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // MassTransit outbox tables
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
