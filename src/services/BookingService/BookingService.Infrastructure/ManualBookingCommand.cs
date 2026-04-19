using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TBE.BookingService.Application.Saga;
using TBE.BookingService.Infrastructure;
using TBE.Contracts.Enums;

namespace TBE.BookingService.Application;

/// <summary>
/// Plan 06-02 Task 1 (BO-02) — direct-insert command for staff-entered
/// manual bookings (phone / walk-in). Bypasses the saga entirely: no
/// <c>BookingInitiated</c> publish, no GDS PNR creation, no Stripe
/// authorization. Writes:
///
/// <list type="number">
///   <item>One <see cref="BookingSagaState"/> row with
///     <see cref="Channel.Manual"/> and <c>CurrentState</c> set to the
///     terminal Confirmed state code.</item>
///   <item>One <c>dbo.BookingEvents</c> row via
///     <see cref="IBookingEventsWriter"/> with EventType
///     <c>ManualBookingCreated</c> and a full snapshot including the
///     pricing breakdown + itinerary.</item>
/// </list>
///
/// <para>
/// Per Pitfall 28 the caller (controller) never passes Channel or
/// Status — this command stamps them. Duplicate <c>SupplierReference</c>
/// within a 24-hour window throws
/// <see cref="DuplicateSupplierReferenceException"/> which the
/// controller maps to a 409 problem+json.
/// </para>
///
/// <para>
/// Amount validation is ALSO performed here (double-guard against
/// malformed requests that slip past the controller DTO validation):
/// BaseFareAmount / SurchargeAmount / TaxAmount must all be
/// non-negative; ItineraryJson must be non-empty.
/// </para>
/// </summary>
public sealed class ManualBookingCommand
{
    /// <summary>
    /// The <c>CurrentState</c> int mapped to the
    /// <c>BookingSagaState.Confirmed</c> saga state. Hardcoded to 7 to
    /// match the Plan 06-01 read-model convention (see
    /// <c>BookingReadRow</c> / Plan 06-01 UnifiedBookingListTests).
    /// </summary>
    internal const int ConfirmedStateCode = 7;

    private readonly BookingDbContext _db;
    private readonly IBookingEventsWriter _eventsWriter;
    private readonly ILogger<ManualBookingCommand> _logger;

    public ManualBookingCommand(
        BookingDbContext db,
        IBookingEventsWriter eventsWriter,
        ILogger<ManualBookingCommand> logger)
    {
        _db = db;
        _eventsWriter = eventsWriter;
        _logger = logger;
    }

    public async Task<Guid> CreateAsync(ManualBookingInput input, string actor, CancellationToken ct)
    {
        if (input.BaseFareAmount < 0 || input.SurchargeAmount < 0 || input.TaxAmount < 0)
        {
            throw new ManualBookingValidationException(
                "invalid-amount",
                "BaseFareAmount, SurchargeAmount and TaxAmount must be non-negative.");
        }

        if (string.IsNullOrWhiteSpace(input.ItineraryJson))
        {
            throw new ManualBookingValidationException(
                "invalid-itinerary",
                "ItineraryJson must be a non-empty JSON document.");
        }

        var now = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(input.SupplierReference))
        {
            var windowStart = now.AddHours(-24);
            var dup = await _db.BookingSagaStates
                .AsNoTracking()
                .Where(b => b.SupplierReference == input.SupplierReference
                         && b.InitiatedAtUtc > windowStart)
                .Select(b => new { b.CorrelationId })
                .FirstOrDefaultAsync(ct);
            if (dup != null)
            {
                throw new DuplicateSupplierReferenceException(dup.CorrelationId, input.SupplierReference!);
            }
        }

        var bookingId = Guid.NewGuid();
        var total = input.BaseFareAmount + input.SurchargeAmount + input.TaxAmount;

        var state = new BookingSagaState
        {
            CorrelationId = bookingId,
            CurrentState = ConfirmedStateCode,
            BookingReference = input.BookingReference,
            ProductType = input.ProductType,
            ChannelText = "manual",
            Channel = Channel.Manual,
            UserId = actor,
            BaseFareAmount = input.BaseFareAmount,
            SurchargeAmount = input.SurchargeAmount,
            TaxAmount = input.TaxAmount,
            TotalAmount = total,
            Currency = input.Currency,
            PaymentMethod = "Manual",
            GdsPnr = input.Pnr,
            CustomerId = input.CustomerId,
            CustomerName = input.CustomerName,
            CustomerEmail = input.CustomerEmail,
            CustomerPhone = input.CustomerPhone,
            AgencyId = input.AgencyId,
            SupplierReference = input.SupplierReference,
            ItineraryJson = input.ItineraryJson,
            InitiatedAtUtc = now,
            ConfirmedAtUtc = now,
        };

        _db.BookingSagaStates.Add(state);
        await _db.SaveChangesAsync(ct);

        await _eventsWriter.WriteAsync(
            bookingId,
            "ManualBookingCreated",
            actor,
            bookingId,
            new
            {
                BookingId = bookingId,
                Channel = "Manual",
                Status = "Confirmed",
                input.BookingReference,
                input.Pnr,
                input.ProductType,
                PricingBreakdown = new
                {
                    input.BaseFareAmount,
                    input.SurchargeAmount,
                    input.TaxAmount,
                    GrossAmount = total,
                },
                input.Currency,
                input.SupplierReference,
                Customer = new { input.CustomerId, input.CustomerName, input.CustomerEmail },
                input.AgencyId,
                Itinerary = input.ItineraryJson,
                Notes = input.Notes,
            },
            ct);

        _logger.LogInformation(
            "manual-booking-created {BookingId} {Actor} {SupplierReference}",
            bookingId, actor, input.SupplierReference ?? "<none>");

        return bookingId;
    }
}

/// <summary>
/// Value-object input to <see cref="ManualBookingCommand.CreateAsync"/>.
/// Deliberately omits Channel and Status — those are stamped server-side
/// per Pitfall 28.
/// </summary>
public sealed record ManualBookingInput(
    string BookingReference,
    string Pnr,
    string ProductType,
    decimal BaseFareAmount,
    decimal SurchargeAmount,
    decimal TaxAmount,
    string Currency,
    Guid? CustomerId,
    string CustomerName,
    string CustomerEmail,
    string? CustomerPhone,
    Guid? AgencyId,
    string ItineraryJson,
    string? SupplierReference,
    string? Notes);

/// <summary>
/// Thrown when a second manual-booking attempt with the same
/// <c>SupplierReference</c> arrives within 24 hours of the first.
/// The controller maps this to 409 problem+json
/// <c>/errors/duplicate-supplier-reference</c>.
/// </summary>
public sealed class DuplicateSupplierReferenceException : Exception
{
    public Guid ExistingBookingId { get; }
    public string SupplierReference { get; }

    public DuplicateSupplierReferenceException(Guid existingBookingId, string supplierReference)
        : base($"Manual booking with supplier reference '{supplierReference}' already exists ({existingBookingId}).")
    {
        ExistingBookingId = existingBookingId;
        SupplierReference = supplierReference;
    }
}

/// <summary>
/// Thrown by <see cref="ManualBookingCommand"/> when input amounts or
/// itinerary fail domain-level validation. <see cref="Kind"/> is the
/// stable slug (appended to <c>/errors/manual-booking-</c>).
/// </summary>
public sealed class ManualBookingValidationException : Exception
{
    public string Kind { get; }

    public ManualBookingValidationException(string kind, string message) : base(message)
    {
        Kind = kind;
    }
}
