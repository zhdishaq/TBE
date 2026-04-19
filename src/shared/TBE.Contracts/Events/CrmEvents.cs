namespace TBE.Contracts.Events;

/// <summary>
/// Plan 06-04 — CRM + compliance integration events.
///
/// CrmService subscribes to these (alongside the Phase 3/5 saga events
/// <see cref="BookingConfirmed"/>, <see cref="BookingCancelled"/>,
/// <see cref="TicketIssued"/>, <see cref="WalletToppedUp"/>) to build
/// local projections (D-51). These events are also consumed by
/// BookingService + PaymentService to participate in the GDPR erasure
/// fan-out (COMP-03 / D-57).
/// </summary>

/// <summary>
/// Plan 06-04 Task 1 — introduced for CrmService.UserRegisteredConsumer
/// which seeds <c>crm.Customers</c> upon B2C account creation. Published
/// by the B2C portal's `/api/auth/register` flow after Keycloak accepts
/// the new user. Consumed by CrmService only in v1; Notifications may
/// subscribe later for a "welcome" email.
/// </summary>
public record UserRegistered(
    Guid UserId,
    string Email,
    string Name,
    DateTime At);

/// <summary>
/// Plan 06-04 Task 1 — ops-cs or ops-admin logged a free-form note
/// against a Customer or Agency via /api/crm/communication-log.
/// Persisted by <c>CustomerCommunicationLoggedConsumer</c> (the
/// controller also writes the row directly; the consumer handles
/// cross-service log publishes from future sources).
/// </summary>
/// <remarks>
/// Body is markdown-safe text (D-62); max 10000 chars validated at the
/// controller boundary.
/// </remarks>
public record CustomerCommunicationLogged(
    Guid LogId,
    string EntityType,   // 'Customer' | 'Agency'
    Guid EntityId,
    string CreatedBy,    // preferred_username of the ops staff member
    string BodyMarkdown,
    DateTime At);

/// <summary>
/// Plan 06-04 Task 3 / COMP-03 / D-57 — ops-admin has requested erasure
/// of a customer's PII. Published by
/// <c>BackofficeService.ErasureController</c> via outbox. Consumed by:
/// <list type="bullet">
///   <item>CrmService — nulls projection PII, writes tombstone, publishes <see cref="CustomerErased"/>.</item>
///   <item>BookingService — nulls BookingSagaState PII columns
///     (<c>CustomerName/Email/Phone/PassportNumber/DateOfBirth</c>);
///     does NOT touch <c>BookingEvents</c> per D-49.</item>
/// </list>
/// Idempotent via MassTransit InboxState dedup + UNIQUE(EmailHash)
/// on the tombstone table (re-publishing the same RequestId is a no-op).
/// </summary>
public record CustomerErasureRequested(
    Guid RequestId,
    Guid CustomerId,
    string EmailHash,    // SHA-256 hex of normalised email (D-57)
    string RequestedBy,
    string Reason,
    DateTime At);

/// <summary>
/// Plan 06-04 Task 3 / COMP-03 — published by
/// <c>CrmService.CustomerErasureRequestedConsumer</c> once the CRM
/// projection is fully anonymised + tombstoned. Downstream observers
/// (future audit/alerting) can subscribe without participating in the
/// erasure write path itself.
/// </summary>
public record CustomerErased(
    Guid CustomerId,
    string EmailHash,
    DateTime ErasedAt);

/// <summary>
/// Plan 06-04 Task 2 / CRM-02 / D-61 — ops-finance (or ops-admin) has
/// adjusted an agency's credit limit via
/// <c>PATCH /api/payments/agencies/{agencyId}/credit-limit</c>.
/// Audited in <c>payment.CreditLimitAuditLog</c> atomically with the
/// column update; this event is the async fan-out for downstream
/// observers (future risk/commission systems).
/// </summary>
public record AgencyCreditLimitChanged(
    Guid AgencyId,
    decimal OldLimit,
    decimal NewLimit,
    string ChangedBy,    // preferred_username (Pitfall 28 fail-closed)
    string Reason,
    DateTime At);
