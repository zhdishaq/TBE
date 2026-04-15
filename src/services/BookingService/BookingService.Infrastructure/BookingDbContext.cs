using MassTransit;
using Microsoft.EntityFrameworkCore;
using TBE.BookingService.Application.Saga;
using TBE.BookingService.Infrastructure.Configurations;

namespace TBE.BookingService.Infrastructure;

public class BookingDbContext : DbContext
{
    public BookingDbContext(DbContextOptions<BookingDbContext> options) : base(options)
    {
    }

    public DbSet<BookingSagaState> BookingSagaStates => Set<BookingSagaState>();
    public DbSet<SagaDeadLetter> SagaDeadLetters => Set<SagaDeadLetter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // MassTransit outbox tables
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        // Saga state + dead-letter ledger (D-01 dedicated schema)
        modelBuilder.ApplyConfiguration(new BookingSagaStateMap());
        modelBuilder.ApplyConfiguration(new SagaDeadLetterMap());
    }
}
