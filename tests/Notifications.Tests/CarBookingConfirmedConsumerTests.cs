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
/// Plan 04-04 Task 2 — contract tests for <see cref="CarBookingConfirmedConsumer"/>
/// (CARB-03). SQLite in-memory so the NOTF-06 unique index is enforced for real.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CarBookingConfirmedConsumerTests : IAsyncLifetime
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

    private static CarBookingConfirmed SampleEvent(Guid? bookingId = null, Guid? eventId = null) =>
        new(
            BookingId: bookingId ?? Guid.NewGuid(),
            EventId: eventId ?? Guid.NewGuid(),
            BookingReference: "CB-0001",
            SupplierRef: "AVIS-XYZ-123",
            VendorName: "Avis",
            PickupLocation: "LHR Terminal 5",
            DropoffLocation: "LHR Terminal 5",
            PickupAtUtc: new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc),
            DropoffAtUtc: new DateTime(2026, 5, 4, 10, 0, 0, DateTimeKind.Utc),
            TotalAmount: 189.00m,
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
            return Task.FromResult(new RenderedEmail("Car Hire Voucher — AVIS-XYZ-123", "<p>html</p>", "text"));
        }
    }

    private sealed class FakePdf : ICarVoucherPdfGenerator
    {
        public int CallCount;
        public byte[] Generate(CarVoucherModel m)
        {
            Interlocked.Increment(ref CallCount);
            return new byte[] { 0x25, 0x50, 0x44, 0x46, 0x01, 0x02 };
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
                ? new EmailDeliveryResult(true, "sg-msg-car", null)
                : new EmailDeliveryResult(false, null, "boom"));
        }
    }

    private static IOptions<BrandOptions> Brand() =>
        Options.Create(new BrandOptions { BrandName = "TBE Travel", SupportPhone = "+44 20 0000 0000" });

    private CarBookingConfirmedConsumer NewConsumer(
        NotificationDbContext ctx,
        IEmailTemplateRenderer renderer,
        ICarVoucherPdfGenerator pdf,
        IEmailDelivery delivery) =>
        new(ctx, renderer, pdf, delivery, Brand(),
            NullLogger<CarBookingConfirmedConsumer>.Instance);

    [Fact]
    public async Task Consume_inserts_EmailIdempotencyLog_with_CarVoucher_type()
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
            (await harness.Consumed.Any<CarBookingConfirmed>()).Should().BeTrue();

            delivery.CallCount.Should().Be(1);
            pdf.CallCount.Should().Be(1);
            renderer.LastKey.Should().Be(EmailType.CarVoucher);

            delivery.LastEnvelope.Should().NotBeNull();
            delivery.LastEnvelope!.Attachments.Should().HaveCount(1);
            delivery.LastEnvelope.Attachments[0].FileName.Should().Be("car-voucher.pdf");
            delivery.LastEnvelope.Attachments[0].ContentType.Should().Be("application/pdf");
            delivery.LastEnvelope.ToEmail.Should().Be("alice@example.com");
            delivery.LastEnvelope.Subject.Should().Contain("Car Hire Voucher");

            using var read = BuildContext();
            read.EmailIdempotencyLogs.Should().HaveCount(1);
            var row = read.EmailIdempotencyLogs.Single();
            row.EmailType.Should().Be(EmailType.CarVoucher);
            row.EventId.Should().Be(evt.EventId);
            row.BookingId.Should().Be(evt.BookingId);
            row.Recipient.Should().Be("alice@example.com");
            row.ProviderMessageId.Should().Be("sg-msg-car");
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
            await harness.Consumed.Any<CarBookingConfirmed>();

            await harness.Bus.Publish(evt, c => c.MessageId = evt.EventId);
            await Task.Delay(200);

            delivery.CallCount.Should().Be(1, "NOTF-06 must prevent duplicate SendGrid calls");
            pdf.CallCount.Should().Be(1);

            using var read = BuildContext();
            read.EmailIdempotencyLogs.Should().HaveCount(1);
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

            (await harness.Consumed.Any<CarBookingConfirmed>()).Should().BeTrue();
            (await harness.Published.Any<Fault<CarBookingConfirmed>>()).Should().BeTrue(
                "failed SendGrid delivery must surface as a Fault so MassTransit retry/dead-letter kicks in");
        }
        finally { await harness.Stop(); }
    }
}
