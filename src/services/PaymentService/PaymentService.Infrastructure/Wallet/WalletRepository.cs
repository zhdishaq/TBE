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

            // Plan 06-04 / CRM-02 / D-61 — look up the agency's configured
            // credit limit under UPDLOCK+HOLDLOCK on the same transaction
            // scope so a concurrent PATCH of AgencyWallets.CreditLimit can't
            // race with the reserve decision (T-6-56 credit-limit bypass
            // mitigation). A missing row (wallet metadata not yet seeded)
            // is treated as CreditLimit=0 (no overdraft — safe default).
            // WalletId == AgencyId (1:1 per AgencyWallet.cs doc).
            var creditLimit = await conn.ExecuteScalarAsync<decimal?>(new CommandDefinition(
                @"SELECT CreditLimit
                  FROM payment.AgencyWallets WITH (UPDLOCK, HOLDLOCK)
                  WHERE AgencyId = @AgencyId",
                new { AgencyId = walletId }, tx, cancellationToken: ct)) ?? 0m;

            var available = balance + creditLimit;
            if (amount > available)
            {
                await tx.RollbackAsync(ct);
                // Distinguish the two failure modes so the consumer can
                // surface different error types:
                //   * creditLimit == 0 → classic insufficient-funds.
                //   * creditLimit  > 0 → credit-limit-over-limit (D-61).
                if (creditLimit > 0m)
                {
                    throw new CreditLimitExceededException(walletId, amount, balance, creditLimit);
                }
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
        catch (CreditLimitExceededException)
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

    /// <summary>
    /// D-39 / Plan 06-01 Task 6 — sole writer of <c>ManualCredit</c> ledger
    /// entries. Called by <c>WalletCreditApprovedConsumer</c>. Idempotent
    /// via the unique index on <c>IdempotencyKey</c>: a redelivery of the
    /// same <c>WalletCreditApproved</c> event converts to an infolog line
    /// and returns the existing TxId — the wallet is never double-credited
    /// even if RabbitMQ redelivers. MassTransit's InboxState dedup on the
    /// consumer side is the first line of defence; this unique constraint
    /// is the belt-and-braces fallback if the outbox is ever bypassed.
    /// </summary>
    public async Task<Guid> ManualCreditAsync(
        Guid walletId,
        decimal amount,
        string currency,
        string idempotencyKey,
        Guid? linkedBookingId,
        string approvedBy,
        string approvalNotes,
        CancellationToken ct)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        var txId = Guid.NewGuid();
        try
        {
            await conn.ExecuteAsync(new CommandDefinition(
                @"INSERT INTO payment.WalletTransactions
                     (TxId, WalletId, BookingId, EntryType, Amount, Currency,
                      IdempotencyKey, ApprovedBy, ApprovalNotes, CreatedAtUtc)
                   VALUES
                     (@TxId, @WalletId, @BookingId, @EntryType, @Amount, @Currency,
                      @IdemKey, @ApprovedBy, @ApprovalNotes, SYSUTCDATETIME())",
                new
                {
                    TxId = txId,
                    WalletId = walletId,
                    BookingId = linkedBookingId,
                    EntryType = (byte)WalletEntryType.ManualCredit,
                    Amount = amount,
                    Currency = currency,
                    IdemKey = idempotencyKey,
                    ApprovedBy = approvedBy,
                    ApprovalNotes = approvalNotes,
                }, cancellationToken: ct));
            return txId;
        }
        catch (SqlException ex) when (IsUniqueViolation(ex))
        {
            var existing = await conn.ExecuteScalarAsync<Guid>(new CommandDefinition(
                "SELECT TxId FROM payment.WalletTransactions WHERE IdempotencyKey = @IdemKey",
                new { IdemKey = idempotencyKey }, cancellationToken: ct));
            _log.LogInformation(
                "manual-credit replay suppressed idemKey={IdemKey} txId={TxId}",
                idempotencyKey, existing);
            return existing;
        }
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

