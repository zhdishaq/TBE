using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TBE.Contracts.Events;
using TBE.NotificationService.API.Templates.Models;
using TBE.NotificationService.Application.Consumers;
using TBE.NotificationService.Application.Email;
using TBE.NotificationService.Application.Pdf;
using TBE.NotificationService.Application.Persistence;
using Xunit;

namespace Notifications.Tests;

/// <summary>
/// Plan 04-04 Task 2 — contract tests for <see cref="BasketConfirmedConsumer"/>. Replaces
/// the Wave 0 RED placeholders authored in Plan 04-00 Task 3. Uses SQLite in-memory so
/// the NOTF-06 unique index on <c>(EventId, EmailType)</c> is actually enforced.
/// <para>
/// Covers PKG-03 (one email for the whole basket), D-09 partial-success copy (single
/// "ONE charge on your statement" disclosure), and NOTF-06 replay safety.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
public sealed class BasketConfirmedConsumerTests : IAsyncLifetime
{
    private SqliteConnection _conn = null!;

    public Task InitializeAsync()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        using var ctx = BuildContext();
        ctx.Database.EnsureCreated();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() { _conn.Dispose(); return Task.CompletedTask; }

    private NotificationDbContext BuildContext() =>
        new(new DbContextOptionsBuilder<NotificationDbContext>().UseSqlite(_conn).Options);

    private static BasketConfirmed FullSuccessEvent(Guid? basketId = null, Guid? eventId = null) =>
        new(
            BasketId: basketId ?? Guid.NewGuid(),
            EventId: eventId ?? Guid.NewGuid(),
            FlightBookingReference: "FB-1234",
            HotelSupplierRef: "SUP-ABC-123",
            GuestEmail: "alice@example.com",
            GuestFullName: "Alice Example",
            TotalAmount: 706.00m,
            Currency: "GBP",
            At: DateTimeOffset.UtcNow);

    private static BasketPartiallyConfirmed PartialFailureEvent(Guid? basketId = null, Guid? eventId = null) =>
        new(
            BasketId: basketId ?? Guid.NewGuid(),
            EventId: eventId ?? Guid.NewGuid(),
            SucceededComponent: "Flight",
            FailedComponent: "Hotel",
            FlightBookingReference: "FB-1234",
            HotelSupplierRef: null,
            GuestEmail: "alice@example.com",
            GuestFullName: "Alice Example",
            ChargedAmount: 250.00m,
            RefundedAmount: 456.00m,
            Currency: "GBP",
            Cause: "hotel supplier inventory unavailable",
            At: DateTimeOffset.UtcNow);

    private sealed class FakeRenderer : IEmailTemplateRenderer
    {
        public string? LastKey;
        public object? LastModel;
        public string SubjectToReturn = "Your trip is confirmed";
        public string HtmlToReturn = "<p>html — ONE charge on your statement</p>";
        public Task<RenderedEmail> RenderAsync<T>(string key, T model, CancellationToken ct)
        {
            LastKey = key;
            LastModel = model;
            return Task.FromResult(new RenderedEmail(SubjectToReturn, HtmlToReturn, "text"));
        }
    }

    private sealed class FakeFlightPdf : IETicketPdfGenerator
    {
        public int CallCount;
        public byte[] Generate(ETicketDocumentModel m) { Interlocked.Increment(ref CallCount); return new byte[] { 0x25, 0x50, 0x44, 0x46 }; }
    }

    private sealed class FakeHotelPdf : IHotelVoucherPdfGenerator
    {
        public int CallCount;
        public byte[] Generate(HotelVoucherModel m) { Interlocked.Increment(ref CallCount); return new byte[] { 0x25, 0x50, 0x44, 0x46 }; }
    }

    private sealed class FakeDelivery : IEmailDelivery
    {
        public int CallCount;
        public EmailEnvelope? LastEnvelope;
        public bool ShouldSucceed = true;
        public Task<EmailDeliveryResult> SendAsync(EmailEnvelope e, CancellationToken ct)
        {
            Interlocked.Increment(ref CallCount);
            LastEnvelope = e;
            return Task.FromResult(ShouldSucceed
                ? new EmailDeliveryResult(true, "sg-msg-basket", null)
                : new EmailDeliveryResult(false, null, "boom"));
        }
    }

    private static IOptions<BrandOptions> Brand() =>
        Options.Create(new BrandOptions { BrandName = "TBE Travel", SupportPhone = "+44 20 0000 0000" });

    private BasketConfirmedConsumer NewConsumer(
        NotificationDbContext ctx,
        IEmailTemplateRenderer renderer,
        IETicketPdfGenerator flightPdf,
        IHotelVoucherPdfGenerator hotelPdf,
        IEmailDelivery delivery) =>
        new(ctx, renderer, flightPdf, hotelPdf, delivery, Brand(),
            NullLogger<BasketConfirmedConsumer>.Instance);

    [Fact]
    public async Task Consume_full_success_sends_single_email_with_two_attachments()
    {
        var renderer = new FakeRenderer();
        var flightPdf = new FakeFlightPdf();
        var hotelPdf = new FakeHotelPdf();
        var delivery = new FakeDelivery();

        var harness = new InMemoryTestHarness();
        harness.Consumer(() => NewConsumer(BuildContext(), renderer, flightPdf, hotelPdf, delivery));

        await harness.Start();
        try
        {
            var evt = FullSuccessEvent();
            await harness.Bus.Publish(evt, c => c.MessageId = evt.EventId);
            (await harness.Consumed.Any<BasketConfirmed>()).Should().BeTrue();

            delivery.CallCount.Should().Be(1, "PKG-03 — one email covers the full basket");
            renderer.LastKey.Should().Be(EmailType.BasketConfirmation);

            flightPdf.CallCount.Should().Be(1);
            hotelPdf.CallCount.Should().Be(1);

            delivery.LastEnvelope.Should().NotBeNull();
            delivery.LastEnvelope!.Attachments.Should().HaveCount(2,
                "full basket confirmation must attach BOTH the flight e-ticket and the hotel voucher");
            delivery.LastEnvelope.ToEmail.Should().Be("alice@example.com");

            using var read = BuildContext();
            read.EmailIdempotencyLogs.Should().HaveCount(1);
            var row = read.EmailIdempotencyLogs.Single();
            row.EmailType.Should().Be(EmailType.BasketConfirmation);
            row.EventId.Should().Be(evt.EventId);
            row.BookingId.Should().Be(evt.BasketId);
            row.ProviderMessageId.Should().Be("sg-msg-basket");
        }
        finally { await harness.Stop(); }
    }

    [Fact]
    public async Task Consume_partial_success_sends_single_email_with_one_attachment_and_discloses_single_charge()
    {
        var renderer = new FakeRenderer
        {
            SubjectToReturn = "Partial booking confirmation",
            HtmlToReturn = "<p>You'll see ONE charge on your statement of 250.00 GBP for the flight portion only.</p>",
        };
        var flightPdf = new FakeFlightPdf();
        var hotelPdf = new FakeHotelPdf();
        var delivery = new FakeDelivery();

        var harness = new InMemoryTestHarness();
        harness.Consumer(() => NewConsumer(BuildContext(), renderer, flightPdf, hotelPdf, delivery));

        await harness.Start();
        try
        {
            var evt = PartialFailureEvent();
            await harness.Bus.Publish(evt, c => c.MessageId = evt.EventId);
            (await harness.Consumed.Any<BasketPartiallyConfirmed>()).Should().BeTrue();

            delivery.CallCount.Should().Be(1, "D-09 — one email for the partial outcome too");
            renderer.LastKey.Should().Be(EmailType.BasketConfirmation,
                "same template key as full-success — the template itself branches on IsPartial");

            // D-09 — only the succeeded (flight) leg's PDF is attached.
            flightPdf.CallCount.Should().Be(1);
            hotelPdf.CallCount.Should().Be(0, "failed hotel leg has no voucher");

            delivery.LastEnvelope.Should().NotBeNull();
            delivery.LastEnvelope!.Attachments.Should().HaveCount(1);
            delivery.LastEnvelope.Attachments[0].FileName.Should().Be("eticket.pdf");
            delivery.LastEnvelope.Subject.Should().Contain("Partial");
            delivery.LastEnvelope.HtmlBody.Should().Contain("ONE charge on your statement",
                "D-09 pre-empts support tickets by using explicit single-statement-entry copy");

            // Model was flagged as partial with the correct amounts.
            var model = renderer.LastModel as BasketConfirmationModel;
            model.Should().NotBeNull();
            model!.IsPartial.Should().BeTrue();
            model.ChargedAmount.Should().Be(250.00m);
            model.RefundedAmount.Should().Be(456.00m);
            model.FlightSection.Should().NotBeNull();
            model.HotelSection.Should().BeNull();
        }
        finally { await harness.Stop(); }
    }

    [Fact]
    public async Task Consume_duplicate_event_is_swallowed()
    {
        var renderer = new FakeRenderer();
        var flightPdf = new FakeFlightPdf();
        var hotelPdf = new FakeHotelPdf();
        var delivery = new FakeDelivery();

        var harness = new InMemoryTestHarness();
        harness.Consumer(() => NewConsumer(BuildContext(), renderer, flightPdf, hotelPdf, delivery));

        await harness.Start();
        try
        {
            var evt = FullSuccessEvent();
            await harness.Bus.Publish(evt, c => c.MessageId = evt.EventId);
            await harness.Consumed.Any<BasketConfirmed>();

            // Same MessageId — consumer MUST ack-and-skip on the unique-index violation (NOTF-06).
            await harness.Bus.Publish(evt, c => c.MessageId = evt.EventId);
            await Task.Delay(200);

            delivery.CallCount.Should().Be(1, "NOTF-06 idempotency must block duplicate SendGrid calls");

            using var read = BuildContext();
            read.EmailIdempotencyLogs.Should().HaveCount(1);
        }
        finally { await harness.Stop(); }
    }
}
