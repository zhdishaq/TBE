using Dapper;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using TBE.PaymentService.Application.Wallet;
using TBE.PaymentService.Infrastructure.Wallet;
using TBE.Tests.Shared.Fixtures;
using Xunit;

namespace Payments.Tests;

/// <summary>
/// Plan 05-03 Task 1 — B2B-07 atomicity: under N parallel reserves whose
/// combined amount exceeds the wallet balance, the append-only ledger guarantees
/// exactly the right number of <c>WalletReserved</c> outcomes (and the rest fail
/// with <see cref="InsufficientWalletBalanceException"/>). The ledger SUM
/// (computed via the PERSISTED <c>SignedAmount</c> column) NEVER goes negative.
///
/// This test is the canonical UAT proof for B2B-07. It runs against a real
/// MSSQL Testcontainer (LocalDB / in-memory cannot reproduce the
/// UPDLOCK + ROWLOCK + HOLDLOCK + SERIALIZABLE escalation that prevents
/// double-spend).
/// </summary>
[Collection(nameof(MsSqlContainerFixture))]
[Trait("Category", "Integration")]
public class WalletConcurrencyTests
{
    private readonly MsSqlContainerFixture _sql;

    public WalletConcurrencyTests(MsSqlContainerFixture sql) => _sql = sql;

    private async Task EnsureSchemaAsync()
    {
        await using var conn = new SqlConnection(_sql.ConnectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync(@"
IF SCHEMA_ID('payment') IS NULL EXEC('CREATE SCHEMA [payment]');
IF OBJECT_ID('payment.WalletTransactions','U') IS NULL
BEGIN
    CREATE TABLE [payment].[WalletTransactions] (
        [TxId]             UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        [WalletId]         UNIQUEIDENTIFIER NOT NULL,
        [BookingId]        UNIQUEIDENTIFIER NULL,
        [EntryType]        TINYINT          NOT NULL,
        [Amount]           DECIMAL(18,4)    NOT NULL,
        [SignedAmount]     AS CASE WHEN [EntryType] IN (1,2) THEN -[Amount] ELSE [Amount] END PERSISTED,
        [Currency]         CHAR(3)          NOT NULL,
        [IdempotencyKey]   NVARCHAR(100)    NOT NULL,
        [CorrelatesWithTx] UNIQUEIDENTIFIER NULL,
        [CreatedAtUtc]     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_WalletTransactions] PRIMARY KEY CLUSTERED ([TxId] ASC)
    );
    CREATE UNIQUE INDEX [UX_WalletTransactions_IdempotencyKey]
        ON [payment].[WalletTransactions] ([IdempotencyKey]);
END
ELSE
BEGIN
    DELETE FROM [payment].[WalletTransactions];
END
");
    }

    private WalletRepository NewRepo() =>
        new WalletRepository(_sql.ConnectionString, NullLogger<WalletRepository>.Instance);

    private async Task SeedBalanceAsync(Guid walletId, decimal balance)
    {
        var repo = NewRepo();
        await repo.TopUpAsync(walletId, balance, "GBP",
            $"seed-{walletId}-{Guid.NewGuid():N}", CancellationToken.None);
    }

    /// <summary>
    /// B2B-07 UAT: seed wallet with balance=100, fire two parallel reserves of
    /// 60 each (combined 120 > 100). Exactly one MUST succeed; the other MUST
    /// fail. Final balance MUST be >= 0. Ledger row count MUST grow by exactly 1.
    /// </summary>
    [Fact(DisplayName = "B2B-07: two parallel reserves exceeding balance permits exactly one")]
    public async Task Two_parallel_reserves_exceeding_balance_permits_exactly_one()
    {
        await EnsureSchemaAsync();
        var walletId = Guid.NewGuid();
        await SeedBalanceAsync(walletId, balance: 100m);

        var repo = NewRepo();
        // After seed there is 1 row (the TopUp).
        var rowsBefore = await CountTransactionsAsync(walletId);
        rowsBefore.Should().Be(1);

        var bookingA = Guid.NewGuid();
        var bookingB = Guid.NewGuid();

        var t1 = Task.Run(() => TryReserve(repo, walletId, bookingA, 60m));
        var t2 = Task.Run(() => TryReserve(repo, walletId, bookingB, 60m));

        var results = await Task.WhenAll(t1, t2);
        var ok = results.Count(r => r.Success);
        var failed = results.Count(r => !r.Success);

        ok.Should().Be(1, "exactly one of the two parallel 60-amount reserves can fit in 100 balance");
        failed.Should().Be(1);

        var balance = await repo.GetBalanceAsync(walletId, CancellationToken.None);
        balance.Should().BeGreaterThanOrEqualTo(0m, "ledger SUM never goes negative under any race");
        balance.Should().Be(40m, "100 - 60 = 40");

        var rowsAfter = await CountTransactionsAsync(walletId);
        (rowsAfter - rowsBefore).Should().Be(1, "exactly one Reserve row should land");
    }

    /// <summary>
    /// Same property at scale: seed 300 capacity, 10 parallel reserves of 50 each
    /// (combined 500 > 300). Exactly 6 succeed (6 * 50 = 300) and exactly 4 fail.
    /// </summary>
    [Fact(DisplayName = "B2B-07: 10 parallel reserves on 300-capacity wallet → exactly 6 successes")]
    public async Task Ten_parallel_reserves_on_300_capacity_wallet_six_succeed()
    {
        await EnsureSchemaAsync();
        var walletId = Guid.NewGuid();
        await SeedBalanceAsync(walletId, balance: 300m);

        var repo = NewRepo();
        int success = 0;
        int insufficient = 0;
        var tasks = Enumerable.Range(0, 10).Select(async _ =>
        {
            var bid = Guid.NewGuid();
            try
            {
                await repo.ReserveAsync(walletId, bid, 50m, "GBP", CancellationToken.None);
                Interlocked.Increment(ref success);
            }
            catch (InsufficientWalletBalanceException)
            {
                Interlocked.Increment(ref insufficient);
            }
        });
        await Task.WhenAll(tasks);

        success.Should().Be(6);
        insufficient.Should().Be(4);
        (await repo.GetBalanceAsync(walletId, CancellationToken.None)).Should().Be(0m);
    }

    private static async Task<(bool Success, decimal Available)> TryReserve(
        WalletRepository repo, Guid walletId, Guid bookingId, decimal amount)
    {
        try
        {
            await repo.ReserveAsync(walletId, bookingId, amount, "GBP", CancellationToken.None);
            return (true, 0m);
        }
        catch (InsufficientWalletBalanceException ex)
        {
            return (false, ex.AvailableBalance);
        }
    }

    private async Task<int> CountTransactionsAsync(Guid walletId)
    {
        await using var conn = new SqlConnection(_sql.ConnectionString);
        await conn.OpenAsync();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM payment.WalletTransactions WHERE WalletId = @W",
            new { W = walletId });
    }
}
