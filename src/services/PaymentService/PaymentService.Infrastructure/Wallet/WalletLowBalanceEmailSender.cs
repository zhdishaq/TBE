using Microsoft.Extensions.Logging;
using TBE.PaymentService.Application.Wallet;

namespace TBE.PaymentService.Infrastructure.Wallet;

/// <summary>
/// Plan 05-03 Task 2 — default in-process implementation of
/// <see cref="IWalletLowBalanceEmailSender"/>.
/// </summary>
/// <remarks>
/// <para>
/// Phase 5 MVP: logs the advisory per recipient and returns. The real
/// SendGrid transport sits behind the NotificationService queue (Phase
/// 03-04) and will be wired in a follow-up plan once the cross-service
/// contract for the B2B advisory template is approved. Until then we need
/// a non-throwing implementation so the consumer can still flip the
/// <c>LowBalanceEmailSent</c> flag deterministically in dev/test
/// environments — otherwise every poll tick would re-publish the detected
/// event forever (T-05-03-07).
/// </para>
/// <para>
/// Operators detect the stub by grepping logs for
/// <c>wallet low-balance advisory (stub)</c>.
/// </para>
/// </remarks>
public sealed class WalletLowBalanceEmailSender : IWalletLowBalanceEmailSender
{
    private readonly ILogger<WalletLowBalanceEmailSender> _log;

    public WalletLowBalanceEmailSender(ILogger<WalletLowBalanceEmailSender> log)
    {
        _log = log;
    }

    public Task SendLowBalanceEmailAsync(
        IReadOnlyList<AgentAdminContact> recipients,
        Guid agencyId,
        decimal balance,
        decimal threshold,
        string currency,
        CancellationToken ct)
    {
        foreach (var r in recipients)
        {
            _log.LogInformation(
                "wallet low-balance advisory (stub) to={Email} agency={AgencyId} balance={Balance} threshold={Threshold} currency={Currency}",
                r.Email, agencyId, balance, threshold, currency);
        }
        return Task.CompletedTask;
    }
}
