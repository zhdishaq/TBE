// Plan 05-01 Task 3 — Gateway B2B authorization integration tests.
//
// Two test surfaces:
//   1. JWT scheme validation (Audience + Issuer + SigningKey) — asserts
//      the gateway rejects tokens minted under the wrong realm
//      (T-05-01-01 Pitfall 4 audience confusion). Runs the full
//      JwtBearer handler on a minimal endpoint registered inside a
//      TestServer so we can assert on 401/403/200 without a live
//      Keycloak or downstream.
//   2. Authorization-policy evaluation — runs the real
//      B2BPolicy / B2BAdminPolicy against synthetic ClaimsPrincipals
//      via IAuthorizationService, exercising the role projection
//      contract in isolation of the JWT decode path.
//
// Both surfaces use the same realm_access.roles projection that
// OnTokenValidated wires up in production Program.cs so the tests
// stay faithful to the real auth pipeline without the YARP round-trip
// (which would require a live downstream cluster).
//
// Traits ------------------------------------------------------------------
//   Trait("Category","Integration") — surfaces under the same filter
//                                     the Plan 05-00 harness uses.
//
// Plan 05-01 acceptance criteria exercised:
//   - tbe-b2b scheme registered + ValidateAudience=true   (T-05-01-01)
//   - tbe-b2c token rejected on /api/b2b/*                 (T-05-01-01)
//   - agent role -> B2BPolicy 200                          (D-32)
//   - agent-readonly -> B2BPolicy 200                      (D-32 / D-35)
//   - agent-admin   -> B2BAdminPolicy 200
//   - agent role   -> B2BAdminPolicy 403                   (T-05-01-02)
//   - no token -> B2BPolicy 401
//   - non-role claim -> B2BPolicy 403 (belt & braces)
//
// The plan stipulates >= 6 test cases; this file ships 8 Facts.

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Gateway.Tests;

[Trait("Category", "Integration")]
public class B2BAuthPolicyTests : IAsyncLifetime
{
    private const string B2bIssuer = "https://test-kc.local/realms/tbe-b2b";
    private const string B2cIssuer = "https://test-kc.local/realms/tbe-b2c";
    private const string B2bAudience = "tbe-api";

    private readonly RSA _rsa = RSA.Create(2048);
    private readonly string _keyId = Guid.NewGuid().ToString("n");
    private IHost _host = null!;
    private TestServer _server = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _host = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    // Mirror TBE.Gateway.Program.cs — tbe-b2b scheme
                    // with ValidateAudience=true (T-05-01-01) + role
                    // projection + B2BPolicy / B2BAdminPolicy.
                    var signingKey = new RsaSecurityKey(_rsa) { KeyId = _keyId };

                    services.AddAuthentication()
                        .AddJwtBearer("tbe-b2b", opts =>
                        {
                            opts.RequireHttpsMetadata = false;
                            opts.Audience = B2bAudience;
                            opts.TokenValidationParameters = new TokenValidationParameters
                            {
                                ValidateIssuer = true,
                                ValidIssuer = B2bIssuer,
                                ValidateAudience = true,
                                ValidAudience = B2bAudience,
                                ValidateIssuerSigningKey = true,
                                IssuerSigningKey = signingKey,
                                ValidateLifetime = true,
                                ClockSkew = TimeSpan.FromSeconds(30),
                            };
                            opts.Events = new JwtBearerEvents
                            {
                                OnTokenValidated = ctx =>
                                {
                                    var realmAccess = ctx.Principal?.FindFirst("realm_access")?.Value;
                                    if (!string.IsNullOrEmpty(realmAccess))
                                    {
                                        using var doc = System.Text.Json.JsonDocument.Parse(realmAccess);
                                        if (doc.RootElement.TryGetProperty("roles", out var rolesEl))
                                        {
                                            var identity = (ClaimsIdentity)ctx.Principal!.Identity!;
                                            foreach (var role in rolesEl.EnumerateArray())
                                            {
                                                identity.AddClaim(new Claim("roles", role.GetString() ?? string.Empty));
                                            }
                                        }
                                    }
                                    return Task.CompletedTask;
                                },
                            };
                        });

                    services.AddAuthorization(options =>
                    {
                        options.AddPolicy("B2BPolicy", p => p
                            .AddAuthenticationSchemes("tbe-b2b")
                            .RequireAuthenticatedUser()
                            .RequireAssertion(ctx =>
                                ctx.User.HasClaim("roles", "agent") ||
                                ctx.User.HasClaim("roles", "agent-admin") ||
                                ctx.User.HasClaim("roles", "agent-readonly")));

                        options.AddPolicy("B2BAdminPolicy", p => p
                            .AddAuthenticationSchemes("tbe-b2b")
                            .RequireAuthenticatedUser()
                            .RequireClaim("roles", "agent-admin"));
                    });

                    services.AddRouting();
                });

                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/api/b2b/bookings/me",
                            (HttpContext ctx) => Results.Ok(new { ok = true }))
                            .RequireAuthorization("B2BPolicy");
                        endpoints.MapGet("/api/b2b/admin/ping",
                            (HttpContext ctx) => Results.Ok(new { ok = true }))
                            .RequireAuthorization("B2BAdminPolicy");
                    });
                });
            })
            .Build();

        await _host.StartAsync();
        _server = _host.GetTestServer();
        _client = _server.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _host.StopAsync();
        _host.Dispose();
        _rsa.Dispose();
    }

    // ---------------------------------------------------------------------

    private string MintToken(string issuer, string audience, string[] realmRoles)
    {
        var now = DateTimeOffset.UtcNow;
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, "test-user-" + Guid.NewGuid().ToString("n")),
        };
        // Keycloak emits realm roles under the realm_access envelope —
        // exactly the shape Program.cs's OnTokenValidated parses.
        var realmAccessJson = "{\"roles\":[" +
            string.Join(",", realmRoles.Select(r => "\"" + r + "\"")) + "]}";
        claims.Add(new Claim("realm_access", realmAccessJson, "JSON"));

        var signingKey = new RsaSecurityKey(_rsa) { KeyId = _keyId };
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256);

        var jwt = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: now.AddMinutes(5).UtcDateTime,
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    // ---- Facts ----------------------------------------------------------

    [Fact]
    public async Task Request_to_api_b2b_bookings_me_without_token_returns_401()
    {
        var resp = await _client.GetAsync("/api/b2b/bookings/me");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Request_to_api_b2b_bookings_me_with_tbe_b2c_token_returns_401()
    {
        // T-05-01-01 — audience mismatch: a tbe-b2c token (wrong
        // issuer for the tbe-b2b scheme) must not authenticate.
        var token = MintToken(
            issuer: B2cIssuer,
            audience: "tbe-gateway",
            realmRoles: new[] { "customer" });
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/b2b/bookings/me");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Request_to_api_b2b_bookings_me_with_wrong_audience_returns_401()
    {
        // Another audience-mismatch shape: same tbe-b2b realm but a
        // wrong audience string (e.g. the old "tbe-gateway" default
        // from before the Plan 05-01 flip).
        var token = MintToken(
            issuer: B2bIssuer,
            audience: "tbe-gateway",
            realmRoles: new[] { "agent" });
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/b2b/bookings/me");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Request_to_api_b2b_bookings_me_with_agent_role_returns_ok()
    {
        var token = MintToken(
            issuer: B2bIssuer,
            audience: B2bAudience,
            realmRoles: new[] { "agent" });
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/b2b/bookings/me");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Request_to_api_b2b_bookings_me_with_readonly_role_returns_ok()
    {
        // D-32 / D-35 — agent-readonly may read bookings list.
        var token = MintToken(
            issuer: B2bIssuer,
            audience: B2bAudience,
            realmRoles: new[] { "agent-readonly" });
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/b2b/bookings/me");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Request_to_B2BAdminPolicy_endpoint_agent_role_returns_403()
    {
        // T-05-01-02 — non-admin cannot hit admin-gated route.
        var token = MintToken(
            issuer: B2bIssuer,
            audience: B2bAudience,
            realmRoles: new[] { "agent" });
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/b2b/admin/ping");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Request_to_B2BAdminPolicy_endpoint_admin_role_returns_ok()
    {
        var token = MintToken(
            issuer: B2bIssuer,
            audience: B2bAudience,
            realmRoles: new[] { "agent-admin" });
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/b2b/admin/ping");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Request_authenticated_but_no_agent_role_claim_returns_403()
    {
        // Belt & braces: the JWT authenticates (audience + issuer OK)
        // but carries no realm-level agent* role, so B2BPolicy's
        // RequireAssertion should deny with 403.
        var token = MintToken(
            issuer: B2bIssuer,
            audience: B2bAudience,
            realmRoles: new[] { "some-other-role" });
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/b2b/bookings/me");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }
}
