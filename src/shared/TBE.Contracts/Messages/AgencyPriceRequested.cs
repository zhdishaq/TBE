namespace TBE.Contracts.Messages;

/// <summary>
/// Plan 05-02 / D-36 — request published by the B2B portal (or a gateway-authenticated
/// controller) to ask the PricingService to apply the agency's markup rule to a
/// GDS-sourced NET fare.
/// </summary>
/// <remarks>
/// <para>
/// <c>AgencyId</c> is stamped server-side from the JWT <c>agency_id</c> claim
/// (Pitfall 28); the consumer NEVER trusts a value forged in the request body.
/// </para>
/// <para>
/// <c>RouteClass</c> selects the D-36 override row when present (e.g. <c>"J-BUSINESS"</c>);
/// the consumer falls back to the agency's base row (RouteClass == NULL) otherwise.
/// </para>
/// </remarks>
public sealed record AgencyPriceRequested(
    Guid CorrelationId,
    Guid AgencyId,
    string OfferId,
    decimal NetFare,
    string Currency,
    string? RouteClass);
