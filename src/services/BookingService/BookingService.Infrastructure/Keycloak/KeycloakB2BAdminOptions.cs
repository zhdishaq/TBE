namespace TBE.BookingService.Infrastructure.Keycloak;

/// <summary>
/// Plan 05-04 Task 1 (B2B-09) — configuration for the BookingService-side
/// <c>KeycloakB2BAdminClient</c> service account. Realm is fixed at
/// <c>tbe-b2b</c> (MUST NOT accidentally query <c>tbe-b2c</c> for
/// agent-admins / agents).
/// </summary>
public sealed class KeycloakB2BAdminOptions
{
    /// <summary>Base URL of the Keycloak server (e.g. <c>http://keycloak:8080</c>).</summary>
    public string BaseUrl { get; set; } = "http://localhost:8080";

    /// <summary>Realm name — always <c>tbe-b2b</c> in production.</summary>
    public string Realm { get; set; } = "tbe-b2b";

    /// <summary>Client-credentials client id for the BookingService service account.</summary>
    public string ClientId { get; set; } = "booking-service";

    /// <summary>Client-credentials secret for the BookingService service account.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Realm roles considered eligible recipients. Default is
    /// <c>agent-admin</c> + <c>agent</c>; <c>agent-readonly</c> is
    /// intentionally excluded per Plan 05-04 (read-only users shouldn't be
    /// paged about TTL expiry).
    /// </summary>
    public IList<string> AllowedRoles { get; set; } = new List<string> { "agent-admin", "agent" };
}
