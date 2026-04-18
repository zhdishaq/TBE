using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TBE.PaymentService.Application.Wallet;

namespace TBE.PaymentService.Infrastructure.Wallet;

/// <summary>
/// Plan 05-03 Task 2 — Dapper-backed <see cref="IAgencyWalletRepository"/>.
/// Keeps parity with <see cref="WalletRepository"/> (raw SQL, explicit locks)
/// so write paths remain visible in code review — Pitfall 19.
/// </summary>
public sealed class AgencyWalletRepository : IAgencyWalletRepository
{
    private readonly string _connectionString;
    private readonly ILogger<AgencyWalletRepository> _log;

    public AgencyWalletRepository(IConfiguration config, ILogger<AgencyWalletRepository> log)
    {
        _connectionString = config.GetConnectionString("PaymentDb") ?? string.Empty;
        _log = log;
    }

    // Test hook — same as WalletRepository's secondary ctor.
    public AgencyWalletRepository(string connectionString, ILogger<AgencyWalletRepository>? log = null)
    {
        _connectionString = connectionString;
        _log = log ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<AgencyWalletRepository>.Instance;
    }

    public async Task<AgencyWallet?> GetAsync(Guid agencyId, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        return await conn.QuerySingleOrDefaultAsync<AgencyWallet>(new CommandDefinition(
            @"SELECT Id, AgencyId, Currency, LowBalanceThresholdAmount,
                     LowBalanceEmailSent, LastLowBalanceEmailAtUtc, UpdatedAtUtc
              FROM payment.AgencyWallets WHERE AgencyId = @AgencyId",
            new { AgencyId = agencyId }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task SetThresholdAsync(Guid agencyId, decimal threshold, string currency, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        // Upsert: admins can set threshold before the first ledger row exists.
        // Resets LowBalanceEmailSent so lowering threshold below current
        // balance re-arms the alert (hysteresis).
        var rows = await conn.ExecuteAsync(new CommandDefinition(
            @"MERGE payment.AgencyWallets WITH (HOLDLOCK) AS target
              USING (SELECT @AgencyId AS AgencyId) AS src ON target.AgencyId = src.AgencyId
              WHEN MATCHED THEN UPDATE SET
                  LowBalanceThresholdAmount = @Threshold,
                  Currency = @Currency,
                  LowBalanceEmailSent = 0,
                  UpdatedAtUtc = SYSUTCDATETIME()
              WHEN NOT MATCHED THEN INSERT
                  (AgencyId, Currency, LowBalanceThresholdAmount, LowBalanceEmailSent, UpdatedAtUtc)
                  VALUES (@AgencyId, @Currency, @Threshold, 0, SYSUTCDATETIME());",
            new { AgencyId = agencyId, Threshold = threshold, Currency = currency },
            cancellationToken: ct)).ConfigureAwait(false);

        _log.LogInformation("wallet threshold set agency={AgencyId} threshold={Threshold} rows={Rows}",
            agencyId, threshold, rows);
    }

    public async Task MarkLowBalanceEmailSentAsync(Guid agencyId, DateTime atUtc, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(new CommandDefinition(
            @"UPDATE payment.AgencyWallets WITH (UPDLOCK, HOLDLOCK)
                 SET LowBalanceEmailSent = 1,
                     LastLowBalanceEmailAtUtc = @AtUtc,
                     UpdatedAtUtc = SYSUTCDATETIME()
              WHERE AgencyId = @AgencyId",
            new { AgencyId = agencyId, AtUtc = atUtc }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task ResetLowBalanceEmailFlagAsync(Guid agencyId, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await conn.ExecuteAsync(new CommandDefinition(
            @"UPDATE payment.AgencyWallets WITH (UPDLOCK, HOLDLOCK)
                 SET LowBalanceEmailSent = 0,
                     UpdatedAtUtc = SYSUTCDATETIME()
              WHERE AgencyId = @AgencyId",
            new { AgencyId = agencyId }, cancellationToken: ct)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AgencyBalanceSnapshot>> ListAgenciesBelowThresholdAsync(CancellationToken ct)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<AgencyBalanceSnapshot>(new CommandDefinition(
            @"SELECT w.AgencyId                           AS AgencyId,
                     COALESCE(SUM(t.SignedAmount), 0)     AS Balance,
                     w.LowBalanceThresholdAmount          AS Threshold,
                     w.Currency                           AS Currency
              FROM payment.AgencyWallets w
              LEFT JOIN payment.WalletTransactions t ON t.WalletId = w.AgencyId
              WHERE w.LowBalanceEmailSent = 0
              GROUP BY w.AgencyId, w.LowBalanceThresholdAmount, w.Currency
              HAVING COALESCE(SUM(t.SignedAmount), 0) < w.LowBalanceThresholdAmount",
            cancellationToken: ct)).ConfigureAwait(false);
        return rows.ToList();
    }
}
