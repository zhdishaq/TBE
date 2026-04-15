using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TBE.Contracts.Events;
using TBE.NotificationService.Application.Consumers;
using TBE.NotificationService.Application.Contacts;
using TBE.NotificationService.Application.Email;
using TBE.NotificationService.Application.Pdf;
using TBE.NotificationService.Application.Persistence;
using Xunit;

namespace TBE.Tests.Unit.NotificationService;

/// <summary>
/// NOTF-01 + NOTF-06 consumer-level contract tests for <see cref="BookingConfirmedConsumer"/>,
/// using MassTransit's <see cref="InMemoryTestHarness"/> + SQLite in-memory (so the NOTF-06
/// unique index is enforced for real). Fakes stand in for SendGrid / RazorLight / QuestPDF
/// / BookingService HTTP lookup so the test exercises the consumer-to-delivery wiring only.
/// </summary>
[Trait("Category", "Unit")]
public sealed class BookingConfirmedConsumerTests : IAsyncLifetime
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

    private sealed class FakeContacts : IBookingContactClient
    {
        public Task<BookingContact?> GetContactAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<BookingContact?>(new BookingContact("alice@example.com", "Alice", null));
    }

    private sealed class FakeRenderer : IEmailTemplateRenderer
    {
        public Task<RenderedEmail> RenderAsync<T>(string key, T model, CancellationToken ct) =>
            Task.FromResult(new RenderedEmail("Subject", "<p>html</p>", "text"));
    }

    private sealed class FakePdf : IETicketPdfGenerator
    {
        public byte[] Generate(ETicketDocumentModel m) => new byte[] { 0x25, 0x50, 0x44, 0x46 }; // "%PDF"
    }

    private sealed class FakeDelivery : IEmailDelivery
    {
        public int CallCount;
        public bool ShouldSucceed = true;
        public Task<EmailDeliveryResult> SendAsync(EmailEnvelope e, CancellationToken ct)
        {
            Interlocked.Increment(ref CallCount);
            return Task.FromResult(ShouldSucceed
                ? new EmailDeliveryResult(true, "sg-msg-1", null)
                : new EmailDeliveryResult(false, null, "boom"));
        }
    }

    private async Task<(InMemoryTestHarness harness, FakeDelivery delivery)> StartHarness(bool deliverySucceeds = true)
    {
        var delivery = new FakeDelivery { ShouldSucceed = deliverySucceeds };
        var ctx = BuildContext();

        var harness = new InMemoryTestHarness();
        var consumerHarness = harness.Consumer(() =>
            new BookingConfirmedConsumer(
                ctx,
                new FakeContacts(),
                new FakeRenderer(),
                new FakePdf(),
                delivery,
                new Microsoft.Extensions.Logging.Abstractions.NullLogger<BookingConfirmedConsumer>()));

        await harness.Start();
        return (harness, delivery);
    }

    [Fact]
    public async Task NOTF01_publishes_BookingConfirmed_triggers_delivery_and_records_idempotency()
    {
        var (harness, delivery) = await StartHarness();
        try
        {
            var eventId = Guid.NewGuid();
            await harness.Bus.Publish(
                new BookingConfirmed(Guid.NewGuid(), eventId, "BR-1", "PNR1", "TKT-1", "pi_1", DateTimeOffset.UtcNow),
                ctx => ctx.MessageId = eventId);

            (await harness.Consumed.Any<BookingConfirmed>()).Should().BeTrue();
            delivery.CallCount.Should().Be(1);

            using var read = BuildContext();
            read.EmailIdempotencyLogs.Should().HaveCount(1);
            read.EmailIdempotencyLogs.Single().ProviderMessageId.Should().Be("sg-msg-1");
        }
        finally { await harness.Stop(); }
    }

    [Fact]
    public async Task NOTF06_duplicate_messageId_skips_SendGrid()
    {
        var (harness, delivery) = await StartHarness();
        try
        {
            var messageId = Guid.NewGuid();
            var evt = new BookingConfirmed(Guid.NewGuid(), messageId, "BR-1", "PNR1", "TKT-1", "pi_1", DateTimeOffset.UtcNow);

            await harness.Bus.Publish(evt, c => c.MessageId = messageId);
            await harness.Consumed.Any<BookingConfirmed>();

            // Re-publish with SAME MessageId — consumer MUST ack-and-skip via unique-index violation.
            await harness.Bus.Publish(evt, c => c.MessageId = messageId);

            // Give MassTransit a beat to process the second delivery.
            await Task.Delay(200);

            delivery.CallCount.Should().Be(1, "NOTF-06 idempotency must prevent duplicate SendGrid calls");
            using var read = BuildContext();
            read.EmailIdempotencyLogs.Should().HaveCount(1);
        }
        finally { await harness.Stop(); }
    }

    [Fact]
    public async Task NOTF01_failed_delivery_throws_to_trigger_MassTransit_retry()
    {
        var (harness, _) = await StartHarness(deliverySucceeds: false);
        try
        {
            var eventId = Guid.NewGuid();
            await harness.Bus.Publish(
                new BookingConfirmed(Guid.NewGuid(), eventId, "BR-2", "PNR2", "TKT-2", "pi_2", DateTimeOffset.UtcNow),
                c => c.MessageId = eventId);

            // The consumer throws InvalidOperationException which the harness records as a fault.
            (await harness.Consumed.Any<BookingConfirmed>()).Should().BeTrue();
            (await harness.Published.Any<Fault<BookingConfirmed>>()).Should().BeTrue(
                "failed SendGrid delivery must surface as a Fault so MassTransit retry/dead-letter kicks in");
        }
        finally { await harness.Stop(); }
    }
}
