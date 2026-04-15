using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;
using TBE.NotificationService.Application.Email;

namespace TBE.NotificationService.Infrastructure.Email;

/// <summary>
/// SendGrid-backed <see cref="IEmailDelivery"/>. NOTF-01 delivery backbone.
/// <para>
/// Security (T-03-17): API key is loaded from options bound to the <c>SendGrid</c>
/// configuration section (sourced from env per 03-03). Constructor throws if the
/// key is empty. The raw HTML body and recipient address are NEVER written to logs.
/// </para>
/// </summary>
public sealed class SendGridEmailDelivery : IEmailDelivery
{
    private static readonly Regex HtmlTagRegex = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new("\\s+", RegexOptions.Compiled);

    private readonly ISendGridClient _client;
    private readonly SendGridOptions _opts;
    private readonly ILogger<SendGridEmailDelivery> _log;

    public SendGridEmailDelivery(
        ISendGridClient client,
        IOptions<SendGridOptions> opts,
        ILogger<SendGridEmailDelivery> log)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _opts = opts?.Value ?? throw new ArgumentNullException(nameof(opts));
        _log = log ?? throw new ArgumentNullException(nameof(log));

        if (string.IsNullOrWhiteSpace(_opts.ApiKey))
        {
            // T-03-17 mitigation: refuse to start if SendGrid API key is missing —
            // avoids silently degrading to no-email and protects against misconfiguration.
            throw new InvalidOperationException(
                "SendGrid:ApiKey is missing — set SENDGRID__APIKEY in environment (.env) before starting NotificationService.");
        }
    }

    public async Task<EmailDeliveryResult> SendAsync(EmailEnvelope envelope, CancellationToken ct)
    {
        if (envelope is null) throw new ArgumentNullException(nameof(envelope));

        try
        {
            var msg = new SendGridMessage
            {
                From = new EmailAddress(_opts.FromEmail, _opts.FromName),
                Subject = envelope.Subject,
                HtmlContent = envelope.HtmlBody,
                PlainTextContent = string.IsNullOrWhiteSpace(envelope.PlainTextBody)
                    ? StripHtml(envelope.HtmlBody)
                    : envelope.PlainTextBody
            };
            msg.AddTo(envelope.ToEmail, envelope.ToName);

            if (envelope.Attachments is not null)
            {
                foreach (var att in envelope.Attachments)
                {
                    msg.AddAttachment(
                        filename: att.FileName,
                        base64Content: Convert.ToBase64String(att.Content),
                        type: att.ContentType,
                        disposition: "attachment",
                        content_id: att.ContentId);
                }
            }

            if (string.Equals(_opts.SandboxMode, "true", StringComparison.OrdinalIgnoreCase))
            {
                msg.MailSettings = new MailSettings
                {
                    SandboxMode = new SandboxMode { Enable = true }
                };
            }

            var resp = await _client.SendEmailAsync(msg, ct).ConfigureAwait(false);
            var success = (int)resp.StatusCode is >= 200 and < 300;

            string? providerMessageId = null;
            if (resp.Headers is not null && resp.Headers.TryGetValues("X-Message-Id", out var values))
            {
                providerMessageId = values.FirstOrDefault();
            }

            if (!success)
            {
                // Error body may contain provider-side diagnostic; safe to include (no PII).
                string? errBody = null;
                if (resp.Body is not null)
                {
                    errBody = await resp.Body.ReadAsStringAsync(ct).ConfigureAwait(false);
                }

                // Log ONLY the status and the hashed recipient prefix — never the raw recipient or body.
                _log.LogWarning(
                    "SendGrid delivery failed: status={Status} recipientHashPrefix={Hash}",
                    (int)resp.StatusCode,
                    HashRecipientPrefix(envelope.ToEmail));

                return new EmailDeliveryResult(false, providerMessageId, errBody ?? resp.StatusCode.ToString());
            }

            return new EmailDeliveryResult(true, providerMessageId, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.LogError(ex,
                "SendGrid delivery threw: recipientHashPrefix={Hash}",
                HashRecipientPrefix(envelope.ToEmail));
            return new EmailDeliveryResult(false, null, ex.GetType().Name);
        }
    }

    private static string StripHtml(string html)
    {
        var stripped = HtmlTagRegex.Replace(html, " ");
        return WhitespaceRegex.Replace(stripped, " ").Trim();
    }

    private static string HashRecipientPrefix(string email)
    {
        // T-03-18 mitigation: log a short, non-reversible prefix instead of the raw address.
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(email));
        return Convert.ToHexString(bytes)[..8];
    }
}
