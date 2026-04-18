namespace TBE.BookingService.Application.Keycloak;

/// <summary>
/// Plan 05-04 Task 1 (B2B-09) — server-side facade over the Keycloak Admin
/// REST API (realm <c>tbe-b2b</c>) that resolves the e-mail addresses of
/// every user whose <c>agency_id</c> attribute equals a given agency AND
/// whose realm-role set intersects a configured allow-list (default
/// <c>agent-admin</c> and <c>agent</c>; <c>agent-readonly</c> is excluded
/// per plan).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a fresh interface in BookingService:</b> Plan 05-03 shipped the
/// analogous client inside PaymentService because the wallet low-balance
/// consumer lives there. The Plan 05-04 TTL-deadline consumer lives in
/// BookingService (the TTL monitor that publishes the events is in
/// BookingService.Infrastructure.Ttl), so we duplicate the shape here rather
/// than take a cross-service project reference. The two clients will stay in
/// sync manually; they're thin enough (~150 LOC) that duplication beats the
/// layering contortion of introducing a new shared "Keycloak" library.
/// </para>
/// <para>
/// <b>Anti-spoofing (T-05-04-07 analog):</b> implementations MUST intersect
/// the <c>q=agency_id:X&amp;exact=true</c> user-search result with the
/// configured role allow-list. A user added to an agency without a permitted
/// role never appears in the returned list.
/// </para>
/// </remarks>
public interface IKeycloakB2BAdminClient
{
    /// <summary>
    /// Return every user e-mail for the given agency whose realm-role set
    /// intersects the options'
    /// <see cref="KeycloakB2BAdminOptions.AllowedRoles"/> list. Empty list
    /// ⇒ no eligible recipients (consumer logs + returns).
    /// </summary>
    Task<IReadOnlyList<AgentContact>> GetAgentContactsForAgencyAsync(
        Guid agencyId, CancellationToken ct);
}

/// <summary>
/// A Keycloak user projection limited to what the TTL-deadline consumer
/// needs (e-mail + display name).
/// </summary>
public sealed record AgentContact(string Email, string DisplayName);
