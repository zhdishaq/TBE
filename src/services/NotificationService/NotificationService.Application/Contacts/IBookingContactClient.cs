namespace TBE.NotificationService.Application.Contacts;

/// <summary>
/// Typed wrapper around <c>HttpClient</c> that fetches customer contact details for a
/// booking from BookingService's internal endpoint <c>GET /api/bookings/{id}/contact</c>.
/// Returns <c>null</c> when the booking/contact cannot be found (NotFound, Forbidden).
/// </summary>
public interface IBookingContactClient
{
    Task<BookingContact?> GetContactAsync(Guid bookingId, CancellationToken ct);
}

public sealed record BookingContact(string Email, string Name, Guid? AgencyId);
