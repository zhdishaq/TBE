using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TBE.BackofficeService.Application.Entities;

/// <summary>
/// Plan 06-01 Task 7 (BO-01) — cross-schema read model mapped to
/// <c>Saga.BookingSagaState</c> owned by BookingService. Keyless-friendly
/// subset of the columns the backoffice unified booking list needs; the
/// <see cref="CorrelationId"/> is the booking id in all client-facing APIs
/// per D-01.
///
/// <para>
/// CONTEXT §Integration Points accepts cross-service read via shared
/// deployment target in dev. Production split may replace this with a
/// projection (Plan 06-04 CRM). The DbContext mapping uses schema=Saga
/// in production SQL Server; EF InMemory ignores schema so unit tests
/// seed this type directly. T-6-05 accepts cross-tenant read because
/// backoffice staff are authorised to see every agency's bookings.
/// </para>
/// </summary>
[Table("BookingSagaState", Schema = "Saga")]
public sealed class BookingReadRow
{
    [Key]
    public Guid CorrelationId { get; set; }

    [MaxLength(32)]
    public string BookingReference { get; set; } = string.Empty;

    /// <summary>
    /// Typed channel column (<c>ChannelKind</c>) per D-24: 0=B2C, 1=B2B,
    /// 2=Manual (Plan 06-02 reserves 2 in the enum). The read layer maps
    /// these ints to the caller-facing "B2C"/"B2B"/"Manual" strings.
    /// </summary>
    public int ChannelKind { get; set; }

    public int CurrentState { get; set; }

    [MaxLength(12)]
    public string? GdsPnr { get; set; }

    [MaxLength(32)]
    public string? TicketNumber { get; set; }

    [MaxLength(200)]
    public string? CustomerName { get; set; }

    [MaxLength(320)]
    public string? CustomerEmail { get; set; }

    public Guid? AgencyId { get; set; }

    public decimal TotalAmount { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "GBP";

    public DateTime InitiatedAtUtc { get; set; }
}
