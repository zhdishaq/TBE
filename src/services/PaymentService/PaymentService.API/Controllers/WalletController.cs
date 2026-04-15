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
