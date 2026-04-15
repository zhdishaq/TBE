using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using TBE.BookingService.Application.Consumers;
using TBE.BookingService.Application.Ttl;
using TBE.Contracts.Commands;
using TBE.Contracts.Events;
using Xunit;

namespace Booking.Saga.Tests;

/// <summary>
/// Verifies <see cref="CreatePnrConsumer"/> integrates <see cref="IFareRuleParser"/> correctly:
///   * parser success → <see cref="PnrCreated"/> with the parsed deadline
///   * parser failure → <see cref="PnrCreated"/> with D-07 fallback (UtcNow + 2h) AND
///     <see cref="FareRuleParseFailedAlert"/> for ops
///   * HTTP failure from FlightConnectorService → <see cref="PnrCreationFailed"/>
/// </summary>
public sealed class CreatePnrConsumerTests
{
    private static IHttpClientFactory HttpClientFactoryReturning(
        HttpStatusCode status,
        object? body = null,
        Exception? throwOnSend = null)
    {
        var handler = new StubHandler(status, body, throwOnSend);
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://flight-connector") };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("flight-connector").Returns(client);
        return factory;
    }

    [Fact]
    public async Task Successful_parse_publishes_PnrCreated_with_parsed_deadline()
    {
        var deadline = DateTime.UtcNow.AddHours(36);
        var parser = Substitute.For<IFareRuleParser>();
        parser.TryParse("amadeus", Arg.Any<string>(), out Arg.Any<DateTime>())
              .Returns(ci => { ci[2] = deadline; return true; });

        var factory = HttpClientFactoryReturning(
            HttpStatusCode.OK,
            new CreatePnrConsumer.CreatePnrResult("amadeus", "ABC123", "{\"lastTicketingDate\":\"2099-01-01\"}"));

        await using var provider = new ServiceCollection()
            .AddSingleton(factory)
            .AddSingleton(parser)
            .AddMassTransitTestHarness(x => x.AddConsumer<CreatePnrConsumer>())
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            var bookingId = Guid.NewGuid();
            await harness.Bus.Publish(new CreatePnrCommand(bookingId, "offer-tok", new[] { "pax-1" }));

            (await harness.Published.Any<PnrCreated>(
                x => x.Context.Message.BookingId == bookingId &&
                     x.Context.Message.Pnr == "ABC123" &&
                     x.Context.Message.TicketingDeadlineUtc == deadline)).Should().BeTrue();

            (await harness.Published.Any<FareRuleParseFailedAlert>()).Should().BeFalse();
        }
        finally { await harness.Stop(); }
    }

    [Fact]
    public async Task Parse_failure_publishes_fallback_deadline_and_alert()
    {
        var parser = Substitute.For<IFareRuleParser>();
        parser.TryParse(Arg.Any<string>(), Arg.Any<string>(), out Arg.Any<DateTime>())
              .Returns(ci => { ci[2] = default(DateTime); return false; });

        var factory = HttpClientFactoryReturning(
            HttpStatusCode.OK,
            new CreatePnrConsumer.CreatePnrResult("amadeus", "XYZ999", "garbled"));

        await using var provider = new ServiceCollection()
            .AddSingleton(factory)
            .AddSingleton(parser)
            .AddMassTransitTestHarness(x => x.AddConsumer<CreatePnrConsumer>())
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            var bookingId = Guid.NewGuid();
            var before = DateTime.UtcNow;
            await harness.Bus.Publish(new CreatePnrCommand(bookingId, "offer-tok", new[] { "pax-1" }));

            (await harness.Published.Any<FareRuleParseFailedAlert>(
                x => x.Context.Message.BookingId == bookingId &&
                     x.Context.Message.GdsCode == "amadeus" &&
                     !string.IsNullOrEmpty(x.Context.Message.RawPayloadDigest))).Should().BeTrue();

            (await harness.Published.Any<PnrCreated>(
                x => x.Context.Message.BookingId == bookingId &&
                     x.Context.Message.TicketingDeadlineUtc >= before.AddHours(2).AddMinutes(-1) &&
                     x.Context.Message.TicketingDeadlineUtc <= DateTime.UtcNow.AddHours(2).AddMinutes(1)))
                .Should().BeTrue();
        }
        finally { await harness.Stop(); }
    }

    [Fact]
    public async Task Http_failure_publishes_PnrCreationFailed()
    {
        var parser = Substitute.For<IFareRuleParser>();
        var factory = HttpClientFactoryReturning(
            HttpStatusCode.ServiceUnavailable,
            throwOnSend: new HttpRequestException("gds down"));

        await using var provider = new ServiceCollection()
            .AddSingleton(factory)
            .AddSingleton(parser)
            .AddMassTransitTestHarness(x => x.AddConsumer<CreatePnrConsumer>())
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            var bookingId = Guid.NewGuid();
            await harness.Bus.Publish(new CreatePnrCommand(bookingId, "offer-tok", new[] { "pax-1" }));

            (await harness.Published.Any<PnrCreationFailed>(
                x => x.Context.Message.BookingId == bookingId &&
                     x.Context.Message.Cause.Contains("gds http error", StringComparison.OrdinalIgnoreCase)))
                .Should().BeTrue();

            (await harness.Published.Any<PnrCreated>()).Should().BeFalse();
        }
        finally { await harness.Stop(); }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly object? _body;
        private readonly Exception? _throw;

        public StubHandler(HttpStatusCode status, object? body, Exception? throwOnSend)
        {
            _status = status;
            _body = body;
            _throw = throwOnSend;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (_throw != null) throw _throw;
            var resp = new HttpResponseMessage(_status);
            if (_body != null) resp.Content = JsonContent.Create(_body);
            return Task.FromResult(resp);
        }
    }
}
