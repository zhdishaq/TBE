namespace TBE.Contracts.Commands;

/// <summary>
/// Plan 05-02 Task 2 — B2B booking saga command to place a reservation hold on
/// the agency wallet at <c>PnrCreated</c>. Published by <c>BookingSaga</c> on
/// the B2B branch; consumed by PaymentService's <c>WalletReserveConsumer</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>D-40 / T-05-02-04 (double-spend mitigation):</b> <see cref="IdempotencyKey"/>
/// is set to <c>BookingId.ToString()</c> by the saga, so retries, duplicate
/// outbox dispatches, or replay of this command resolve to at most ONE
/// ledger entry. PaymentService's wallet ledger INSERTs use a unique
/// constraint on <c>IdempotencyKey</c>; duplicate inserts surface as the
/// pre-existing reserved row (consumer re-publishes the existing
/// <see cref="TBE.Contracts.Events.WalletReserved"/> rather than creating
/// a second hold).
/// </para>
/// <para>
/// <b>Plan deviation (Rule 3 — blocking):</b> the plan's interface contract
/// omits <see cref="WalletId"/>, but PaymentService's Phase-3
/// <c>IWalletRepository.ReserveAsync</c> is keyed by walletId. Rather than
/// adding an agency→wallet lookup on the hot path (which would require a
/// new repository method + migration in Phase 5), the agent client sends
/// both ids — server-side stamping on the controller guarantees the
/// agency_id is JWT-derived (T-05-02-08). Follow-on Plan 05-03 wires
/// agent-admin wallet top-up against the same <see cref="AgencyId"/>.
/// </para>
/// </remarks>
public sealed record WalletReserveCommand(
    Guid CorrelationId,
    Guid BookingId,
    Guid AgencyId,
    Guid WalletId,
    decimal Amount,
    string Currency,
    string IdempotencyKey);
