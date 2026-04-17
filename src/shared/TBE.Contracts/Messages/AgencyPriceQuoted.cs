namespace TBE.Contracts.Messages;

/// <summary>
/// Plan 05-02 / D-36 / D-41 — quote published by <c>AgencyPriceRequestedConsumer</c>
/// after the D-36 <c>override ?? base</c> resolver computes the markup. Travels the
/// internal MassTransit bus only: NET fare is NEVER exposed on any traveller-facing
/// surface (T-05-02-05 / Pitfall 21).
/// </summary>
/// <param name="CorrelationId">Echoes the originating <see cref="AgencyPriceRequested.CorrelationId"/>.</param>
/// <param name="AgencyId">Agency tenant for which the markup was resolved.</param>
/// <param name="OfferId">GDS offer identifier carried through from the request.</param>
/// <param name="NetFare">Raw GDS NET fare, unchanged from the request.</param>
/// <param name="MarkupAmount">FlatAmount + NetFare * PercentOfNet (D-36 formula).</param>
/// <param name="GrossPrice">NetFare + MarkupAmount — the customer-facing price.</param>
/// <param name="CommissionAmount">D-41 v1: equals <paramref name="MarkupAmount"/>. Display-only; settlement deferred to Phase 6.</param>
/// <param name="Currency">ISO-4217 currency code; mirrors the request.</param>
public sealed record AgencyPriceQuoted(
    Guid CorrelationId,
    Guid AgencyId,
    string OfferId,
    decimal NetFare,
    decimal MarkupAmount,
    decimal GrossPrice,
    decimal CommissionAmount,
    string Currency);
