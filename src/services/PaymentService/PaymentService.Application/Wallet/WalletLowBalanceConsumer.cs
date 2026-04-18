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
/// Stub — real implementation lands in the Task 2 GREEN commit. Surface is
/// present so RED tests compile against the same ctor shape.
/// </remarks>
public sealed class WalletLowBalanceConsumer : IConsumer<WalletLowBalanceDetected>
{
    public WalletLowBalanceConsumer(
        IAgencyWalletRepository wallets,
        IKeycloakB2BAdminClient keycloak,
        IWalletLowBalanceEmailSender email,
        IOptionsMonitor<WalletOptions> options,
        TimeProvider clock,
        ILogger<WalletLowBalanceConsumer> log)
    {
        _ = wallets;
        _ = keycloak;
        _ = email;
        _ = options;
        _ = clock;
        _ = log;
    }

    public Task Consume(ConsumeContext<WalletLowBalanceDetected> context)
        => throw new NotImplementedException(
            "Plan 05-03 Task 2 RED: WalletLowBalanceConsumer.Consume not yet implemented.");
}
