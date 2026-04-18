using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TBE.Contracts.Messages;

namespace TBE.PaymentService.Application.Wallet;

/// <summary>
/// Plan 05-03 Task 2 — consumes <see cref="WalletLowBalanceDetected"/> from
/// <c>WalletLowBalanceMonitor</c>, resolves the agency's agent-admin contacts
/// via <see cref="IKeycloakB2BAdminClient"/>, dispatches the advisory e-mail
/// through <see cref="IWalletLowBalanceEmailSender"/>, and flips
/// <c>AgencyWallets.LowBalanceEmailSent = 1</c> so the monitor stops
/// re-publishing for this agency until <c>WalletTopUpService</c> resets the
/// flag on balance-cross-up.
/// </summary>
/// <remarks>
/// <para>
/// Cooldown (T-05-03-07 defence-in-depth): if
/// <see cref="AgencyWallet.LastLowBalanceEmailAtUtc"/> is within
/// <c>WalletOptions.LowBalance.EmailCooldownHours</c> of the consumer's
/// <see cref="TimeProvider"/> clock, the consumer ACKs the event WITHOUT
/// sending. This covers the narrow window where an admin reset the flag
/// (via <c>PUT /api/wallet/threshold</c>) but the previous advisory was
/// still recent enough to count as noise.
/// </para>
/// <para>
/// Anti-spoofing (T-05-03-11): <see cref="IKeycloakB2BAdminClient"/> is the
/// single source of truth for recipient e-mail addresses. The
/// consumer never reads recipients from the message body, never from any
/// cached per-agency list — it always fetches admins freshly for the
/// event's <see cref="WalletLowBalanceDetected.AgencyId"/>.
/// </para>
/// </remarks>
public sealed class WalletLowBalanceConsumer : IConsumer<WalletLowBalanceDetected>
{
    private readonly IAgencyWalletRepository _wallets;
    private readonly IKeycloakB2BAdminClient _keycloak;
    private readonly IWalletLowBalanceEmailSender _email;
    private readonly IOptionsMonitor<WalletOptions> _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<WalletLowBalanceConsumer> _log;

    public WalletLowBalanceConsumer(
        IAgencyWalletRepository wallets,
        IKeycloakB2BAdminClient keycloak,
        IWalletLowBalanceEmailSender email,
        IOptionsMonitor<WalletOptions> options,
        TimeProvider clock,
        ILogger<WalletLowBalanceConsumer> log)
    {
        _wallets = wallets;
        _keycloak = keycloak;
        _email = email;
        _options = options;
        _clock = clock;
        _log = log;
    }

    public async Task Consume(ConsumeContext<WalletLowBalanceDetected> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;

        // Cooldown defence-in-depth (T-05-03-07). Ignore if the previous
        // e-mail was sent inside EmailCooldownHours of now, even when the
        // hysteresis flag says we can re-send.
        var wallet = await _wallets.GetAsync(msg.AgencyId, ct).ConfigureAwait(false);
        if (wallet is not null && wallet.LastLowBalanceEmailAtUtc is { } last)
        {
            var cooldownHours = _options.CurrentValue.LowBalance.EmailCooldownHours;
            var now = _clock.GetUtcNow().UtcDateTime;
            if (now - last < TimeSpan.FromHours(cooldownHours))
            {
                _log.LogInformation(
                    "wallet low-balance e-mail skipped (cooldown) agency={AgencyId} last={Last} now={Now} cooldownHours={Hours}",
                    msg.AgencyId, last, now, cooldownHours);
                return;
            }
        }

        // Anti-spoofing (T-05-03-11): always resolve recipients fresh from
        // Keycloak for the event's agency_id — never trust anything else.
        var recipients = await _keycloak
            .GetAgentAdminsForAgencyAsync(msg.AgencyId, ct)
            .ConfigureAwait(false);

        if (recipients.Count == 0)
        {
            _log.LogWarning(
                "wallet low-balance e-mail not sent — no agent-admin recipients resolved agency={AgencyId}",
                msg.AgencyId);
            // Still flip the flag so the monitor doesn't busy-loop on this agency;
            // operators will notice via the warning log + bounced-addresses dashboard.
            await _wallets.MarkLowBalanceEmailSentAsync(
                msg.AgencyId,
                _clock.GetUtcNow().UtcDateTime,
                ct).ConfigureAwait(false);
            return;
        }

        await _email.SendLowBalanceEmailAsync(
            recipients,
            msg.AgencyId,
            msg.BalanceAmount,
            msg.ThresholdAmount,
            msg.Currency,
            ct).ConfigureAwait(false);

        // LowBalanceEmailSent = 1 + LastLowBalanceEmailAtUtc = now.
        // Repo-side UPDATE is the single source of truth so the monitor's
        // next tick won't re-publish for this agency until
        // WalletTopUpService / PUT /threshold resets the flag.
        await _wallets.MarkLowBalanceEmailSentAsync(
            msg.AgencyId,
            _clock.GetUtcNow().UtcDateTime,
            ct).ConfigureAwait(false);

        _log.LogInformation(
            "wallet low-balance e-mail sent agency={AgencyId} balance={Balance} threshold={Threshold} recipients={Count}",
            msg.AgencyId, msg.BalanceAmount, msg.ThresholdAmount, recipients.Count);
    }
}
