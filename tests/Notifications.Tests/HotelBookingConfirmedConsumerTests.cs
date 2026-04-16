using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using TBE.Contracts.Events;
using TBE.NotificationService.API.Templates.Models;
using TBE.NotificationService.Application.Consumers;
using TBE.NotificationService.Application.Email;
using TBE.NotificationService.Application.Pdf;
using TBE.NotificationService.Application.Persistence;
using Xunit;

namespace Notifications.Tests;

/// <summary>
/// NOTF-02 consumer-level contract tests for <see cref="HotelBookingConfirmedConsumer"/>.
/// Uses SQLite in-memory (so the NOTF-06 unique index is enforced for real) + MassTransit's
/// <see cref="InMemoryTestHarness"/>. Fakes stand in for SendGrid / RazorLight / QuestPDF so
/// the test exercises the consumer-to-delivery wiring only. Replaces the Wave 0 red
/// placeholders authored in Plan 04-00 Task 3.
/// </summary>
[Trait("Category", "Unit")]
public sealed class HotelBookingConfirmedConsumerTests : IAsyncLifetime
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

    private static HotelBookingConfirmed SampleEvent(Guid? bookingId = null, Guid? eventId = null) =>
        new(
            BookingId: bookingId ?? Guid.NewGuid(),
            EventId: eventId ?? Guid.NewGuid(),
            BookingReference: "HB-0001",
            SupplierRef: "SUP-ABC-123",
            PropertyName: "The Grand Sample",
            AddressLine: "1 Example Street, London",
            CheckInDate: new DateOnly(2026, 5, 1),
            CheckOutDate: new DateOnly(2026, 5, 4),
            Rooms: 1,
            Adults: 2,
            Children: 0,
            TotalAmount: 456.00m,
            Currency: "GBP",
            GuestEmail: "alice@example.com",
            GuestFullName: "Alice Example",
            At: DateTimeOffset.UtcNow);

    private sealed class FakeRenderer : IEmailTemplateRenderer
    {
        public string? LastKey;
        public Task<RenderedEmail> RenderAsync<T>(string key, T model, CancellationToken ct)
        {
            LastKey = key;
            return Task.FromResult(new RenderedEmail("Hotel Voucher — SUP-ABC-123", "<p>html</p>", "text"));
        }
    }

    private sealed class FakePdf : IHotelVoucherPdfGenerator
    {
        public int CallCount;
        public byte[] Generate(HotelVoucherModel m)
        {
            Interlocked.Increment(ref CallCount);
            return new byte[] { 0x25, 0x50, 0x44, 0x46, 0x01, 0x02, 0x03 }; // "%PDF..."
        }
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
                ? new EmailDeliveryResult(true, "sg-msg-1", null)
                : new EmailDeliveryResult(false, null, "boom"));
        }
    }

    private static IOptions<BrandOptions> Brand() =>
        Options.Create(new BrandOptions { BrandName = "TBE Travel", SupportPhone = "+44 20 0000 0000" });

    private HotelBookingConfirmedConsumer NewConsumer(
        NotificationDbContext ctx,
        IEmailTemplateRenderer renderer,
        IHotelVoucherPdfGenerator pdf,
        IEmailDelivery delivery) =>
        new(ctx, renderer, pdf, delivery, Brand(),
            NullLogger<HotelBookingConfirmedConsumer>.Instance);

    [Fact]
    public async Task Consume_inserts_EmailIdempotencyLog_with_HotelVoucher_type()
    {
        var renderer = new FakeRenderer();
        var pdf = new FakePdf();
        var delivery = new FakeDelivery();

        var harness = new InMemoryTestHarness();
        harness.Consumer(() => NewConsumer(BuildContext(), renderer, pdf, delivery));

        await harness.Start();
        try
        {
            var evt = SampleEvent();
            await harness.Bus.Publish(evt, c => c.MessageId = evt.EventId);

            (await harness.Consumed.Any<HotelBookingConfirmed>()).Should().BeTrue();

            // Deterministic check: the consumer should have fired delivery exactly once.
            delivery.CallCount.Should().Be(1);
            pdf.CallCount.Should().Be(1);
            renderer.LastKey.Should().Be(EmailType.HotelVoucher);

            delivery.LastEnvelope.Should().NotBeNull();
            delivery.LastEnvelope!.Attachments.Should().HaveCount(1);
            delivery.LastEnvelope.Attachments[0].FileName.Should().Be("voucher.pdf");
            delivery.LastEnvelope.Attachments[0].ContentType.Should().Be("application/pdf");
            delivery.LastEnvelope.ToEmail.Should().Be("alice@example.com");

            using var read = BuildContext();
            read.EmailIdempotencyLogs.Should().HaveCount(1);
            var row = read.EmailIdempotencyLogs.Single();
            row.EmailType.Should().Be(EmailType.HotelVoucher);
            row.EventId.Should().Be(evt.EventId);
            row.BookingId.Should().Be(evt.BookingId);
            row.Recipient.Should().Be("alice@example.com");
            row.ProviderMessageId.Should().Be("sg-msg-1");
        }
        finally { await harness.Stop(); }
    }

    [Fact]
    public async Task Consume_duplicate_event_is_swallowed()
    {
        var renderer = new FakeRenderer();
        var pdf = new FakePdf();
        var delivery = new FakeDelivery();

        var harness = new InMemoryTestHarness();
        harness.Consumer(() => NewConsumer(BuildContext(), renderer, pdf, delivery));

        await harness.Start();
        try
        {
            var evt = SampleEvent();

            await harness.Bus.Publish(evt, c => c.MessageId = evt.EventId);
            await harness.Consumed.Any<HotelBookingConfirmed>();

            // Re-publish with SAME MessageId — consumer MUST ack-and-skip via unique-index violation (NOTF-06 / D-19).
            await harness.Bus.Publish(evt, c => c.MessageId = evt.EventId);
            await Task.Delay(200);

            delivery.CallCount.Should().Be(1, "NOTF-06 idempotency must prevent duplicate SendGrid calls");
            pdf.CallCount.Should().Be(1, "duplicate events must not regenerate the PDF");

            using var read = BuildContext();
            read.EmailIdempotencyLogs.Should().HaveCount(1);
        }
        finally { await harness.Stop(); }
    }

    [Fact]
    public async Task Consume_sends_email_with_voucher_attachment()
    {
        var renderer = new FakeRenderer();
        var pdf = new FakePdf();
        var delivery = new FakeDelivery();

        var harness = new InMemoryTestHarness();
        harness.Consumer(() => NewConsumer(BuildContext(), renderer, pdf, delivery));

        await harness.Start();
        try
        {
            var evt = SampleEvent();
            await harness.Bus.Publish(evt, c => c.MessageId = evt.EventId);
            (await harness.Consumed.Any<HotelBookingConfirmed>()).Should().BeTrue();

            delivery.LastEnvelope.Should().NotBeNull();
            delivery.LastEnvelope!.Subject.Should().Contain("Hotel Voucher");
            delivery.LastEnvelope.Attachments.Should().HaveCount(1);

            var attachment = delivery.LastEnvelope.Attachments[0];
            attachment.FileName.Should().Be("voucher.pdf");
            attachment.Content.Should().NotBeNullOrEmpty();
            attachment.Content[0].Should().Be(0x25); // "%"
            attachment.Content[1].Should().Be(0x50); // "P"
            attachment.Content[2].Should().Be(0x44); // "D"
            attachment.Content[3].Should().Be(0x46); // "F"
        }
        finally { await harness.Stop(); }
    }

    [Fact]
    public async Task Consume_delivery_failure_throws_to_trigger_retry()
    {
        var renderer = new FakeRenderer();
        var pdf = new FakePdf();
        var delivery = new FakeDelivery { ShouldSucceed = false };

        var harness = new InMemoryTestHarness();
        harness.Consumer(() => NewConsumer(BuildContext(), renderer, pdf, delivery));

        await harness.Start();
        try
        {
            var evt = SampleEvent();
            await harness.Bus.Publish(evt, c => c.MessageId = evt.EventId);

            (await harness.Consumed.Any<HotelBookingConfirmed>()).Should().BeTrue();
            (await harness.Published.Any<Fault<HotelBookingConfirmed>>()).Should().BeTrue(
                "failed SendGrid delivery must surface as a Fault so MassTransit retry/dead-letter kicks in");
        }
        finally { await harness.Stop(); }
    }
}
