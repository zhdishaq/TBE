using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using SendGrid;
using SendGrid.Helpers.Mail;
using TBE.NotificationService.Application.Email;
using TBE.NotificationService.Infrastructure.Email;
using Xunit;

namespace TBE.Tests.Unit.NotificationService;

[Trait("Category", "Unit")]
public sealed class SendGridEmailDeliveryTests
{
    private static SendGridOptions ValidOptions() => new()
    {
        ApiKey = "SG.fake-key",
        FromEmail = "no-reply@tbe.travel",
        FromName = "TBE Bookings",
        SandboxMode = "false"
    };

    private static EmailEnvelope SampleEnvelope(IReadOnlyList<EmailAttachment>? attachments = null) =>
        new(
            ToEmail: "alice@example.com",
            ToName: "Alice",
            Subject: "Booking Confirmed — ABC123",
            HtmlBody: "<html><body><h1>Hello Alice</h1></body></html>",
            PlainTextBody: null,
            Attachments: attachments ?? Array.Empty<EmailAttachment>());

    private static Response AcceptedResponse(string? messageId = "smid-123")
    {
        var headers = new HttpResponseMessage().Headers;
        if (messageId is not null) headers.Add("X-Message-Id", messageId);
        return new Response(HttpStatusCode.Accepted, new StringContent(""), headers);
    }

    [Fact]
    public async Task NOTF01_sends_email_with_correct_recipient_subject_html()
    {
        var client = Substitute.For<ISendGridClient>();
        SendGridMessage? captured = null;
        client.SendEmailAsync(Arg.Do<SendGridMessage>(m => captured = m), Arg.Any<CancellationToken>())
              .Returns(AcceptedResponse());

        var sut = new SendGridEmailDelivery(client, Options.Create(ValidOptions()), NullLogger<SendGridEmailDelivery>.Instance);

        var result = await sut.SendAsync(SampleEnvelope(), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ProviderMessageId.Should().Be("smid-123");
        captured.Should().NotBeNull();
        captured!.From.Email.Should().Be("no-reply@tbe.travel");
        captured.Subject.Should().Be("Booking Confirmed — ABC123");
        captured.HtmlContent.Should().Contain("Hello Alice");
        captured.Personalizations.Should().ContainSingle()
            .Which.Tos.Should().ContainSingle()
            .Which.Email.Should().Be("alice@example.com");
    }

    [Fact]
    public async Task NOTF01_attaches_pdf_with_correct_filename_and_contenttype()
    {
        var client = Substitute.For<ISendGridClient>();
        SendGridMessage? captured = null;
        client.SendEmailAsync(Arg.Do<SendGridMessage>(m => captured = m), Arg.Any<CancellationToken>())
              .Returns(AcceptedResponse());

        var sut = new SendGridEmailDelivery(client, Options.Create(ValidOptions()), NullLogger<SendGridEmailDelivery>.Instance);

        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }; // %PDF-1.4
        var envelope = SampleEnvelope(new[]
        {
            new EmailAttachment("eticket.pdf", "application/pdf", pdfBytes, "eticket.pdf")
        });

        await sut.SendAsync(envelope, CancellationToken.None);

        captured!.Attachments.Should().ContainSingle();
        var att = captured.Attachments[0];
        att.Filename.Should().Be("eticket.pdf");
        att.Type.Should().Be("application/pdf");
        att.Content.Should().Be(Convert.ToBase64String(pdfBytes));
        att.ContentId.Should().Be("eticket.pdf");
    }

    [Fact]
    public async Task NOTF01_failure_response_is_surfaced_as_unsuccessful_result()
    {
        var client = Substitute.For<ISendGridClient>();
        client.SendEmailAsync(Arg.Any<SendGridMessage>(), Arg.Any<CancellationToken>())
              .Returns(new Response(HttpStatusCode.BadRequest, new StringContent("{\"error\":\"bad\"}"), new HttpResponseMessage().Headers));

        var sut = new SendGridEmailDelivery(client, Options.Create(ValidOptions()), NullLogger<SendGridEmailDelivery>.Instance);

        var result = await sut.SendAsync(SampleEnvelope(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorReason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void NOTF01_missing_apikey_throws_at_construction()
    {
        var client = Substitute.For<ISendGridClient>();
        var opts = Options.Create(new SendGridOptions { ApiKey = "" });

        Action act = () => _ = new SendGridEmailDelivery(client, opts, NullLogger<SendGridEmailDelivery>.Instance);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*SendGrid*ApiKey*");
    }

    [Fact]
    public async Task NOTF01_raw_body_never_appears_in_logger()
    {
        var client = Substitute.For<ISendGridClient>();
        client.SendEmailAsync(Arg.Any<SendGridMessage>(), Arg.Any<CancellationToken>())
              .Returns(new Response(HttpStatusCode.BadRequest, new StringContent("fail"), new HttpResponseMessage().Headers));

        var log = new RecordingLogger<SendGridEmailDelivery>();
        var sut = new SendGridEmailDelivery(client, Options.Create(ValidOptions()), log);

        var envelope = SampleEnvelope() with { ToEmail = "sensitive-pii@example.com" };
        await sut.SendAsync(envelope, CancellationToken.None);

        // Raw recipient and raw HTML body must NEVER appear in any log entry (T-03-18 mitigation).
        log.AllMessages.Any(m => m.Contains("sensitive-pii@example.com")).Should().BeFalse();
        log.AllMessages.Any(m => m.Contains(envelope.HtmlBody)).Should().BeFalse();
    }

    // --- helpers ---
    private sealed class NullLogger<T> : ILogger<T>
    {
        public static readonly NullLogger<T> Instance = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> AllMessages { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            AllMessages.Add(formatter(state, exception));
        }
    }
}
