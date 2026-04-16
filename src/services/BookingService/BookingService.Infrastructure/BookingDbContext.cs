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
    public DbSet<HotelBookingSagaState> HotelBookingSagaStates => Set<HotelBookingSagaState>();

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

        // Plan 04-03 — hotel-booking aggregate (HOTB-01..05, D-16)
        modelBuilder.ApplyConfiguration(new HotelBookingSagaStateMap());
    }
}
