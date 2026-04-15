namespace TBE.NotificationService.Application.Email;

/// <summary>
/// Canonical email-type discriminators. Each value also doubles as the Razor template key
/// (cshtml filename minus extension) and as the second component of the
/// <c>EmailIdempotencyLog (EventId, EmailType)</c> unique key (NOTF-06).
/// </summary>
public static class EmailType
{
    public const string FlightConfirmation = "FlightConfirmation";                         // NOTF-01 (customer confirmation + e-ticket PDF)
    public const string FlightCancellation = "FlightCancellation";                         // NOTF-03 (customer cancellation + refund)
    public const string TicketIssued = "TicketIssued";                                     // internal support for NOTF-01 (PDF lifecycle)
    public const string BookingExpired = "BookingExpired";                                 // supporting — NOTF-03 family (PNR expiry)
    public const string TicketingDeadlineApproaching = "TicketingDeadlineApproaching";     // NOTF-04 (B2B 24h + 2h alerts)
    public const string WalletLowBalance = "WalletLowBalance";                             // NOTF-05 (agency-admin internal alert)
}
