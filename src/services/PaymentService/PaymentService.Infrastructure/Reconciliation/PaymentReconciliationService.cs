using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TBE.PaymentService.Application.Reconciliation;
using TBE.PaymentService.Application.Wallet;

namespace TBE.PaymentService.Infrastructure.Reconciliation;

/// <summary>
/// Plan 06-02 Task 3 (BO-06) — discrepancy scanner run nightly at 02:00 UTC.
///
/// <para>
/// Compares the PREVIOUS 24h of Stripe events against the wallet ledger
/// and flags four classes of mismatch into <c>payment.PaymentReconciliationQueue</c>:
/// </para>
///
/// <list type="number">
///   <item>
///   <b>OrphanStripeEvent</b> — Stripe <c>charge.succeeded</c> observed
///   but no matching WalletTransactions row (joined via booking_id
///   metadata). This is a revenue-leak signal (money received, ledger
///   not updated).
///   </item>
///   <item>
///   <b>OrphanWalletRow</b> — WalletTransactions row in the window with
///   no matching Stripe event. Flags manual adjustments or a consumer
///   crash that left a phantom ledger row.
///   </item>
///   <item>
///   <b>AmountDrift</b> — Both sides present but Amount differs.
///   Severity Low if |drift| ≤ £5 else High. Low-drift rows are
///   typically rounding / currency-conversion quirks; high-drift
///   rows are escalated to the finance channel.
///   </item>
///   <item>
///   <b>UnprocessedEvent</b> — <c>StripeWebhookEvent.Processed = 0</c>
///   older than 1h. The webhook ingress persists every delivery but
///   the typed consumer never completed — suggests a poison-message
///   or downstream-service outage.
///   </item>
/// </list>
///
/// <para>
/// Rescans are idempotent via the
/// <c>IX_PaymentReconciliationQueue_Type_StripeEventId</c> (or _BookingId)
/// unique lookup: before insert we short-circuit if a Pending row
/// already exists.
/// </para>
/// </summary>
public sealed class PaymentReconciliationService : IPaymentReconciliationService
{
    private static readonly TimeSpan WindowLength = TimeSpan.FromHours(24);
    private static readonly TimeSpan UnprocessedSla = TimeSpan.FromHours(1);
    private const decimal AmountDriftLowBoundary = 5m;
    private const string ChargeSucceeded = "charge.succeeded";

    private readonly PaymentDbContext _db;
    private readonly TimeProvider _clock;
    private readonly ILogger<PaymentReconciliationService> _logger;

    public PaymentReconciliationService(
        PaymentDbContext db,
        TimeProvider clock,
        ILogger<PaymentReconciliationService> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task ScanAsync(CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var windowStart = now - WindowLength;

        // Note: EF InMemory cannot translate JSON_VALUE, so we pull raw
        // payloads and parse in-process. On SQL Server this is a modest
        // scan over at most 24h of events — cheap on the nightly
        // schedule. If volume grows we can push parsing into a persisted
        // computed column later.
        var stripeEvents = await _db.StripeWebhookEvents
            .Where(e => e.ReceivedAtUtc >= windowStart
                         && e.ReceivedAtUtc <= now
                         && e.EventType == ChargeSucceeded)
            .ToListAsync(ct);

        var stripeExtracts = stripeEvents
            .Select(e =>
            {
                var (bookingId, amount) = ParseChargeMetadata(e.RawPayload);
                return new StripeParsed(e.EventId, e.ReceivedAtUtc, bookingId, amount);
            })
            .ToList();

        var walletRows = await _db.WalletTransactions
            .Where(w => w.CreatedAtUtc >= windowStart
                        && w.CreatedAtUtc <= now
                        && w.BookingId != null
                        && (w.EntryType == WalletEntryType.Reserve
                            || w.EntryType == WalletEntryType.Commit
                            || w.EntryType == WalletEntryType.TopUp))
            .ToListAsync(ct);

        // Existing Pending rows — used for idempotent rescans.
        var existing = await _db.ReconciliationQueue
            .Where(q => q.Status == "Pending")
            .ToListAsync(ct);

        var existingStripeIds = existing
            .Where(q => q.StripeEventId != null)
            .Select(q => (q.DiscrepancyType, q.StripeEventId!))
            .ToHashSet();

        var existingBookingIds = existing
            .Where(q => q.BookingId != null)
            .Select(q => (q.DiscrepancyType, q.BookingId!.Value))
            .ToHashSet();

        var stripeByBooking = stripeExtracts
            .Where(s => s.BookingId != null)
            .ToLookup(s => s.BookingId!.Value);

        var walletByBooking = walletRows.ToLookup(w => w.BookingId!.Value);

        // Pass 1: orphan Stripe events + amount drift.
        foreach (var s in stripeExtracts)
        {
            if (existingStripeIds.Contains(("OrphanStripeEvent", s.EventId)) ||
                existingStripeIds.Contains(("AmountDrift", s.EventId)))
            {
                continue;
            }

            if (s.BookingId is null)
            {
                _db.ReconciliationQueue.Add(new PaymentReconciliationItem
                {
                    Id = Guid.NewGuid(),
                    DiscrepancyType = "OrphanStripeEvent",
                    Severity = "Medium",
                    StripeEventId = s.EventId,
                    DetectedAtUtc = now,
                    Status = "Pending",
                    Details = BuildDetailsJson(
                        stripe: new { eventId = s.EventId, receivedAtUtc = s.ReceivedAtUtc, amount = s.Amount },
                        wallet: null),
                });
                existingStripeIds.Add(("OrphanStripeEvent", s.EventId));
                continue;
            }

            var walletMatches = walletByBooking[s.BookingId.Value].ToList();
            if (walletMatches.Count == 0)
            {
                _db.ReconciliationQueue.Add(new PaymentReconciliationItem
                {
                    Id = Guid.NewGuid(),
                    DiscrepancyType = "OrphanStripeEvent",
                    Severity = "High",
                    BookingId = s.BookingId,
                    StripeEventId = s.EventId,
                    DetectedAtUtc = now,
                    Status = "Pending",
                    Details = BuildDetailsJson(
                        stripe: new { eventId = s.EventId, bookingId = s.BookingId, receivedAtUtc = s.ReceivedAtUtc, amount = s.Amount },
                        wallet: null),
                });
                existingStripeIds.Add(("OrphanStripeEvent", s.EventId));
                continue;
            }

            // Both sides present — amount drift check.
            var walletTotal = walletMatches.Sum(w => w.Amount);
            if (s.Amount is decimal stripeAmount && Math.Abs(walletTotal - stripeAmount) > 0m)
            {
                var drift = Math.Abs(walletTotal - stripeAmount);
                var severity = drift <= AmountDriftLowBoundary ? "Low" : "High";

                _db.ReconciliationQueue.Add(new PaymentReconciliationItem
                {
                    Id = Guid.NewGuid(),
                    DiscrepancyType = "AmountDrift",
                    Severity = severity,
                    BookingId = s.BookingId,
                    StripeEventId = s.EventId,
                    DetectedAtUtc = now,
                    Status = "Pending",
                    Details = BuildDetailsJson(
                        stripe: new { eventId = s.EventId, bookingId = s.BookingId, amount = stripeAmount },
                        wallet: new { bookingId = s.BookingId, totalAmount = walletTotal, txCount = walletMatches.Count }),
                });
                existingStripeIds.Add(("AmountDrift", s.EventId));
            }
        }

        // Pass 2: orphan wallet rows.
        foreach (var w in walletRows)
        {
            var bookingId = w.BookingId!.Value;
            if (existingBookingIds.Contains(("OrphanWalletRow", bookingId)))
                continue;

            if (!stripeByBooking[bookingId].Any())
            {
                _db.ReconciliationQueue.Add(new PaymentReconciliationItem
                {
                    Id = Guid.NewGuid(),
                    DiscrepancyType = "OrphanWalletRow",
                    Severity = "High",
                    BookingId = bookingId,
                    DetectedAtUtc = now,
                    Status = "Pending",
                    Details = BuildDetailsJson(
                        stripe: null,
                        wallet: new
                        {
                            bookingId,
                            txId = w.TxId,
                            entryType = w.EntryType.ToString(),
                            amount = w.Amount,
                            createdAtUtc = w.CreatedAtUtc,
                        }),
                });
                existingBookingIds.Add(("OrphanWalletRow", bookingId));
            }
        }

        // Pass 3: unprocessed Stripe events older than 1h (outside the
        // 24h window — stale payloads can linger).
        var unprocessedCutoff = now - UnprocessedSla;
        var unprocessed = await _db.StripeWebhookEvents
            .Where(e => e.Processed == false && e.ReceivedAtUtc <= unprocessedCutoff)
            .ToListAsync(ct);

        foreach (var u in unprocessed)
        {
            if (existingStripeIds.Contains(("UnprocessedEvent", u.EventId)))
                continue;

            _db.ReconciliationQueue.Add(new PaymentReconciliationItem
            {
                Id = Guid.NewGuid(),
                DiscrepancyType = "UnprocessedEvent",
                Severity = "High",
                StripeEventId = u.EventId,
                DetectedAtUtc = now,
                Status = "Pending",
                Details = BuildDetailsJson(
                    stripe: new { eventId = u.EventId, eventType = u.EventType, receivedAtUtc = u.ReceivedAtUtc },
                    wallet: null),
            });
            existingStripeIds.Add(("UnprocessedEvent", u.EventId));
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "payment-reconciliation-scan stripeEvents={StripeCount} walletRows={WalletCount} unprocessed={UnprocessedCount}",
            stripeEvents.Count, walletRows.Count, unprocessed.Count);
    }

    private static (Guid? bookingId, decimal? amount) ParseChargeMetadata(string rawPayload)
    {
        if (string.IsNullOrWhiteSpace(rawPayload)) return (null, null);

        try
        {
            using var doc = JsonDocument.Parse(rawPayload);
            if (!doc.RootElement.TryGetProperty("data", out var data)) return (null, null);
            if (!data.TryGetProperty("object", out var obj)) return (null, null);

            Guid? bookingId = null;
            if (obj.TryGetProperty("metadata", out var md) &&
                md.ValueKind == JsonValueKind.Object &&
                md.TryGetProperty("booking_id", out var bEl) &&
                bEl.ValueKind == JsonValueKind.String &&
                Guid.TryParse(bEl.GetString(), out var b))
            {
                bookingId = b;
            }

            decimal? amount = null;
            if (obj.TryGetProperty("amount", out var amtEl) && amtEl.ValueKind == JsonValueKind.Number)
            {
                // Stripe represents amounts as integer minor units.
                var minor = amtEl.GetInt64();
                amount = minor / 100m;
            }

            return (bookingId, amount);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    private static string BuildDetailsJson(object? stripe, object? wallet)
    {
        return JsonSerializer.Serialize(new { stripe, wallet });
    }

    private sealed record StripeParsed(string EventId, DateTime ReceivedAtUtc, Guid? BookingId, decimal? Amount);
}
