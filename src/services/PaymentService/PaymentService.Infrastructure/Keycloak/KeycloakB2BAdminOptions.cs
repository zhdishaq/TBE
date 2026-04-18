namespace TBE.PaymentService.Infrastructure.Keycloak;

/// <summary>
/// Plan 05-03 Task 2 — configuration for the <c>KeycloakB2BAdminClient</c>
/// service account. Realm is fixed at <c>tbe-b2b</c> per T-05-03-11 (we
/// MUST NOT accidentally query the <c>tbe-b2c</c> realm for agent-admins).
/// </summary>
public sealed class KeycloakB2BAdminOptions
{
    /// <summary>Base URL of the Keycloak server (e.g. <c>http://keycloak:8080</c>).</summary>
    public string BaseUrl { get; set; } = "http://localhost:8080";

    /// <summary>Realm name — always <c>tbe-b2b</c> in production.</summary>
    public string Realm { get; set; } = "tbe-b2b";

    /// <summary>Client-credentials client id for the PaymentService service account.</summary>
    public string ClientId { get; set; } = "payment-service";

    /// <summary>Client-credentials secret for the PaymentService service account.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Name of the realm role that marks an agent admin. Default <c>agent-admin</c>.</summary>
    public string AgentAdminRole { get; set; } = "agent-admin";
}
