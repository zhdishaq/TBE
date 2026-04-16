using MassTransit;
using Microsoft.EntityFrameworkCore;
using TBE.BookingService.Application.Baskets;
using TBE.BookingService.Application.Cars;
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

    // Plan 04-04 — Trip Builder basket aggregate + inbox-pattern event log (D-08/D-10).
    public DbSet<Basket> Baskets => Set<Basket>();
    public DbSet<BasketEventLog> BasketEventLogs => Set<BasketEventLog>();

    // Plan 04-04 Task 3a — car-hire booking aggregate (CARB-01..03).
    public DbSet<CarBooking> CarBookings => Set<CarBooking>();

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

        // Plan 04-04 — Trip Builder basket + inbox event log (PKG-01..04, D-08/D-10)
        modelBuilder.ApplyConfiguration(new BasketMap());
        modelBuilder.ApplyConfiguration(new BasketEventLogMap());

        // Plan 04-04 Task 3a — car-hire booking aggregate (CARB-01..03).
        modelBuilder.ApplyConfiguration(new CarBookingMap());
    }
}
