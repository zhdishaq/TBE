using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TBE.PaymentService.Application.Wallet;

namespace TBE.PaymentService.Infrastructure.Wallet;

/// <summary>
/// Dapper-based implementation of <see cref="IWalletRepository"/>.
/// Every Reserve/Commit/Release/TopUp opens its own <see cref="SqlConnection"/>,
/// begins a transaction, and reads the current balance with
/// <c>WITH (UPDLOCK, ROWLOCK, HOLDLOCK)</c> per Pattern 5 / D-15 to prevent
/// double-spend races.
/// </summary>
public sealed class WalletRepository : IWalletRepository
{
    private readonly string _connectionString;
    private readonly ILogger<WalletRepository> _log;

    public WalletRepository(IConfiguration config, ILogger<WalletRepository> log)
    {
        _connectionString = config.GetConnectionString("PaymentDb") ?? string.Empty;
        _log = log;
    }

    // Test hook: build directly from a connection string.
    public WalletRepository(string connectionString, ILogger<WalletRepository>? log = null)
    {
        _connectionString = connectionString;
        _log = log ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<WalletRepository>.Instance;
    }

    public async Task<Guid> ReserveAsync(Guid walletId, Guid bookingId, decimal amount, string currency, CancellationToken ct)
    {
        var idemKey = $"booking-{bookingId}-reserve";
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(ct);

        try
        {
            var balance = await conn.ExecuteScalarAsync<decimal>(new CommandDefinition(
                @"SELECT ISNULL(SUM(SignedAmount),0)
                  FROM payment.WalletTransactions WITH (UPDLOCK, ROWLOCK, HOLDLOCK)
                  WHERE WalletId = @WalletId",
                new { WalletId = walletId }, tx, cancellationToken: ct));

            if (balance < amount)
            {
                await tx.RollbackAsync(ct);
                throw new InsufficientWalletBalanceException(walletId, amount, balance);
            }

            var txId = Guid.NewGuid();
            try
            {
                await conn.ExecuteAsync(new CommandDefinition(
                    @"INSERT INTO payment.WalletTransactions
                         (TxId, WalletId, BookingId, EntryType, Amount, Currency, IdempotencyKey, CreatedAtUtc)
                       VALUES
                         (@TxId, @WalletId, @BookingId, @EntryType, @Amount, @Currency, @IdemKey, SYSUTCDATETIME())",
                    new
                    {
                        TxId = txId,
                        WalletId = walletId,
                        BookingId = (Guid?)bookingId,
                        EntryType = (byte)WalletEntryType.Reserve,
                        Amount = amount,
                        Currency = currency,
                        IdemKey = idemKey
                    }, tx, cancellationToken: ct));
            }
            catch (SqlException ex) when (IsUniqueViolation(ex))
            {
                // Idempotent retry — return existing row's TxId.
                var existing = await conn.ExecuteScalarAsync<Guid>(new CommandDefinition(
                    "SELECT TxId FROM payment.WalletTransactions WHERE IdempotencyKey = @IdemKey",
                    new { IdemKey = idemKey }, tx, cancellationToken: ct));
                await tx.CommitAsync(ct);
                return existing;
            }

            await tx.CommitAsync(ct);
            return txId;
        }
        catch (InsufficientWalletBalanceException)
        {
            throw;
        }
        catch
        {
            try { await tx.RollbackAsync(ct); } catch { /* suppress */ }
            throw;
        }
    }

    public async Task CommitAsync(Guid walletId, Guid bookingId, Guid reservationTxId, CancellationToken ct)
    {
        var idemKey = $"booking-{bookingId}-wallet-commit";
        await InsertLedgerEntryAsync(walletId, bookingId, WalletEntryType.Commit, amount: 0m, currency: "GBP",
            idempotencyKey: idemKey, correlates: reservationTxId, ct: ct);
    }

    public async Task ReleaseAsync(Guid walletId, Guid bookingId, Guid reservationTxId, CancellationToken ct)
    {
        var idemKey = $"booking-{bookingId}-wallet-release";
        // Release must restore the reserved amount; look it up from the Reserve row.
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        var reserveAmount = await conn.ExecuteScalarAsync<decimal?>(new CommandDefinition(
            "SELECT Amount FROM payment.WalletTransactions WHERE TxId = @TxId AND EntryType = 1",
            new { TxId = reservationTxId }, cancellationToken: ct));
        if (reserveAmount is null) return;

        await InsertLedgerEntryAsync(walletId, bookingId, WalletEntryType.Release, reserveAmount.Value, "GBP",
            idemKey, reservationTxId, ct);
    }

    public async Task<Guid> TopUpAsync(Guid walletId, decimal amount, string currency, string idempotencyKey, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        var txId = Guid.NewGuid();
        try
        {
            await conn.ExecuteAsync(new CommandDefinition(
                @"INSERT INTO payment.WalletTransactions
                     (TxId, WalletId, BookingId, EntryType, Amount, Currency, IdempotencyKey, CreatedAtUtc)
                   VALUES
                     (@TxId, @WalletId, NULL, @EntryType, @Amount, @Currency, @IdemKey, SYSUTCDATETIME())",
                new
                {
                    TxId = txId,
                    WalletId = walletId,
                    EntryType = (byte)WalletEntryType.TopUp,
                    Amount = amount,
                    Currency = currency,
                    IdemKey = idempotencyKey
                }, cancellationToken: ct));
            return txId;
        }
        catch (SqlException ex) when (IsUniqueViolation(ex))
        {
            var existing = await conn.ExecuteScalarAsync<Guid>(new CommandDefinition(
                "SELECT TxId FROM payment.WalletTransactions WHERE IdempotencyKey = @IdemKey",
                new { IdemKey = idempotencyKey }, cancellationToken: ct));
            throw new DuplicateWalletTopUpException(existing, ex);
        }
    }

    public async Task<decimal> GetBalanceAsync(Guid walletId, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<decimal>(new CommandDefinition(
            "SELECT ISNULL(SUM(SignedAmount),0) FROM payment.WalletTransactions WHERE WalletId = @WalletId",
            new { WalletId = walletId }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<WalletTransactionDto>> ListAsync(
        Guid walletId, DateTimeOffset? from, DateTimeOffset? to, int page, int size, CancellationToken ct)
    {
        if (page < 1) page = 1;
        if (size < 1 || size > 500) size = 50;
        var offset = (page - 1) * size;

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        var rows = await conn.QueryAsync<WalletTransactionDto>(new CommandDefinition(
            @"SELECT TxId, WalletId, BookingId, EntryType, Amount, SignedAmount, Currency, IdempotencyKey, CreatedAtUtc
              FROM payment.WalletTransactions
              WHERE WalletId = @WalletId
                AND (@From IS NULL OR CreatedAtUtc >= @From)
                AND (@To   IS NULL OR CreatedAtUtc <  @To)
              ORDER BY CreatedAtUtc DESC
              OFFSET @Offset ROWS FETCH NEXT @Size ROWS ONLY",
            new
            {
                WalletId = walletId,
                From = from?.UtcDateTime,
                To = to?.UtcDateTime,
                Offset = offset,
                Size = size
            }, cancellationToken: ct));
        return rows.ToList();
    }

    private async Task InsertLedgerEntryAsync(
        Guid walletId, Guid bookingId, WalletEntryType type, decimal amount, string currency,
        string idempotencyKey, Guid? correlates, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        try
        {
            await conn.ExecuteAsync(new CommandDefinition(
                @"INSERT INTO payment.WalletTransactions
                     (TxId, WalletId, BookingId, EntryType, Amount, Currency, IdempotencyKey, CorrelatesWithTx, CreatedAtUtc)
                   VALUES
                     (NEWID(), @WalletId, @BookingId, @EntryType, @Amount, @Currency, @IdemKey, @Correlates, SYSUTCDATETIME())",
                new
                {
                    WalletId = walletId,
                    BookingId = (Guid?)bookingId,
                    EntryType = (byte)type,
                    Amount = amount,
                    Currency = currency,
                    IdemKey = idempotencyKey,
                    Correlates = correlates
                }, cancellationToken: ct));
        }
        catch (SqlException ex) when (IsUniqueViolation(ex))
        {
            _log.LogInformation("ledger entry replay suppressed idempotencyKey={IdemKey}", idempotencyKey);
        }
    }

    private static bool IsUniqueViolation(SqlException ex)
        => ex.Number is 2601 or 2627;
}

