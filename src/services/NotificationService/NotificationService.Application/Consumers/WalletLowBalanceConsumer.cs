using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TBE.Contracts.Events;
using TBE.NotificationService.API.Templates.Models;
using TBE.NotificationService.Application.Contacts;
using TBE.NotificationService.Application.Email;
using TBE.NotificationService.Application.Persistence;

namespace TBE.NotificationService.Application.Consumers;

/// <summary>
/// NOTF-05 — sends an internal advisory email to the agency admin when the wallet balance
/// drops below threshold. Looks up the admin contact via <see cref="IAgencyAdminContactClient"/>
/// (BookingService <c>GET /api/agencies/{agencyId}/admin-contact</c>).
/// <para>
/// NOTE: The <see cref="WalletLowBalance"/> event carries only <c>WalletId</c>, not
/// <c>AgencyId</c>. Resolving agency-id from wallet-id is BookingService's responsibility —
/// here we forward the wallet-id and expect the admin-contact endpoint to accept either
/// identifier, OR a follow-up plan will enrich the event with AgencyId directly.
/// This consumer degrades safely to a log-and-skip when contact resolution fails.
/// </para>
/// </summary>
public sealed class WalletLowBalanceConsumer : IConsumer<WalletLowBalance>
{
    private readonly NotificationDbContext _db;
    private readonly IAgencyAdminContactClient _agencyContacts;
    private readonly IEmailTemplateRenderer _renderer;
    private readonly IEmailDelivery _delivery;
    private readonly ILogger<WalletLowBalanceConsumer> _log;

    public WalletLowBalanceConsumer(
        NotificationDbContext db,
        IAgencyAdminContactClient agencyContacts,
        IEmailTemplateRenderer renderer,
        IEmailDelivery delivery,
        ILogger<WalletLowBalanceConsumer> log)
    {
        _db = db; _agencyContacts = agencyContacts; _renderer = renderer; _delivery = delivery; _log = log;
    }

    public async Task Consume(ConsumeContext<WalletLowBalance> ctx)
    {
        var eventId = ctx.MessageId ?? Guid.NewGuid();
        var evt = ctx.Message;

        // WalletId serves as the agency-lookup key — BookingService's
        // /api/agencies/{id}/admin-contact endpoint is expected to resolve either id shape.
        var contact = await _agencyContacts.GetAdminContactAsync(evt.WalletId, ctx.CancellationToken).ConfigureAwait(false);
        if (contact is null)
        {
            _log.LogWarning(
                "NOTF-05: cannot resolve agency admin contact for wallet {WalletId} — skipping low-balance alert",
                evt.WalletId);
            return;
        }

        var idemp = new EmailIdempotencyLog
        {
            EventId = eventId,
            EmailType = EmailType.WalletLowBalance,
            BookingId = null,
            Recipient = contact.Email,
            SentAtUtc = DateTime.UtcNow,
        };
        _db.EmailIdempotencyLogs.Add(idemp);
        try { await _db.SaveChangesAsync(ctx.CancellationToken).ConfigureAwait(false); }
        catch (DbUpdateException ex) when (IdempotencyHelpers.IsUniqueViolation(ex))
        {
            _log.LogInformation("NOTF-06: duplicate WalletLowBalance for {EventId} skipped", eventId);
            return;
        }

        var rendered = await _renderer.RenderAsync(
            EmailType.WalletLowBalance,
            new WalletLowBalanceModel(
                AgencyName: contact.AgencyName,
                CurrentBalance: evt.Balance,
                Threshold: evt.Threshold,
                Currency: "GBP"),
            ctx.CancellationToken).ConfigureAwait(false);

        var envelope = new EmailEnvelope(
            contact.Email, contact.Name, rendered.Subject,
            rendered.HtmlBody, rendered.PlainTextBody,
            Array.Empty<EmailAttachment>());

        var result = await _delivery.SendAsync(envelope, ctx.CancellationToken).ConfigureAwait(false);
        if (!result.Success)
            throw new InvalidOperationException($"SendGrid failed for WalletLowBalance {evt.WalletId}: {result.ErrorReason}");

        idemp.ProviderMessageId = result.ProviderMessageId;
        await _db.SaveChangesAsync(ctx.CancellationToken).ConfigureAwait(false);
    }
}
