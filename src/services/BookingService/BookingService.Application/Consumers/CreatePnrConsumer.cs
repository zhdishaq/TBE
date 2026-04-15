using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using MassTransit;
using Microsoft.Extensions.Logging;
using TBE.BookingService.Application.Ttl;
using TBE.Contracts.Commands;
using TBE.Contracts.Events;

namespace TBE.BookingService.Application.Consumers;

/// <summary>
/// Consumes <see cref="CreatePnrCommand"/> by POSTing to FlightConnectorService, then parses the
/// returned raw fare-rule payload via <see cref="IFareRuleParser"/> to extract
/// <c>TicketingDeadlineUtc</c>. On parse failure applies D-07 fallback (UtcNow + 2h) AND publishes
/// <see cref="FareRuleParseFailedAlert"/> so ops can investigate adapter drift.
/// </summary>
public sealed class CreatePnrConsumer : IConsumer<CreatePnrCommand>
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IFareRuleParser _parser;
    private readonly ILogger<CreatePnrConsumer> _log;

    public CreatePnrConsumer(
        IHttpClientFactory httpClientFactory,
        IFareRuleParser parser,
        ILogger<CreatePnrConsumer> log)
    {
        _httpClientFactory = httpClientFactory;
        _parser = parser;
        _log = log;
    }

    public async Task Consume(ConsumeContext<CreatePnrCommand> ctx)
    {
        var cmd = ctx.Message;
        try
        {
            var client = _httpClientFactory.CreateClient("flight-connector");
            var response = await client.PostAsJsonAsync(
                "/pnr",
                new { cmd.BookingId, cmd.OfferToken, cmd.PassengerRefs },
                ctx.CancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<CreatePnrResult>(
                cancellationToken: ctx.CancellationToken)
                ?? throw new InvalidOperationException("FlightConnectorService returned empty body");

            DateTime deadlineUtc;
            if (!_parser.TryParse(result.GdsCode, result.RawFareRule, out deadlineUtc))
            {
                // D-07 fallback — 2-hour conservative window when parser can't extract.
                deadlineUtc = DateTime.UtcNow.AddHours(2);
                _log.LogWarning(
                    "FareRuleParser failed for booking {BookingId} gds={GdsCode}; applying D-07 2h fallback",
                    cmd.BookingId, result.GdsCode);
                await ctx.Publish(new FareRuleParseFailedAlert(
                    cmd.BookingId,
                    result.GdsCode,
                    Sha256Hex(TruncateForDigest(result.RawFareRule)),
                    DateTimeOffset.UtcNow));
            }

            await ctx.Publish(new PnrCreated(cmd.BookingId, result.Pnr, deadlineUtc, DateTimeOffset.UtcNow));
        }
        catch (HttpRequestException ex)
        {
            _log.LogWarning(ex, "PNR creation HTTP failure for booking {BookingId}", cmd.BookingId);
            await ctx.Publish(new PnrCreationFailed(cmd.BookingId, $"gds http error: {ex.Message}"));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogError(ex, "PNR creation failed unexpectedly for booking {BookingId}", cmd.BookingId);
            await ctx.Publish(new PnrCreationFailed(cmd.BookingId, ex.Message));
            throw; // let MassTransit retry per service-level retry policy
        }
    }

    private static string TruncateForDigest(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        return raw.Length <= 1024 ? raw : raw[..1024];
    }

    private static string Sha256Hex(string input) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input ?? string.Empty)));

    /// <summary>
    /// FlightConnectorService /pnr response shape. Kept internal — contract owned by the
    /// connector team; when they ship a public DTO this can be switched over.
    /// </summary>
    public sealed record CreatePnrResult(string GdsCode, string Pnr, string RawFareRule);
}
