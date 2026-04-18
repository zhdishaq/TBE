namespace TBE.PaymentService.Application.Wallet;

/// <summary>
/// Plan 05-03 Task 2 — server-side facade over the Keycloak Admin REST API
/// (realm <c>tbe-b2b</c>) that resolves the e-mail addresses of every user
/// with the <c>agent-admin</c> role mapped to a given agency.
/// </summary>
/// <remarks>
/// T-05-03-11 (spoofing mitigation): the implementation MUST intersect the
/// <c>q=agency_id:X&amp;exact=true</c> result with the <c>agent-admin</c>
/// realm-role-mapping, so a user who was merely added to the agency without
/// the admin role never appears in the returned list.
/// </remarks>
public interface IKeycloakB2BAdminClient
{
    /// <summary>Return every agent-admin user's e-mail address for the given agency.</summary>
    Task<IReadOnlyList<AgentAdminContact>> GetAgentAdminsForAgencyAsync(Guid agencyId, CancellationToken ct);
}

/// <summary>A Keycloak user projection limited to what the low-balance
/// consumer needs (e-mail + display name). No agency_id — the consumer
/// already has it.</summary>
public sealed record AgentAdminContact(string Email, string DisplayName);
