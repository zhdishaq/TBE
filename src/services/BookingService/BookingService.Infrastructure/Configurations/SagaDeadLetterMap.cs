using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TBE.BookingService.Application.Saga;

namespace TBE.BookingService.Infrastructure.Configurations;

/// <summary>
/// EF Core mapping for the <see cref="SagaDeadLetter"/> ledger row.
/// Table lives in the <c>Saga</c> schema for operational isolation.
/// </summary>
public class SagaDeadLetterMap : IEntityTypeConfiguration<SagaDeadLetter>
{
    public void Configure(EntityTypeBuilder<SagaDeadLetter> b)
    {
        b.ToTable("SagaDeadLetter", "Saga");
        b.HasKey(x => x.Id);

        b.Property(x => x.LastSuccessfulStep).HasMaxLength(32).IsRequired();
        b.Property(x => x.FailedStep).HasMaxLength(32).IsRequired();
        b.Property(x => x.ExceptionMessage).HasMaxLength(1000).IsRequired();
        b.Property(x => x.ExceptionDetail).HasMaxLength(4000);

        b.HasIndex(x => x.CorrelationId);
        b.HasIndex(x => new { x.Resolved, x.CreatedAtUtc });
    }
}
