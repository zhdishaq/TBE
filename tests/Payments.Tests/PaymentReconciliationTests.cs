using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using TBE.PaymentService.API.Controllers;
using TBE.PaymentService.Application.Reconciliation;
using TBE.PaymentService.Application.Wallet;
using TBE.PaymentService.Infrastructure;
using TBE.PaymentService.Infrastructure.Reconciliation;
using TBE.PaymentService.Infrastructure.Stripe;
using TBE.PaymentService.Infrastructure.Wallet;
using Xunit;

namespace Payments.Tests;

/// <summary>
/// Plan 06-02 Task 3 (BO-06) — nightly Stripe-vs-wallet reconciliation.
///
/// <para>
/// The reconciliation service inspects the PREVIOUS 24h window and flags
/// four classes of discrepancy into <c>payment.PaymentReconciliationQueue</c>:
/// <list type="bullet">
///   <item><c>OrphanStripeEvent</c>: charge.succeeded in Stripe but no
///         WalletTransactions row with matching BookingId.</item>
///   <item><c>OrphanWalletRow</c>: WalletTransactions row with no
///         matching Stripe event inside the window.</item>
///   <item><c>AmountDrift</c>: Both sides present but Amount differs;
///         severity Low if |drift| ≤ £5 else High.</item>
///   <item><c>UnprocessedEvent</c>: StripeWebhookEvent with Processed=0
///         &gt; 1h old (handler never ran).</item>
/// </list>
/// </para>
///
/// <para>
/// The controller surface exposes List (ops-read / cs / finance / admin)
/// and Resolve (ops-finance + ops-admin) — Resolve flips Status and
/// stamps ResolvedBy/ResolvedAt/ResolutionNotes.
/// </para>
/// </summary>
[Trait("Category", "Phase06")]
public sealed class PaymentReconciliationTests
{
    private static readonly DateTime TodayFixed = new(2026, 6, 15, 2, 0, 0, DateTimeKind.Utc);

    private static PaymentDbContext NewDb() =>
        new(new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase($"recon-{Guid.NewGuid()}")
            .ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static ControllerContext Ctx(string preferredUsername, params string[] roles)
    {
        var claims = roles.Select(r => new Claim(ClaimTypes.Role, r)).ToList();
        claims.Add(new Claim("preferred_username", preferredUsername));
        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")),
            },
        };
    }

    private static string StripeChargeSucceededPayload(Guid bookingId, decimal amountGbp) => $$"""
        {
          "data": {
            "object": {
              "id": "ch_test_{{Guid.NewGuid():N}}",
              "amount": {{(int)(amountGbp * 100)}},
              "currency": "gbp",
              "metadata": { "booking_id": "{{bookingId}}" }
            }
          }
        }
        """;

    [Fact]
    public async Task detects_orphan_stripe_event_when_no_matching_wallet_row()
    {
        var db = NewDb();
        var clock = new FakeTimeProvider(TodayFixed);

        var bookingId = Guid.NewGuid();
        db.StripeWebhookEvents.Add(new StripeWebhookEvent
        {
            EventId = "evt_orphan_1",
            EventType = "charge.succeeded",
            ReceivedAtUtc = TodayFixed.AddHours(-6),
            RawPayload = StripeChargeSucceededPayload(bookingId, 100m),
            Processed = true,
        });
        await db.SaveChangesAsync();

        var service = new PaymentReconciliationService(db, clock, NullLogger<PaymentReconciliationService>.Instance);
        await service.ScanAsync(CancellationToken.None);

        var queue = await db.ReconciliationQueue.ToListAsync();
        queue.Should().ContainSingle(q =>
            q.DiscrepancyType == "OrphanStripeEvent" &&
            q.StripeEventId == "evt_orphan_1");
    }

    [Fact]
    public async Task detects_orphan_wallet_row_when_no_matching_stripe_event()
    {
        var db = NewDb();
        var clock = new FakeTimeProvider(TodayFixed);

        var bookingId = Guid.NewGuid();
        db.WalletTransactions.Add(new WalletTransaction
        {
            TxId = Guid.NewGuid(),
            WalletId = Guid.NewGuid(),
            BookingId = bookingId,
            EntryType = WalletEntryType.Reserve,
            Amount = 200m,
            SignedAmount = -200m,
            Currency = "GBP",
            IdempotencyKey = $"wallet-orphan-{Guid.NewGuid()}",
            CreatedAtUtc = TodayFixed.AddHours(-4),
        });
        await db.SaveChangesAsync();

        var service = new PaymentReconciliationService(db, clock, NullLogger<PaymentReconciliationService>.Instance);
        await service.ScanAsync(CancellationToken.None);

        var queue = await db.ReconciliationQueue.ToListAsync();
        queue.Should().ContainSingle(q =>
            q.DiscrepancyType == "OrphanWalletRow" &&
            q.BookingId == bookingId);
    }

    [Fact]
    public async Task detects_amount_drift_and_tags_low_severity_when_under_five_pounds()
    {
        var db = NewDb();
        var clock = new FakeTimeProvider(TodayFixed);

        var bookingId = Guid.NewGuid();
        db.StripeWebhookEvents.Add(new StripeWebhookEvent
        {
            EventId = "evt_drift_low",
            EventType = "charge.succeeded",
            ReceivedAtUtc = TodayFixed.AddHours(-3),
            RawPayload = StripeChargeSucceededPayload(bookingId, 100m),
            Processed = true,
        });
        db.WalletTransactions.Add(new WalletTransaction
        {
            TxId = Guid.NewGuid(),
            WalletId = Guid.NewGuid(),
            BookingId = bookingId,
            EntryType = WalletEntryType.Commit,
            Amount = 103m, // £3 drift — Low.
            SignedAmount = -103m,
            Currency = "GBP",
            IdempotencyKey = $"wallet-drift-low-{Guid.NewGuid()}",
            CreatedAtUtc = TodayFixed.AddHours(-3),
        });
        await db.SaveChangesAsync();

        var service = new PaymentReconciliationService(db, clock, NullLogger<PaymentReconciliationService>.Instance);
        await service.ScanAsync(CancellationToken.None);

        var row = await db.ReconciliationQueue.SingleAsync();
        row.DiscrepancyType.Should().Be("AmountDrift");
        row.Severity.Should().Be("Low");
    }

    [Fact]
    public async Task detects_amount_drift_and_tags_high_severity_when_over_five_pounds()
    {
        var db = NewDb();
        var clock = new FakeTimeProvider(TodayFixed);

        var bookingId = Guid.NewGuid();
        db.StripeWebhookEvents.Add(new StripeWebhookEvent
        {
            EventId = "evt_drift_high",
            EventType = "charge.succeeded",
            ReceivedAtUtc = TodayFixed.AddHours(-3),
            RawPayload = StripeChargeSucceededPayload(bookingId, 100m),
            Processed = true,
        });
        db.WalletTransactions.Add(new WalletTransaction
        {
            TxId = Guid.NewGuid(),
            WalletId = Guid.NewGuid(),
            BookingId = bookingId,
            EntryType = WalletEntryType.Commit,
            Amount = 120m, // £20 drift — High.
            SignedAmount = -120m,
            Currency = "GBP",
            IdempotencyKey = $"wallet-drift-high-{Guid.NewGuid()}",
            CreatedAtUtc = TodayFixed.AddHours(-3),
        });
        await db.SaveChangesAsync();

        var service = new PaymentReconciliationService(db, clock, NullLogger<PaymentReconciliationService>.Instance);
        await service.ScanAsync(CancellationToken.None);

        var row = await db.ReconciliationQueue.SingleAsync();
        row.DiscrepancyType.Should().Be("AmountDrift");
        row.Severity.Should().Be("High");
    }

    [Fact]
    public async Task flags_unprocessed_event_older_than_one_hour()
    {
        var db = NewDb();
        var clock = new FakeTimeProvider(TodayFixed);

        db.StripeWebhookEvents.Add(new StripeWebhookEvent
        {
            EventId = "evt_unprocessed",
            EventType = "charge.succeeded",
            ReceivedAtUtc = TodayFixed.AddHours(-2),
            RawPayload = "{}",
            Processed = false,
        });
        await db.SaveChangesAsync();

        var service = new PaymentReconciliationService(db, clock, NullLogger<PaymentReconciliationService>.Instance);
        await service.ScanAsync(CancellationToken.None);

        var queue = await db.ReconciliationQueue.ToListAsync();
        queue.Should().Contain(q =>
            q.DiscrepancyType == "UnprocessedEvent" &&
            q.StripeEventId == "evt_unprocessed");
    }

    [Fact]
    public async Task does_not_flag_matched_pair_within_tolerance()
    {
        var db = NewDb();
        var clock = new FakeTimeProvider(TodayFixed);

        var bookingId = Guid.NewGuid();
        db.StripeWebhookEvents.Add(new StripeWebhookEvent
        {
            EventId = "evt_match",
            EventType = "charge.succeeded",
            ReceivedAtUtc = TodayFixed.AddHours(-3),
            RawPayload = StripeChargeSucceededPayload(bookingId, 100m),
            Processed = true,
        });
        db.WalletTransactions.Add(new WalletTransaction
        {
            TxId = Guid.NewGuid(),
            WalletId = Guid.NewGuid(),
            BookingId = bookingId,
            EntryType = WalletEntryType.Commit,
            Amount = 100m,
            SignedAmount = -100m,
            Currency = "GBP",
            IdempotencyKey = $"wallet-match-{Guid.NewGuid()}",
            CreatedAtUtc = TodayFixed.AddHours(-3),
        });
        await db.SaveChangesAsync();

        var service = new PaymentReconciliationService(db, clock, NullLogger<PaymentReconciliationService>.Instance);
        await service.ScanAsync(CancellationToken.None);

        (await db.ReconciliationQueue.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task rescan_is_idempotent_by_stripe_event_id_and_booking_id()
    {
        var db = NewDb();
        var clock = new FakeTimeProvider(TodayFixed);

        var bookingId = Guid.NewGuid();
        db.StripeWebhookEvents.Add(new StripeWebhookEvent
        {
            EventId = "evt_idempotent",
            EventType = "charge.succeeded",
            ReceivedAtUtc = TodayFixed.AddHours(-6),
            RawPayload = StripeChargeSucceededPayload(bookingId, 100m),
            Processed = true,
        });
        await db.SaveChangesAsync();

        var service = new PaymentReconciliationService(db, clock, NullLogger<PaymentReconciliationService>.Instance);
        await service.ScanAsync(CancellationToken.None);
        await service.ScanAsync(CancellationToken.None);

        var queue = await db.ReconciliationQueue.ToListAsync();
        queue.Should().HaveCount(1);
    }

    [Fact]
    public async Task list_endpoint_returns_pending_items_ordered_by_detected_desc()
    {
        var db = NewDb();
        var clock = new FakeTimeProvider(TodayFixed);

        db.ReconciliationQueue.AddRange(
            new PaymentReconciliationItem
            {
                Id = Guid.NewGuid(),
                DiscrepancyType = "OrphanStripeEvent",
                Severity = "Medium",
                StripeEventId = "evt_a",
                DetectedAtUtc = TodayFixed.AddDays(-2),
                Status = "Pending",
                Details = "{}",
            },
            new PaymentReconciliationItem
            {
                Id = Guid.NewGuid(),
                DiscrepancyType = "OrphanWalletRow",
                Severity = "High",
                BookingId = Guid.NewGuid(),
                DetectedAtUtc = TodayFixed.AddHours(-1),
                Status = "Pending",
                Details = "{}",
            });
        await db.SaveChangesAsync();

        var controller = new ReconciliationController(
            db, clock, NullLogger<ReconciliationController>.Instance);
        controller.ControllerContext = Ctx("ops-read-1", "ops-read");

        var result = await controller.List(
            new ReconciliationController.ListQuery(), CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ReconciliationController.ListResponse>(ok.Value);

        body.Rows.Should().HaveCount(2);
        body.Rows[0].DetectedAtUtc.Should().BeAfter(body.Rows[1].DetectedAtUtc);
    }

    [Fact]
    public async Task resolve_endpoint_flips_status_and_stamps_actor_and_notes()
    {
        var db = NewDb();
        var clock = new FakeTimeProvider(TodayFixed);

        var id = Guid.NewGuid();
        db.ReconciliationQueue.Add(new PaymentReconciliationItem
        {
            Id = id,
            DiscrepancyType = "AmountDrift",
            Severity = "High",
            BookingId = Guid.NewGuid(),
            StripeEventId = "evt_resolve",
            DetectedAtUtc = TodayFixed.AddHours(-1),
            Status = "Pending",
            Details = "{}",
        });
        await db.SaveChangesAsync();

        var controller = new ReconciliationController(
            db, clock, NullLogger<ReconciliationController>.Instance);
        controller.ControllerContext = Ctx("ops-finance-1", "ops-finance");

        var result = await controller.Resolve(
            id,
            new ReconciliationController.ResolveRequest { Notes = "Corrected via manual credit request WC-123" },
            CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        await db.Entry(db.ReconciliationQueue.Single()).ReloadAsync();
        var row = db.ReconciliationQueue.Single();
        row.Status.Should().Be("Resolved");
        row.ResolvedBy.Should().Be("ops-finance-1");
        row.ResolutionNotes.Should().Contain("WC-123");
        row.ResolvedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task resolve_endpoint_returns_404_for_unknown_id()
    {
        var db = NewDb();
        var clock = new FakeTimeProvider(TodayFixed);

        var controller = new ReconciliationController(
            db, clock, NullLogger<ReconciliationController>.Instance);
        controller.ControllerContext = Ctx("ops-finance-1", "ops-finance");

        var result = await controller.Resolve(
            Guid.NewGuid(),
            new ReconciliationController.ResolveRequest { Notes = "n/a" },
            CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        problem.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}
