using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using TBE.PaymentService.Application.Stripe;
using TBE.PaymentService.Application.Wallet;
using Xunit;

namespace Payments.Tests;

/// <summary>
/// Plan 05-03 Task 1 — WalletController endpoints (top-up intent / threshold / transactions).
///
/// Asserts the D-40 cap rejection surfaces as application/problem+json (T-05-03-03),
/// the controller never trusts a body-supplied agencyId (T-05-03-01 / Pitfall 28),
/// and non-admin agents are 403'd from mutation endpoints (T-05-03-02).
/// </summary>
public sealed class WalletControllerTopUpTests : IClassFixture<WalletControllerTestFactory>
{
    private readonly WalletControllerTestFactory _factory;

    public WalletControllerTopUpTests(WalletControllerTestFactory factory) => _factory = factory;

    private HttpClient ClientFor(string role, Guid? agencyId = null)
    {
        var aid = agencyId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        client.DefaultRequestHeaders.Add("X-Test-AgencyId", aid.ToString());
        return client;
    }

    [Fact(DisplayName = "T-05-03-03: below-min returns 400 problem+json with /errors/wallet-topup-out-of-range")]
    public async Task Below_min_returns_problem_json()
    {
        _factory.TopUpService.CreateTopUpIntentAsync(default, default, default)
            .ReturnsForAnyArgs<TopUpIntentResult>(_ => throw new WalletTopUpOutOfRangeException(min: 10m, max: 50_000m, requested: 5m, currency: "GBP"));

        var client = ClientFor("agent-admin");
        var resp = await client.PostAsJsonAsync("/api/wallet/top-up/intent", new { amount = 5m });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        resp.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("/errors/wallet-topup-out-of-range");
        body.Should().Contain("Top-up amount out of range");
        body.Should().Contain("allowedRange");
    }

    [Fact(DisplayName = "T-05-03-03: above-max returns 400 problem+json")]
    public async Task Above_max_returns_problem_json()
    {
        _factory.TopUpService.CreateTopUpIntentAsync(default, default, default)
            .ReturnsForAnyArgs<TopUpIntentResult>(_ => throw new WalletTopUpOutOfRangeException(min: 10m, max: 50_000m, requested: 60_000m, currency: "GBP"));

        var client = ClientFor("agent-admin");
        var resp = await client.PostAsJsonAsync("/api/wallet/top-up/intent", new { amount = 60_000m });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        resp.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact(DisplayName = "D-40: in-range top-up returns 200 with clientSecret + paymentIntentId")]
    public async Task In_range_returns_clientSecret()
    {
        _factory.TopUpService.CreateTopUpIntentAsync(default, default, default)
            .ReturnsForAnyArgs(new TopUpIntentResult("pi_abc_secret_xyz", "pi_abc", 250m, "GBP"));

        var client = ClientFor("agent-admin");
        var resp = await client.PostAsJsonAsync("/api/wallet/top-up/intent", new { amount = 250m });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<CreateTopUpIntentResponse>();
        body!.ClientSecret.Should().Be("pi_abc_secret_xyz");
        body.PaymentIntentId.Should().Be("pi_abc");
        body.Amount.Should().Be(250m);
        body.Currency.Should().Be("GBP");
    }

    [Fact(DisplayName = "T-05-03-01: body-supplied agencyId is ignored — controller stamps from JWT claim")]
    public async Task Body_supplied_agencyId_is_ignored_JWT_wins()
    {
        var jwtAgency = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var forgedAgency = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        Guid? observedAgency = null;
        _factory.TopUpService.CreateTopUpIntentAsync(default, default, default)
            .ReturnsForAnyArgs(call =>
            {
                observedAgency = call.Arg<Guid>();
                return Task.FromResult(new TopUpIntentResult("pi_x", "pi_x", 100m, "GBP"));
            });

        var client = ClientFor("agent-admin", agencyId: jwtAgency);
        // Body deliberately includes a forged agencyId that the controller MUST drop.
        var resp = await client.PostAsJsonAsync("/api/wallet/top-up/intent",
            new { amount = 100m, agencyId = forgedAgency, AgencyId = forgedAgency });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        observedAgency.Should().Be(jwtAgency, "controller must use JWT agency_id (Pitfall 28), never trust body");
    }

    [Fact(DisplayName = "T-05-03-02: non-admin agent gets 403 from POST /top-up/intent")]
    public async Task NonAdmin_gets_403_on_topup_intent()
    {
        var client = ClientFor("agent");
        var resp = await client.PostAsJsonAsync("/api/wallet/top-up/intent", new { amount = 100m });
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "T-05-03-02: agent-readonly gets 403 from POST /top-up/intent")]
    public async Task Readonly_gets_403_on_topup_intent()
    {
        var client = ClientFor("agent-readonly");
        var resp = await client.PostAsJsonAsync("/api/wallet/top-up/intent", new { amount = 100m });
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

/// <summary>
/// Custom WebApplicationFactory that swaps in test auth + a stub WalletTopUpService.
/// Avoids real Stripe + real DB; we only verify the HTTP/auth/gate boundaries here.
/// The full DB-backed concurrency proof lives in <see cref="WalletConcurrencyTests"/>.
/// </summary>
public sealed class WalletControllerTestFactory : WebApplicationFactory<Program>
{
    public IWalletTopUpService TopUpService { get; } = Substitute.For<IWalletTopUpService>();

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            // Replace authentication with a header-based test scheme.
            services.AddAuthentication(opts =>
                {
                    opts.DefaultAuthenticateScheme = "Test";
                    opts.DefaultChallengeScheme = "Test";
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });

            // Re-register authorization with the same B2BAdminPolicy the production
            // Program uses, but pinned to the Test scheme so [Authorize(Policy=...)] resolves.
            services.AddAuthorization(opt =>
            {
                opt.AddPolicy("B2BAdminPolicy", p =>
                    p.RequireAuthenticatedUser().RequireRole("agent-admin"));
                opt.AddPolicy("B2BPolicy", p =>
                    p.RequireAuthenticatedUser());
            });

            services.AddSingleton(TopUpService);
        });
        return base.CreateHost(builder);
    }
}

/// <summary>
/// Test auth handler that reads X-Test-Role + X-Test-AgencyId headers and emits a
/// matching ClaimsPrincipal. Mirrors what JwtBearer would produce post-validation.
/// </summary>
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        Microsoft.Extensions.Logging.ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-Role", out var role))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var agency = Request.Headers.TryGetValue("X-Test-AgencyId", out var aid)
            ? aid.ToString()
            : Guid.NewGuid().ToString();

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "test-user"),
            new Claim(ClaimTypes.Role, role.ToString()),
            new Claim("agency_id", agency),
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public sealed record CreateTopUpIntentResponse(string ClientSecret, string PaymentIntentId, decimal Amount, string Currency);
