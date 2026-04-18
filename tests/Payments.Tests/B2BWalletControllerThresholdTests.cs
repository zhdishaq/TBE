using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using NSubstitute;
using NSubstitute.ClearExtensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TBE.PaymentService.Application.Wallet;
using Xunit;

namespace Payments.Tests;

/// <summary>
/// Plan 05-05 Task 3 — B2BWalletController.UpdateThresholdAsync (PUT /api/wallet/threshold).
///
/// Facts cover (from 05-05-PLAN Task 3 behavior block):
///   1. Happy path — SetThresholdAsync called exactly once with JWT agency_id, 204 NoContent.
///   2. Pitfall 28 — body-supplied agencyId is ignored; JWT agency_id wins.
///   3. B2BAdminPolicy — agent-readonly gets 403.
///   4. Shape validation — non-positive threshold returns 400 problem+json.
///   5. Shape validation — bad currency (len != 3) returns 400 problem+json.
///   6. Server-side range guard — threshold &lt; 50 returns 400 problem+json.
///   7. Server-side range guard — threshold &gt; 10_000 returns 400 problem+json.
/// </summary>
public sealed class B2BWalletControllerThresholdTests
    : IClassFixture<B2BWalletThresholdTestFactory>
{
    private readonly B2BWalletThresholdTestFactory _factory;

    public B2BWalletControllerThresholdTests(B2BWalletThresholdTestFactory factory)
    {
        _factory = factory;
        _factory.AgencyWallets.ClearSubstitute(ClearOptions.All);
    }

    private HttpClient ClientFor(string role, Guid? agencyId = null)
    {
        var aid = agencyId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        client.DefaultRequestHeaders.Add("X-Test-AgencyId", aid.ToString());
        return client;
    }

    [Fact(DisplayName = "T-05-05-03: happy path — SetThresholdAsync called with JWT agency_id")]
    public async Task UpdateThreshold_happy_path_calls_SetThresholdAsync_with_JWT_agency_id()
    {
        var jwtAgency = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var client = ClientFor("agent-admin", agencyId: jwtAgency);

        var resp = await client.PutAsJsonAsync(
            "/api/wallet/threshold",
            new { thresholdAmount = 1000m, currency = "GBP" });

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await _factory.AgencyWallets.Received(1).SetThresholdAsync(
            jwtAgency, 1000m, "GBP", Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "T-05-05-03 / Pitfall 28: body-supplied agencyId is ignored — JWT wins")]
    public async Task UpdateThreshold_ignores_body_agency_id_Pitfall_28()
    {
        var jwtAgency = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var forgedAgency = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var client = ClientFor("agent-admin", agencyId: jwtAgency);

        // The forged `agencyId` on the body MUST be discarded — the controller
        // deliberately does not deserialise it into its DTO (Pitfall 28 codified
        // at the type level, not just at the handler).
        var resp = await client.PutAsJsonAsync(
            "/api/wallet/threshold",
            new { thresholdAmount = 750m, currency = "GBP", agencyId = forgedAgency });

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await _factory.AgencyWallets.Received(1).SetThresholdAsync(
            jwtAgency, 750m, "GBP", Arg.Any<CancellationToken>());
        await _factory.AgencyWallets.DidNotReceive().SetThresholdAsync(
            forgedAgency, Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "B2BAdminPolicy: non-admin (agent-readonly) gets 403 — repo never called")]
    public async Task UpdateThreshold_returns_403_when_role_is_not_agent_admin()
    {
        var client = ClientFor("agent-readonly");

        var resp = await client.PutAsJsonAsync(
            "/api/wallet/threshold",
            new { thresholdAmount = 1000m, currency = "GBP" });

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await _factory.AgencyWallets.DidNotReceiveWithAnyArgs().SetThresholdAsync(
            default, default, default!, default);
    }

    [Fact(DisplayName = "400 problem+json when thresholdAmount is non-positive")]
    public async Task UpdateThreshold_returns_400_problem_json_when_amount_is_non_positive()
    {
        var client = ClientFor("agent-admin");

        var resp = await client.PutAsJsonAsync(
            "/api/wallet/threshold",
            new { thresholdAmount = -50m, currency = "GBP" });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        resp.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("/errors/wallet-threshold-out-of-range");
        body.Should().Contain("\"requested\":-50");
        await _factory.AgencyWallets.DidNotReceiveWithAnyArgs().SetThresholdAsync(
            default, default, default!, default);
    }

    [Fact(DisplayName = "400 problem+json when currency is not ISO-4217 3-char")]
    public async Task UpdateThreshold_returns_400_when_currency_is_not_3_chars()
    {
        var client = ClientFor("agent-admin");

        var resp = await client.PutAsJsonAsync(
            "/api/wallet/threshold",
            new { thresholdAmount = 1000m, currency = "GB" });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        resp.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("/errors/wallet-threshold-out-of-range");
        await _factory.AgencyWallets.DidNotReceiveWithAnyArgs().SetThresholdAsync(
            default, default, default!, default);
    }

    [Fact(DisplayName = "D-40 parity: below-£50 returns 400 problem+json with allowedRange.min=50")]
    public async Task UpdateThreshold_returns_400_problem_json_when_amount_below_50()
    {
        var client = ClientFor("agent-admin");

        var resp = await client.PutAsJsonAsync(
            "/api/wallet/threshold",
            new { thresholdAmount = 49m, currency = "GBP" });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        resp.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("/errors/wallet-threshold-out-of-range");
        body.Should().Contain("\"min\":50");
        body.Should().Contain("\"requested\":49");
        await _factory.AgencyWallets.DidNotReceiveWithAnyArgs().SetThresholdAsync(
            default, default, default!, default);
    }

    [Fact(DisplayName = "D-40 parity: above-£10 000 returns 400 problem+json with allowedRange.max=10000")]
    public async Task UpdateThreshold_returns_400_problem_json_when_amount_above_10000()
    {
        var client = ClientFor("agent-admin");

        var resp = await client.PutAsJsonAsync(
            "/api/wallet/threshold",
            new { thresholdAmount = 10_001m, currency = "GBP" });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        resp.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("/errors/wallet-threshold-out-of-range");
        body.Should().Contain("\"max\":10000");
        body.Should().Contain("\"requested\":10001");
        await _factory.AgencyWallets.DidNotReceiveWithAnyArgs().SetThresholdAsync(
            default, default, default!, default);
    }
}

/// <summary>
/// Custom WebApplicationFactory pinned to the threshold-PUT fact set.
/// Swaps IAgencyWalletRepository + IWalletTopUpService for stubs; re-registers
/// the same B2BAdminPolicy Program.cs does so [Authorize(Policy="B2BAdminPolicy")]
/// resolves against the test auth scheme.
/// </summary>
public sealed class B2BWalletThresholdTestFactory
    : WebApplicationFactory<Program>
{
    public IAgencyWalletRepository AgencyWallets { get; } = Substitute.For<IAgencyWalletRepository>();
    public IWalletTopUpService TopUpService { get; } = Substitute.For<IWalletTopUpService>();

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(opts =>
                {
                    opts.DefaultAuthenticateScheme = "Test";
                    opts.DefaultChallengeScheme = "Test";
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });

            services.AddAuthorization(opt =>
            {
                opt.AddPolicy("B2BAdminPolicy", p =>
                    p.RequireAuthenticatedUser().RequireRole("agent-admin"));
                opt.AddPolicy("B2BPolicy", p =>
                    p.RequireAuthenticatedUser());
            });

            services.AddSingleton(AgencyWallets);
            services.AddSingleton(TopUpService);
        });
        return base.CreateHost(builder);
    }
}
