using MassTransit;
using TBE.Contracts.Enums;

namespace TBE.BookingService.Application.Saga;

/// <summary>
/// Persisted saga state per D-01. Implements <see cref="SagaStateMachineInstance"/> for
/// MassTransit correlation and <see cref="ISagaVersion"/> for optimistic concurrency (see Pitfall 2).
/// The <c>Version</c> property is mapped as a row-version (concurrency token) in EF Core.
/// Warn24HSent and Warn2HSent are owned by this plan and mutated ONLY by the 03-03 TTL
/// monitor hosted service — kept here to avoid cross-plan state-map migration leakage.
/// No passport / payment PII is stored here: passenger PII enters only in Phase 4 (D-20).
///
/// Plan 05-02 Task 2 adds: typed <see cref="Channel"/> enum (D-24), agency identification
/// (<see cref="AgencyId"/>, D-33 single-valued), frozen agency-pricing snapshot
/// (<see cref="AgencyNetFare"/>…<see cref="AgencyCommissionAmount"/>, D-36/D-41), admin-only
/// per-booking override (<see cref="AgencyMarkupOverride"/>, D-37), and customer-contact
/// snapshot for the on-behalf booking flow (<see cref="CustomerName"/>/<see cref="CustomerEmail"/>/
/// <see cref="CustomerPhone"/>, B2B-04).
/// </summary>
public class BookingSagaState : SagaStateMachineInstance, ISagaVersion
{
    public Guid CorrelationId { get; set; }
    public int CurrentState { get; set; }
    public int Version { get; set; }

    public string BookingReference { get; set; } = string.Empty;
    public string ProductType { get; set; } = string.Empty;

    /// <summary>
    /// Phase-3 textual channel marker ("b2c" / "b2b") mirroring
    /// <see cref="TBE.Contracts.Events.BookingInitiated.Channel"/>. Preserved for
    /// backwards compatibility with the Plan 03-01 migration + rows already in
    /// production; the B2B saga branch reads the typed <see cref="Channel"/>
    /// enum instead (Plan 05-02 Task 2).
    /// </summary>
    public string ChannelText { get; set; } = string.Empty;

    /// <summary>
    /// Plan 05-02 D-24 / Task 2 — typed channel used by the saga's IfElse branch
    /// at <c>PnrCreated</c>. Backed by the new <c>ChannelKind</c> column;
    /// defaults to <see cref="Channel.B2C"/> so existing rows stay on the
    /// Stripe-authorize path during backfill.
    /// </summary>
    public Channel Channel { get; set; } = Channel.B2C;

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

    // --- Plan 05-02 Task 2 — B2B agency pricing snapshot + customer contact ---

    /// <summary>D-33 single-valued agency id (NEVER from request body; stamped from JWT claim).</summary>
    public Guid? AgencyId { get; set; }

    /// <summary>Frozen-at-booking net fare from the GDS offer (D-36; Pitfall 21 — NET never surfaced to traveller).</summary>
    public decimal? AgencyNetFare { get; set; }

    /// <summary>Resolved markup amount (<see cref="AgencyNetFare"/> × PercentOfNet + FlatAmount) per D-36.</summary>
    public decimal? AgencyMarkupAmount { get; set; }

    /// <summary>NET + Markup, i.e. the amount the traveller sees / the agency wallet is debited for.</summary>
    public decimal? AgencyGrossAmount { get; set; }

    /// <summary>Display-only commission per D-41 (v1: == <see cref="AgencyMarkupAmount"/>; settlement deferred to Phase 6).</summary>
    public decimal? AgencyCommissionAmount { get; set; }

    /// <summary>D-37 per-booking markup override captured from the checkout form. Admin-only — enforced server-side in <c>AgentBookingsController</c>.</summary>
    public decimal? AgencyMarkupOverride { get; set; }

    /// <summary>Customer contact snapshot — captured at on-behalf booking time (B2B-04).</summary>
    public string? CustomerName { get; set; }

    /// <summary>Customer email snapshot (B2B-04). Not a B2B agent identity — the agent is on <see cref="UserId"/>.</summary>
    public string? CustomerEmail { get; set; }

    /// <summary>Customer phone snapshot (B2B-04).</summary>
    public string? CustomerPhone { get; set; }

    /// <summary>Optional B2B failure reason surfaced by the saga when <c>WalletReserveFailed</c> fires.</summary>
    public string? FailureReason { get; set; }

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

    // --- Plan 06-01 Task 5 — BO-03 staff cancel/modify metadata ---

    /// <summary>
    /// True when the row is in, or has been through, the staff-initiated
    /// cancellation flow. Distinguishes ops-triggered cancellations from
    /// customer-requested voids (D-48).
    /// </summary>
    public bool CancelledByStaff { get; set; }

    /// <summary>
    /// One of <c>CustomerRequest / SupplierInitiated / FareRuleViolation /
    /// FraudSuspected / DuplicateBooking / Other</c> — enforced by CHECK
    /// constraint on the column (migration AddCancellationColumns).
    /// </summary>
    public string? CancellationReasonCode { get; set; }

    /// <summary>Free-text cancellation reason (&lt;=500 chars).</summary>
    public string? CancellationReason { get; set; }

    /// <summary>preferred_username of the first-eye operator that opened the cancellation request.</summary>
    public string? CancellationRequestedBy { get; set; }

    /// <summary>preferred_username of the second-eye ops-admin that approved the cancellation.</summary>
    public string? CancellationApprovedBy { get; set; }

    /// <summary>UTC timestamp when the row flipped to Approved (Plan 06-01 / D-48).</summary>
    public DateTime? CancellationApprovedAt { get; set; }
}
