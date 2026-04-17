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
    private readonly IWalletRepository _wallet;
    private readonly IWalletTopUpService _topUp;
    private readonly ILogger<B2BWalletController> _log;

    public B2BWalletController(
        IWalletRepository wallet,
        IWalletTopUpService topUp,
        ILogger<B2BWalletController> log)
    {
        _wallet = wallet;
        _topUp = topUp;
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
    /// Reads the agency's configured low-balance alert threshold (future Task 2 consumer).
    /// Read-only — any authenticated B2B user can see their own threshold.
    /// </summary>
    [HttpGet("threshold")]
    public IActionResult GetThreshold()
    {
        if (!TryGetAgencyIdFromClaim(out _))
        {
            return Unauthorized(new { error = "agency_id claim missing or invalid" });
        }

        // Task 2 will persist per-agency thresholds on AgencyWallet. For now return the default.
        return Ok(new { threshold = 500m, currency = "GBP" });
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
