using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TBE.PaymentService.Application.Wallet;

namespace TBE.PaymentService.Infrastructure.Keycloak;

/// <summary>
/// Plan 05-03 Task 2 — server-side facade over the Keycloak Admin REST API
/// (realm <c>tbe-b2b</c>). Resolves the e-mail addresses of every user with
/// the <c>agent-admin</c> role mapped to a given <c>agency_id</c> attribute.
/// </summary>
/// <remarks>
/// <para>
/// Token-caching strategy: a single service-account access_token is cached in
/// memory with a 30-second clock-skew margin to avoid lockstep token churn.
/// </para>
/// <para>
/// T-05-03-11 (spoofing mitigation): the recipient list is the intersection
/// of <c>q=agency_id:X&amp;exact=true</c> user search AND the realm
/// role-mapping <c>agent-admin</c>. A user who was merely added to the
/// agency without the admin role never appears in the returned list.
/// </para>
/// </remarks>
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

    public async Task<IReadOnlyList<AgentAdminContact>> GetAgentAdminsForAgencyAsync(
        Guid agencyId, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync(ct).ConfigureAwait(false);

        // 1) Users whose agency_id attribute matches.
        var usersUri =
            $"{_opt.BaseUrl.TrimEnd('/')}/admin/realms/{_opt.Realm}/users?q=agency_id:{agencyId}&exact=true";
        using var usersReq = new HttpRequestMessage(HttpMethod.Get, usersUri);
        usersReq.Headers.Authorization = new("Bearer", token);
        using var usersResp = await _http.SendAsync(usersReq, ct).ConfigureAwait(false);
        usersResp.EnsureSuccessStatusCode();
        var users = await usersResp.Content.ReadFromJsonAsync<List<KcUser>>(cancellationToken: ct).ConfigureAwait(false)
                    ?? new List<KcUser>();

        // 2) Intersect with agent-admin realm role (T-05-03-11).
        var contacts = new List<AgentAdminContact>(users.Count);
        foreach (var u in users)
        {
            if (string.IsNullOrWhiteSpace(u.Id) || string.IsNullOrWhiteSpace(u.Email))
            {
                continue;
            }

            var rolesUri =
                $"{_opt.BaseUrl.TrimEnd('/')}/admin/realms/{_opt.Realm}/users/{u.Id}/role-mappings/realm";
            using var rolesReq = new HttpRequestMessage(HttpMethod.Get, rolesUri);
            rolesReq.Headers.Authorization = new("Bearer", token);
            using var rolesResp = await _http.SendAsync(rolesReq, ct).ConfigureAwait(false);
            rolesResp.EnsureSuccessStatusCode();
            var roles = await rolesResp.Content.ReadFromJsonAsync<List<KcRole>>(cancellationToken: ct).ConfigureAwait(false)
                        ?? new List<KcRole>();

            if (roles.Any(r => string.Equals(r.Name, _opt.AgentAdminRole, StringComparison.Ordinal)))
            {
                contacts.Add(new AgentAdminContact(
                    u.Email!,
                    string.IsNullOrWhiteSpace(u.FirstName) ? u.Email! : $"{u.FirstName} {u.LastName}".Trim()));
            }
        }

        _log.LogInformation(
            "resolved {Count} agent-admin contact(s) agency={AgencyId} realm={Realm}",
            contacts.Count, agencyId, _opt.Realm);
        return contacts;
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        // 30-second clock-skew margin to avoid lockstep token churn.
        var now = _clock.GetUtcNow();
        if (_token is not null && _tokenExpiresAt - now > TimeSpan.FromSeconds(30))
        {
            return _token;
        }

        await _tokenLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            now = _clock.GetUtcNow();
            if (_token is not null && _tokenExpiresAt - now > TimeSpan.FromSeconds(30))
            {
                return _token;
            }

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
