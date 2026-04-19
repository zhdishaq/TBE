using MassTransit;
using Microsoft.EntityFrameworkCore;
using TBE.CrmService.Application.Projections;

namespace TBE.CrmService.Infrastructure;

/// <summary>
/// Plan 06-04 Task 1 — CrmService's local projection store. Event-sourced
/// build-up per D-51: every row here is derived from an integration event
/// consumed through MassTransit with <c>InboxState</c> dedup on MessageId.
/// </summary>
public class CrmDbContext : DbContext
{
    public CrmDbContext(DbContextOptions<CrmDbContext> options) : base(options)
    {
    }

    public DbSet<CustomerProjection> Customers => Set<CustomerProjection>();
    public DbSet<AgencyProjection> Agencies => Set<AgencyProjection>();
    public DbSet<BookingProjection> BookingProjections => Set<BookingProjection>();
    public DbSet<CommunicationLogRow> CommunicationLog => Set<CommunicationLogRow>();
    public DbSet<UpcomingTripRow> UpcomingTrips => Set<UpcomingTripRow>();
    public DbSet<CustomerErasureTombstoneRow> CustomerErasureTombstones => Set<CustomerErasureTombstoneRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("crm");

        // MassTransit outbox / inbox tables — consumer-level dedup per D-51.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        modelBuilder.Entity<CustomerProjection>(e =>
        {
            e.ToTable("Customers", "crm");
            e.HasKey(c => c.Id);
            e.Property(c => c.Email).HasMaxLength(256);
            e.Property(c => c.Name).HasMaxLength(256);
            e.Property(c => c.Phone).HasMaxLength(64);
            e.Property(c => c.LifetimeGross).HasColumnType("decimal(18,4)");
            e.HasIndex(c => c.Email)
                .HasFilter("[Email] IS NOT NULL")
                .HasDatabaseName("IX_Customers_Email");
            e.HasIndex(c => c.Name)
                .HasFilter("[Name] IS NOT NULL")
                .HasDatabaseName("IX_Customers_Name");
        });

        modelBuilder.Entity<AgencyProjection>(e =>
        {
            e.ToTable("Agencies", "crm");
            e.HasKey(a => a.Id);
            e.Property(a => a.Name).HasMaxLength(256).IsRequired();
            e.Property(a => a.ContactEmail).HasMaxLength(256);
            e.Property(a => a.ContactPhone).HasMaxLength(64);
            e.Property(a => a.LifetimeGross).HasColumnType("decimal(18,4)");
            e.Property(a => a.LifetimeCommission).HasColumnType("decimal(18,4)");
            e.HasIndex(a => a.Name).HasDatabaseName("IX_Agencies_Name");
        });

        modelBuilder.Entity<BookingProjection>(e =>
        {
            e.ToTable("BookingProjections", "crm");
            e.HasKey(b => b.Id);
            e.Property(b => b.BookingReference).HasMaxLength(64);
            e.Property(b => b.Pnr).HasMaxLength(32);
            e.Property(b => b.Channel).HasMaxLength(16).IsRequired();
            e.Property(b => b.Status).HasMaxLength(32).IsRequired();
            e.Property(b => b.Currency).HasMaxLength(3).IsRequired();
            e.Property(b => b.GrossAmount).HasColumnType("decimal(18,4)");
            e.Property(b => b.CommissionAmount).HasColumnType("decimal(18,4)");
            e.Property(b => b.TicketNumber).HasMaxLength(32);
            e.Property(b => b.OriginIata).HasMaxLength(3);
            e.Property(b => b.DestinationIata).HasMaxLength(3);
            e.Property(b => b.CustomerName).HasMaxLength(256);
            e.HasIndex(b => b.CustomerId)
                .HasFilter("[CustomerId] IS NOT NULL")
                .HasDatabaseName("IX_BookingProjections_CustomerId");
            e.HasIndex(b => b.AgencyId)
                .HasFilter("[AgencyId] IS NOT NULL")
                .HasDatabaseName("IX_BookingProjections_AgencyId");
            e.HasIndex(b => b.Pnr)
                .HasFilter("[Pnr] IS NOT NULL")
                .HasDatabaseName("IX_BookingProjections_Pnr");
            e.HasIndex(b => b.BookingReference)
                .HasFilter("[BookingReference] IS NOT NULL")
                .HasDatabaseName("IX_BookingProjections_BookingReference");
            e.HasIndex(b => new { b.TravelDate, b.Status })
                .HasDatabaseName("IX_BookingProjections_TravelDate_Status");
        });

        modelBuilder.Entity<CommunicationLogRow>(e =>
        {
            e.ToTable("CommunicationLog", "crm");
            e.HasKey(c => c.LogId);
            e.Property(c => c.EntityType).HasMaxLength(16).IsRequired();
            e.Property(c => c.CreatedBy).HasMaxLength(128).IsRequired();
            e.Property(c => c.Body).HasMaxLength(10000).IsRequired();
            e.HasIndex(c => new { c.EntityType, c.EntityId, c.CreatedAt })
                .IsDescending(false, false, true)
                .HasDatabaseName("IX_CommunicationLog_Entity_CreatedAt");
        });

        modelBuilder.Entity<UpcomingTripRow>(e =>
        {
            e.ToTable("UpcomingTrips", "crm");
            e.HasKey(u => u.BookingId);
            e.Property(u => u.BookingReference).HasMaxLength(64);
            e.Property(u => u.Pnr).HasMaxLength(32);
            e.Property(u => u.Status).HasMaxLength(32).IsRequired();
            e.Property(u => u.Currency).HasMaxLength(3).IsRequired();
            e.Property(u => u.GrossAmount).HasColumnType("decimal(18,4)");
            e.Property(u => u.OriginIata).HasMaxLength(3);
            e.Property(u => u.DestinationIata).HasMaxLength(3);
            e.HasIndex(u => new { u.TravelDate, u.Status })
                .HasDatabaseName("IX_UpcomingTrips_TravelDate_Status");
            e.HasIndex(u => new { u.AgencyId, u.TravelDate })
                .HasFilter("[AgencyId] IS NOT NULL")
                .HasDatabaseName("IX_UpcomingTrips_AgencyId_TravelDate");
        });

        modelBuilder.Entity<CustomerErasureTombstoneRow>(e =>
        {
            e.ToTable("CustomerErasureTombstones", "crm");
            e.HasKey(t => t.Id);
            e.Property(t => t.EmailHash).HasMaxLength(64).IsRequired();
            e.Property(t => t.ErasedBy).HasMaxLength(128).IsRequired();
            e.Property(t => t.Reason).HasMaxLength(500).IsRequired();
            e.HasIndex(t => t.EmailHash).IsUnique()
                .HasDatabaseName("UX_CustomerErasureTombstones_EmailHash");
            e.HasIndex(t => t.ErasedAt).IsDescending()
                .HasDatabaseName("IX_CustomerErasureTombstones_ErasedAt");
        });
    }
}
