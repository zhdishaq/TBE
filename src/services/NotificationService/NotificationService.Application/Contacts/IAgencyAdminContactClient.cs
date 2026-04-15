namespace TBE.NotificationService.Application.Contacts;

/// <summary>
/// Typed wrapper around <c>HttpClient</c> that fetches the agency admin's email for an
/// agency via <c>GET /api/agencies/{agencyId}/admin-contact</c>. Used by
/// <c>WalletLowBalanceConsumer</c> (NOTF-05).
/// </summary>
public interface IAgencyAdminContactClient
{
    Task<AgencyAdminContact?> GetAdminContactAsync(Guid agencyId, CancellationToken ct);
}

public sealed record AgencyAdminContact(string Email, string Name, string AgencyName);
