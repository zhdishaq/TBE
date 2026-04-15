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
/// Integration tests for <see cref="WalletRepository"/> — each test spins up the
/// WalletTransactions schema in the shared MsSql Testcontainer and verifies
/// the UPDLOCK/ROWLOCK/HOLDLOCK concurrency guarantee end-to-end.
/// </summary>
[Collection(nameof(MsSqlContainerFixture))]
[Trait("Category", "Integration")]
public class WalletRepositoryTests
{
    private readonly MsSqlContainerFixture _sql;

    public WalletRepositoryTests(MsSqlContainerFixture sql)
    {
        _sql = sql;
    }

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

    /// <summary>Seed a TopUp row giving the wallet a given available balance.</summary>
    private async Task SeedBalanceAsync(Guid walletId, decimal balance)
    {
        var repo = NewRepo();
        await repo.TopUpAsync(walletId, balance, "GBP",
            $"seed-{walletId}-{Guid.NewGuid():N}", CancellationToken.None);
    }

    [Fact(DisplayName = "PAY05: reserve then commit then balance is correct")]
    public async Task PAY05_reserve_then_commit_then_balance_is_correct()
    {
        await EnsureSchemaAsync();
        var walletId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        await SeedBalanceAsync(walletId, 1000m);

        var repo = NewRepo();
        var txId = await repo.ReserveAsync(walletId, bookingId, 250m, "GBP", CancellationToken.None);
        (await repo.GetBalanceAsync(walletId, CancellationToken.None)).Should().Be(750m);

        await repo.CommitAsync(walletId, bookingId, txId, CancellationToken.None);
        (await repo.GetBalanceAsync(walletId, CancellationToken.None)).Should().Be(750m);
    }

    [Fact(DisplayName = "PAY05: reserve then release restores balance")]
    public async Task PAY05_reserve_then_release_restores_balance()
    {
        await EnsureSchemaAsync();
        var walletId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        await SeedBalanceAsync(walletId, 1000m);

        var repo = NewRepo();
        var txId = await repo.ReserveAsync(walletId, bookingId, 250m, "GBP", CancellationToken.None);
        (await repo.GetBalanceAsync(walletId, CancellationToken.None)).Should().Be(750m);

        await repo.ReleaseAsync(walletId, bookingId, txId, CancellationToken.None);
        (await repo.GetBalanceAsync(walletId, CancellationToken.None)).Should().Be(1000m);
    }

    [Fact(DisplayName = "PAY06: idempotent retry of reserve returns existing tx")]
    public async Task PAY06_idempotent_retry_of_reserve_returns_existing_tx()
    {
        await EnsureSchemaAsync();
        var walletId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        await SeedBalanceAsync(walletId, 1000m);

        var repo = NewRepo();
        var first = await repo.ReserveAsync(walletId, bookingId, 100m, "GBP", CancellationToken.None);
        var second = await repo.ReserveAsync(walletId, bookingId, 100m, "GBP", CancellationToken.None);

        second.Should().Be(first);
        (await repo.GetBalanceAsync(walletId, CancellationToken.None)).Should().Be(900m);
    }

    [Fact(DisplayName = "PAY06: 50 concurrent reserves on 30-capacity wallet produce exactly 30 successes")]
    public async Task PAY06_fifty_concurrent_reserves_on_30_capacity_produce_exactly_30_success()
    {
        await EnsureSchemaAsync();
        var walletId = Guid.NewGuid();
        // Capacity: 30 reservations at 10.00 each = 300.00
        await SeedBalanceAsync(walletId, 300m);

        var repo = NewRepo();
        int success = 0;
        int insufficient = 0;
        var tasks = Enumerable.Range(0, 50).Select(async _ =>
        {
            // Each reservation uses a distinct bookingId so the idempotency keys don't collide.
            var bookingId = Guid.NewGuid();
            try
            {
                await repo.ReserveAsync(walletId, bookingId, 10m, "GBP", CancellationToken.None);
                Interlocked.Increment(ref success);
            }
            catch (InsufficientWalletBalanceException)
            {
                Interlocked.Increment(ref insufficient);
            }
        });
        await Task.WhenAll(tasks);

        success.Should().Be(30);
        insufficient.Should().Be(20);

        var balance = await repo.GetBalanceAsync(walletId, CancellationToken.None);
        balance.Should().Be(0m);
    }
}
