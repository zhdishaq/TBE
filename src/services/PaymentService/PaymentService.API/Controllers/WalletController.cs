using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TBE.PaymentService.Application.Stripe;
using TBE.PaymentService.Application.Wallet;

namespace TBE.PaymentService.API.Controllers;

[ApiController]
[Route("wallets")]
[Authorize]
public sealed class WalletController : ControllerBase
{
    private readonly IWalletRepository _wallet;
    private readonly IStripePaymentGateway _stripe;
    private readonly ILogger<WalletController> _log;

    public WalletController(
        IWalletRepository wallet,
        IStripePaymentGateway stripe,
        ILogger<WalletController> log)
    {
        _wallet = wallet;
        _stripe = stripe;
        _log = log;
    }

    [HttpGet("{walletId:guid}/balance")]
    public async Task<IActionResult> GetBalance(Guid walletId, CancellationToken ct)
    {
        var balance = await _wallet.GetBalanceAsync(walletId, ct);
        return Ok(new { walletId, balance, currency = "GBP", threshold = 500m });
    }

    [HttpGet("{walletId:guid}/transactions")]
    public async Task<IActionResult> List(
        Guid walletId,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int size = 50,
        CancellationToken ct = default)
    {
        var items = await _wallet.ListAsync(walletId, from, to, page, size, ct);
        return Ok(items);
    }

    [HttpPost("{walletId:guid}/top-ups")]
    [Authorize(Roles = "agency-admin")]
    public async Task<IActionResult> TopUp(Guid walletId, [FromBody] TopUpRequest req, CancellationToken ct)
    {
        var agencyClaim = User.FindFirst("agency_id")?.Value;
        if (!Guid.TryParse(agencyClaim, out var agencyId))
        {
            return BadRequest(new { error = "agency_id claim missing or invalid" });
        }

        var r = await _stripe.CreateWalletTopUpAsync(
            walletId, agencyId, req.AmountCents, req.Currency,
            req.StripeCustomerId, req.PaymentMethodId, ct);

        _log.LogInformation("wallet {WalletId} top-up PaymentIntent {Pi} created for agency {AgencyId}",
            walletId, r.PaymentIntentId, agencyId);

        return Accepted(new { paymentIntentId = r.PaymentIntentId, walletId });
    }
}

public sealed record TopUpRequest(long AmountCents, string Currency, string StripeCustomerId, string PaymentMethodId);

/// <summary>
/// Plan 05-03 — B2B-portal-facing wallet surface. Endpoints are scoped under
/// <c>/api/wallet/*</c> (singular) to match the Next.js App-Router routes the portal
/// calls. The agency/wallet id is ALWAYS derived from the JWT <c>agency_id</c> claim —
/// never from the request body (Pitfall 28).
/// </summary>
[ApiController]
[Route("api/wallet")]
[Authorize(Policy = "B2BPolicy")]
public sealed class B2BWalletController : ControllerBase
{
    // D-40 parity: threshold alert range matches the top-up cap policy (£50-£10,000).
    // Enforced server-side — client-side is UX hint only (T-05-05-03).
    internal const decimal MinThreshold = 50m;
    internal const decimal MaxThreshold = 10_000m;

    private readonly IWalletRepository _wallet;
    private readonly IWalletTopUpService _topUp;
    private readonly IAgencyWalletRepository _agencyWallets;
    private readonly ILogger<B2BWalletController> _log;

    public B2BWalletController(
        IWalletRepository wallet,
        IWalletTopUpService topUp,
        IAgencyWalletRepository agencyWallets,
        ILogger<B2BWalletController> log)
    {
        _wallet = wallet;
        _topUp = topUp;
        _agencyWallets = agencyWallets;
        _log = log;
    }

    /// <summary>
    /// D-40 top-up intent. Agent-admin only. Body carries only <c>amount</c>;
    /// any <c>agencyId</c> supplied in the payload is discarded.
    /// </summary>
    [HttpPost("top-up/intent")]
    [Authorize(Policy = "B2BAdminPolicy")]
    [Produces("application/json")]
    public async Task<IActionResult> CreateTopUpIntent(
        [FromBody] CreateTopUpIntentRequest req,
        CancellationToken ct)
    {
        if (!TryGetAgencyIdFromClaim(out var agencyId))
        {
            return Unauthorized(new { error = "agency_id claim missing or invalid" });
        }

        try
        {
            var r = await _topUp.CreateTopUpIntentAsync(agencyId, req.Amount, ct);
            return Ok(new CreateTopUpIntentResponse(
                ClientSecret: r.ClientSecret,
                PaymentIntentId: r.PaymentIntentId,
                Amount: r.Amount,
                Currency: r.Currency));
        }
        catch (WalletTopUpOutOfRangeException ex)
        {
            // RFC 7807 problem+json with a stable type URI so the portal can branch on error type.
            // We serialize ourselves (rather than returning ObjectResult<ProblemDetails>) so the
            // Content-Type header is pinned to application/problem+json regardless of the active
            // output formatter configuration.
            var problem = new
            {
                type = "/errors/wallet-topup-out-of-range",
                title = "Top-up amount out of range",
                status = StatusCodes.Status400BadRequest,
                detail = $"Requested {ex.Requested:N2} {ex.Currency} is outside allowed range.",
                allowedRange = new { min = ex.Min, max = ex.Max, currency = ex.Currency },
                requested = ex.Requested,
            };
            var json = System.Text.Json.JsonSerializer.Serialize(problem);
            return new ContentResult
            {
                Content = json,
                ContentType = "application/problem+json",
                StatusCode = StatusCodes.Status400BadRequest,
            };
        }
    }

    /// <summary>
    /// Reads the agency's configured low-balance alert threshold. Read-only —
    /// any authenticated B2B user can see their own threshold. Falls back to
    /// the configured default (500 GBP) when the agency has no row yet.
    /// </summary>
    [HttpGet("threshold")]
    public async Task<IActionResult> GetThreshold(CancellationToken ct)
    {
        if (!TryGetAgencyIdFromClaim(out var agencyId))
        {
            return Unauthorized(new { error = "agency_id claim missing or invalid" });
        }

        var row = await _agencyWallets.GetAsync(agencyId, ct);
        return Ok(new
        {
            threshold = row?.LowBalanceThresholdAmount ?? 500m,
            currency = row?.Currency ?? "GBP",
        });
    }

    /// <summary>
    /// 05-05 Task 3 — PUT /api/wallet/threshold. Agent-admin only. JWT agency_id is
    /// authoritative (Pitfall 28 — the DTO has no agencyId property so any client-
    /// supplied value is structurally discarded). D-40 parity server-side range
    /// guard £50-£10,000; violations return <c>application/problem+json</c> with
    /// type URI <c>/errors/wallet-threshold-out-of-range</c>. Delegates to
    /// <see cref="IAgencyWalletRepository.SetThresholdAsync"/> which also re-arms
    /// the low-balance email via hysteresis.
    /// </summary>
    [HttpPut("threshold")]
    [Authorize(Policy = "B2BAdminPolicy")]
    public async Task<IActionResult> UpdateThresholdAsync(
        [FromBody] UpdateThresholdRequest req,
        CancellationToken ct)
    {
        if (!TryGetAgencyIdFromClaim(out var agencyId))
        {
            return Unauthorized(new { error = "agency_id claim missing or invalid" });
        }

        // Shape validation — non-positive amount OR non-ISO-4217 3-char currency.
        // We fold shape failures into the same problem+json payload as range
        // failures so the threshold dialog has a single error-rendering branch.
        var currency = req.Currency ?? string.Empty;
        if (req.ThresholdAmount <= 0m || currency.Length != 3)
        {
            return OutOfRangeProblem(req.ThresholdAmount, currency);
        }

        // D-40 parity range guard.
        if (req.ThresholdAmount < MinThreshold || req.ThresholdAmount > MaxThreshold)
        {
            return OutOfRangeProblem(req.ThresholdAmount, currency);
        }

        await _agencyWallets.SetThresholdAsync(agencyId, req.ThresholdAmount, currency, ct);

        _log.LogInformation(
            "agency {AgencyId} updated low-balance threshold to {Threshold} {Currency}",
            agencyId, req.ThresholdAmount, currency);

        return NoContent();
    }

    /// <summary>
    /// Emit RFC-7807 problem+json for threshold shape/range violations. We
    /// serialise by hand (rather than returning <c>ObjectResult&lt;ProblemDetails&gt;</c>)
    /// so the Content-Type header is pinned to <c>application/problem+json</c>
    /// independent of the active output-formatter configuration — this matches
    /// the T-05-03-03 top-up intent contract.
    /// </summary>
    private ContentResult OutOfRangeProblem(decimal requested, string currency)
    {
        var effectiveCurrency = string.IsNullOrWhiteSpace(currency) ? "GBP" : currency;
        var problem = new
        {
            type = "/errors/wallet-threshold-out-of-range",
            title = "Threshold amount out of range",
            status = StatusCodes.Status400BadRequest,
            detail = $"Requested {requested:N2} {effectiveCurrency} is outside allowed range [{MinThreshold:N2}, {MaxThreshold:N2}].",
            allowedRange = new { min = MinThreshold, max = MaxThreshold, currency = effectiveCurrency },
            requested,
        };
        var json = System.Text.Json.JsonSerializer.Serialize(problem);
        return new ContentResult
        {
            Content = json,
            ContentType = "application/problem+json",
            StatusCode = StatusCodes.Status400BadRequest,
        };
    }

    /// <summary>
    /// Lists wallet ledger rows for the caller's agency. Agent-admin only.
    /// </summary>
    [HttpGet("transactions")]
    [Authorize(Policy = "B2BAdminPolicy")]
    public async Task<IActionResult> ListTransactions(
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int size = 50,
        CancellationToken ct = default)
    {
        if (!TryGetAgencyIdFromClaim(out var agencyId))
        {
            return Unauthorized(new { error = "agency_id claim missing or invalid" });
        }

        var items = await _wallet.ListAsync(agencyId, from, to, page, size, ct);
        return Ok(new { items });
    }

    private bool TryGetAgencyIdFromClaim(out Guid agencyId)
    {
        agencyId = Guid.Empty;
        var raw = User.FindFirst("agency_id")?.Value;
        return Guid.TryParse(raw, out agencyId);
    }
}

/// <summary>
/// Request body for <c>POST /api/wallet/top-up/intent</c>. Any <c>agencyId</c> supplied
/// here is deliberately NOT deserialized — the controller derives it from the JWT
/// (Pitfall 28). Amount is in major units (e.g. £250.00 → 250m).
/// </summary>
public sealed record CreateTopUpIntentRequest(decimal Amount);

/// <summary>
/// Response for <c>POST /api/wallet/top-up/intent</c>. Browser passes
/// <see cref="ClientSecret"/> to <c>stripe.confirmPayment</c>.
/// </summary>
public sealed record CreateTopUpIntentResponse(
    string ClientSecret,
    string PaymentIntentId,
    decimal Amount,
    string Currency);

/// <summary>
/// Request body for <c>PUT /api/wallet/threshold</c>. Deliberately omits
/// <c>agencyId</c> — the controller derives it from the JWT <c>agency_id</c>
/// claim (Pitfall 28). Any body-supplied agency id is structurally discarded
/// because this DTO has no such property to bind to.
/// </summary>
public sealed record UpdateThresholdRequest(decimal ThresholdAmount, string Currency);
