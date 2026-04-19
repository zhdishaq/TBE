using System.ComponentModel.DataAnnotations;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TBE.Contracts.Events;
using TBE.PaymentService.Application.Wallet;
using TBE.PaymentService.Infrastructure;

namespace TBE.PaymentService.API.Controllers;

/// <summary>
/// Plan 06-04 Task 2 / CRM-02 / D-61 / T-6-59 — sole mutation surface
/// for <c>payment.AgencyWallets.CreditLimit</c>. Pinned to the
/// Backoffice JWT scheme + <c>BackofficeFinancePolicy</c> (ops-finance
/// or ops-admin) so a B2B / B2C token can never raise its own credit
/// limit (Pitfall 4).
///
/// <para>
/// Every PATCH writes an audit row to <c>payment.CreditLimitAuditLog</c>
/// and publishes an <c>AgencyCreditLimitChanged</c> envelope via the EF
/// outbox (atomic with the PATCH transaction) — gives both local and
/// fan-out non-repudiation per the phase-6 threat register.
/// </para>
///
/// <para>
/// Problem+json type URIs:
/// <list type="bullet">
///   <item>/errors/credit-limit-out-of-range (400)</item>
///   <item>/errors/credit-limit-reason-required (400)</item>
///   <item>/errors/agency-wallet-not-found (404)</item>
///   <item>/errors/auth-missing-actor (401)</item>
/// </list>
/// </para>
/// </summary>
[ApiController]
[Route("api/payments/agencies/{agencyId:guid}/credit-limit")]
[Authorize(Policy = "BackofficeFinancePolicy", AuthenticationSchemes = "Backoffice")]
public sealed class AgencyCreditLimitController : ControllerBase
{
    /// <summary>
    /// Business upper bound for the credit limit. Overrideable via the
    /// <c>Wallet:MaxCreditLimit</c> config key so finance can raise the
    /// ceiling without a redeploy.
    /// </summary>
    public const decimal DefaultMaxCreditLimit = 100_000m;

    private readonly PaymentDbContext _db;
    private readonly IPublishEndpoint _publish;
    private readonly IConfiguration _config;
    private readonly ILogger<AgencyCreditLimitController> _log;

    public AgencyCreditLimitController(
        PaymentDbContext db,
        IPublishEndpoint publish,
        IConfiguration config,
        ILogger<AgencyCreditLimitController> log)
    {
        _db = db;
        _publish = publish;
        _config = config;
        _log = log;
    }

    public sealed record PatchCreditLimitRequest(
        [property: Required] decimal CreditLimit,
        [property: Required, MinLength(10), MaxLength(500)] string Reason);

    [HttpPatch("")]
    public async Task<IActionResult> Patch(
        Guid agencyId,
        [FromBody] PatchCreditLimitRequest req,
        CancellationToken ct)
    {
        if (req is null)
        {
            return Problem(
                title: "Request body required",
                type: "/errors/credit-limit-out-of-range",
                statusCode: 400);
        }
        if (req.CreditLimit < 0m)
        {
            return Problem(
                title: "CreditLimit must be >= 0",
                type: "/errors/credit-limit-out-of-range",
                statusCode: 400);
        }

        var maxLimit = _config.GetValue<decimal?>("Wallet:MaxCreditLimit") ?? DefaultMaxCreditLimit;
        if (req.CreditLimit > maxLimit)
        {
            return Problem(
                title: $"CreditLimit exceeds business threshold ({maxLimit:0.00})",
                type: "/errors/credit-limit-out-of-range",
                statusCode: 400);
        }

        if (string.IsNullOrWhiteSpace(req.Reason) || req.Reason.Trim().Length < 10)
        {
            return Problem(
                title: "Reason must be at least 10 characters",
                type: "/errors/credit-limit-reason-required",
                statusCode: 400);
        }
        if (req.Reason.Length > 500)
        {
            return Problem(
                title: "Reason exceeds 500 characters",
                type: "/errors/credit-limit-reason-required",
                statusCode: 400);
        }

        // Pitfall 28 — actor must come from preferred_username; fail-closed
        // if the claim is absent so a token without it can't anonymously
        // raise credit limits.
        var actor = User.FindFirst("preferred_username")?.Value;
        if (string.IsNullOrWhiteSpace(actor))
        {
            return Problem(
                title: "preferred_username claim missing",
                type: "/errors/auth-missing-actor",
                statusCode: 401);
        }

        var wallet = await _db.AgencyWallets.FirstOrDefaultAsync(w => w.AgencyId == agencyId, ct);
        if (wallet is null)
        {
            return Problem(
                title: $"No AgencyWallet row exists for agency {agencyId}",
                type: "/errors/agency-wallet-not-found",
                statusCode: 404);
        }

        var old = wallet.CreditLimit;
        wallet.CreditLimit = req.CreditLimit;
        wallet.UpdatedAtUtc = DateTime.UtcNow;

        _db.CreditLimitAuditLog.Add(new CreditLimitAuditLogRow
        {
            AgencyId = agencyId,
            OldLimit = old,
            NewLimit = req.CreditLimit,
            ChangedBy = actor,
            Reason = req.Reason.Trim(),
            ChangedAtUtc = DateTime.UtcNow,
        });

        // Atomic outbox publish — MassTransit EF outbox commits the
        // AgencyCreditLimitChanged envelope in the same tx as the wallet
        // UPDATE + audit INSERT (Plan 03-01 pattern; no half-state).
        await _publish.Publish(new AgencyCreditLimitChanged(
            agencyId, old, req.CreditLimit, actor, req.Reason.Trim(), DateTime.UtcNow), ct);

        await _db.SaveChangesAsync(ct);

        _log.LogInformation(
            "credit limit patched agency={AgencyId} from={OldLimit} to={NewLimit} by={Actor}",
            agencyId, old, req.CreditLimit, actor);

        return NoContent();
    }
}
