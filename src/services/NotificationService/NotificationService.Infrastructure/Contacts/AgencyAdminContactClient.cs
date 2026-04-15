using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using TBE.NotificationService.Application.Contacts;

namespace TBE.NotificationService.Infrastructure.Contacts;

/// <summary>
/// HTTP-backed <see cref="IAgencyAdminContactClient"/>. Reads from BookingService's
/// <c>GET /api/agencies/{agencyId}/admin-contact</c>.
/// </summary>
public sealed class AgencyAdminContactClient : IAgencyAdminContactClient
{
    private readonly HttpClient _http;
    private readonly ILogger<AgencyAdminContactClient> _log;

    public AgencyAdminContactClient(HttpClient http, ILogger<AgencyAdminContactClient> log)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task<AgencyAdminContact?> GetAdminContactAsync(Guid agencyId, CancellationToken ct)
    {
        try
        {
            using var resp = await _http.GetAsync($"api/agencies/{agencyId}/admin-contact", ct)
                .ConfigureAwait(false);
            if (resp.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.NoContent)
                return null;
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<AgencyAdminContact>(cancellationToken: ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            _log.LogWarning(ex, "BookingService agency-admin lookup failed for agency {AgencyId}", agencyId);
            return null;
        }
    }
}
