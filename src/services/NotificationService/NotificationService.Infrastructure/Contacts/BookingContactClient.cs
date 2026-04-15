using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using TBE.NotificationService.Application.Contacts;

namespace TBE.NotificationService.Infrastructure.Contacts;

/// <summary>
/// HTTP-backed <see cref="IBookingContactClient"/>.
/// T-03-04 mitigation: expects BookingService to enforce <c>[Authorize]</c> +
/// <c>service-internal</c> role; the service-account JWT is injected via a
/// <c>DelegatingHandler</c> registered at the DI level.
/// </summary>
public sealed class BookingContactClient : IBookingContactClient
{
    private readonly HttpClient _http;
    private readonly ILogger<BookingContactClient> _log;

    public BookingContactClient(HttpClient http, ILogger<BookingContactClient> log)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task<BookingContact?> GetContactAsync(Guid bookingId, CancellationToken ct)
    {
        try
        {
            using var resp = await _http.GetAsync($"api/bookings/{bookingId}/contact", ct)
                .ConfigureAwait(false);
            if (resp.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.NoContent)
                return null;
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<BookingContact>(cancellationToken: ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            _log.LogWarning(ex, "BookingService contact lookup failed for booking {BookingId}", bookingId);
            return null;
        }
    }
}
