namespace TBE.NotificationService.Infrastructure.Persistence;

/// <summary>
/// Append-only audit row keyed uniquely by <c>(EventId, EmailType)</c>.
/// NOTF-06: proves exactly-one email per (event, email-type) combination by relying on a
/// SQL unique-index violation (error 2601 / 2627) when a duplicate insert is attempted.
/// Insert happens BEFORE the SendGrid call — duplicate detection is authoritative.
/// </summary>
public sealed class EmailIdempotencyLog
{
    public long Id { get; set; }
    public Guid EventId { get; set; }
    public string EmailType { get; set; } = "";
    public Guid? BookingId { get; set; }
    public string Recipient { get; set; } = "";
    public string? ProviderMessageId { get; set; }
    public DateTime SentAtUtc { get; set; }
}
