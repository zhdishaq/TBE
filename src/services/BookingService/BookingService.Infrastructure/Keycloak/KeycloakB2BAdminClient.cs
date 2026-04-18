using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TBE.BookingService.Application.Keycloak;

namespace TBE.BookingService.Infrastructure.Keycloak;

/// <summary>
/// Plan 05-04 Task 1 (B2B-09) — BookingService-side facade over the Keycloak
/// Admin REST API (realm <c>tbe-b2b</c>). Resolves e-mail addresses of every
/// user whose <c>agency_id</c> attribute matches AND whose realm-role set
/// intersects <see cref="KeycloakB2BAdminOptions.AllowedRoles"/>.
///
/// <para>
/// Duplicates <c>TBE.PaymentService.Infrastructure.Keycloak.KeycloakB2BAdminClient</c>
/// shape with a broader role allow-list (agent-admin + agent, not just
/// agent-admin). See <see cref="IKeycloakB2BAdminClient"/> for rationale
/// on the copy vs. shared-library trade.
/// </para>
///
/// <para>
/// Token caching: single in-memory access_token with a 30-second clock-skew
/// margin. T-05-04-07 analog: the recipient list is always an intersection
/// of fresh Keycloak search + fresh role-mapping lookups, never cached
/// per-agency.
/// </para>
/// </summary>
public sealed class KeycloakB2BAdminClient : IKeycloakB2BAdminClient
{
    private readonly HttpClient _http;
    private readonly KeycloakB2BAdminOptions _opt;
    private readonly TimeProvider _clock;
    private readonly ILogger<KeycloakB2BAdminClient> _log;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _token;
    private DateTimeOffset _tokenExpiresAt;

    public KeycloakB2BAdminClient(
        HttpClient http,
        IOptions<KeycloakB2BAdminOptions> opt,
        TimeProvider clock,
        ILogger<KeycloakB2BAdminClient> log)
    {
        _http = http;
        _opt = opt.Value;
        _clock = clock;
        _log = log;
    }

    public async Task<IReadOnlyList<AgentContact>> GetAgentContactsForAgencyAsync(
        Guid agencyId, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync(ct).ConfigureAwait(false);

        var usersUri =
            $"{_opt.BaseUrl.TrimEnd('/')}/admin/realms/{_opt.Realm}/users?q=agency_id:{agencyId}&exact=true";
        using var usersReq = new HttpRequestMessage(HttpMethod.Get, usersUri);
        usersReq.Headers.Authorization = new("Bearer", token);
        using var usersResp = await _http.SendAsync(usersReq, ct).ConfigureAwait(false);
        usersResp.EnsureSuccessStatusCode();
        var users = await usersResp.Content.ReadFromJsonAsync<List<KcUser>>(cancellationToken: ct).ConfigureAwait(false)
                    ?? new List<KcUser>();

        var allowedRoles = new HashSet<string>(_opt.AllowedRoles, StringComparer.Ordinal);
        var contacts = new List<AgentContact>(users.Count);

        foreach (var u in users)
        {
            if (string.IsNullOrWhiteSpace(u.Id) || string.IsNullOrWhiteSpace(u.Email))
                continue;

            var rolesUri =
                $"{_opt.BaseUrl.TrimEnd('/')}/admin/realms/{_opt.Realm}/users/{u.Id}/role-mappings/realm";
            using var rolesReq = new HttpRequestMessage(HttpMethod.Get, rolesUri);
            rolesReq.Headers.Authorization = new("Bearer", token);
            using var rolesResp = await _http.SendAsync(rolesReq, ct).ConfigureAwait(false);
            rolesResp.EnsureSuccessStatusCode();
            var roles = await rolesResp.Content.ReadFromJsonAsync<List<KcRole>>(cancellationToken: ct).ConfigureAwait(false)
                        ?? new List<KcRole>();

            if (roles.Any(r => r.Name is not null && allowedRoles.Contains(r.Name)))
            {
                contacts.Add(new AgentContact(
                    u.Email!,
                    string.IsNullOrWhiteSpace(u.FirstName) ? u.Email! : $"{u.FirstName} {u.LastName}".Trim()));
            }
        }

        _log.LogInformation(
            "resolved {Count} agent contact(s) agency={AgencyId} realm={Realm} allowedRoles={Roles}",
            contacts.Count, agencyId, _opt.Realm, string.Join(",", _opt.AllowedRoles));
        return contacts;
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        var now = _clock.GetUtcNow();
        if (_token is not null && _tokenExpiresAt - now > TimeSpan.FromSeconds(30))
            return _token;

        await _tokenLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            now = _clock.GetUtcNow();
            if (_token is not null && _tokenExpiresAt - now > TimeSpan.FromSeconds(30))
                return _token;

            var tokenUri = $"{_opt.BaseUrl.TrimEnd('/')}/realms/{_opt.Realm}/protocol/openid-connect/token";
            using var req = new HttpRequestMessage(HttpMethod.Post, tokenUri)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = _opt.ClientId,
                    ["client_secret"] = _opt.ClientSecret,
                }),
            };
            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            var body = await resp.Content.ReadFromJsonAsync<KcTokenResponse>(cancellationToken: ct).ConfigureAwait(false)
                       ?? throw new InvalidOperationException("Keycloak returned an empty token response");

            _token = body.AccessToken;
            _tokenExpiresAt = now.AddSeconds(body.ExpiresIn);
            return _token!;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private sealed record KcUser(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("firstName")] string? FirstName,
        [property: JsonPropertyName("lastName")] string? LastName);

    private sealed record KcRole(
        [property: JsonPropertyName("name")] string? Name);

    private sealed record KcTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
