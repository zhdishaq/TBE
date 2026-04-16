using MassTransit;

namespace TBE.BookingService.Application.Saga;

/// <summary>
/// Persisted saga state per D-01. Implements <see cref="SagaStateMachineInstance"/> for
/// MassTransit correlation and <see cref="ISagaVersion"/> for optimistic concurrency (see Pitfall 2).
/// The <c>Version</c> property is mapped as a row-version (concurrency token) in EF Core.
/// Warn24HSent and Warn2HSent are owned by this plan and mutated ONLY by the 03-03 TTL
/// monitor hosted service — kept here to avoid cross-plan state-map migration leakage.
/// No passport / payment PII is stored here: passenger PII enters only in Phase 4 (D-20).
/// </summary>
public class BookingSagaState : SagaStateMachineInstance, ISagaVersion
{
    public Guid CorrelationId { get; set; }
    public int CurrentState { get; set; }
    public int Version { get; set; }

    public string BookingReference { get; set; } = string.Empty;
    public string ProductType { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public Guid? WalletId { get; set; }
    public Guid? WalletReservationTxId { get; set; }
    public string? OfferToken { get; set; }
    public string? GdsPnr { get; set; }
    public string? StripePaymentIntentId { get; set; }
    public string? TicketNumber { get; set; }

    /// <summary>
    /// Fare breakdown persisted for receipt regeneration (04-01 / FLTB-03 / D-15).
    /// Sourced from the GDS offer at PNR time and frozen onto the saga so the
    /// PDF receipt remains auditable after ticketing. All three are in the
    /// booking's <see cref="Currency"/>; their sum should equal <see cref="TotalAmount"/>.
    /// </summary>
    public decimal BaseFareAmount { get; set; }

    /// <summary>YQ/YR carrier-imposed surcharges (kept separate from taxes per FLTB-03 / EU-UK regs).</summary>
    public decimal SurchargeAmount { get; set; }

    /// <summary>Government taxes and airport fees.</summary>
    public decimal TaxAmount { get; set; }

    public DateTime TicketingDeadlineUtc { get; set; }
    public Guid? TimeoutTokenId { get; set; }
    public DateTime InitiatedAtUtc { get; set; }
    public string? LastSuccessfulStep { get; set; }

    /// <summary>Set by the 03-03 TTL monitor when the 24-hour pre-deadline warning is sent.</summary>
    public bool Warn24HSent { get; set; }

    /// <summary>Set by the 03-03 TTL monitor when the 2-hour pre-deadline warning is sent.</summary>
    public bool Warn2HSent { get; set; }
}
