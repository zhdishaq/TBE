namespace TBE.NotificationService.Application.Email;

public sealed record EmailEnvelope(
    string ToEmail,
    string ToName,
    string Subject,
    string HtmlBody,
    string? PlainTextBody,
    IReadOnlyList<EmailAttachment> Attachments);

public sealed record EmailAttachment(
    string FileName,
    string ContentType,
    byte[] Content,
    string? ContentId = null);
