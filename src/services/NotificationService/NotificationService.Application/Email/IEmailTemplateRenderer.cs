namespace TBE.NotificationService.Application.Email;

/// <summary>
/// Renders a Razor cshtml template keyed by <paramref name="templateKey"/> into a
/// <see cref="RenderedEmail"/> with subject + HTML body + plain-text body.
/// NOTF-06: all outbound transactional emails are rendered through this interface —
/// never plain-text concatenation.
/// </summary>
public interface IEmailTemplateRenderer
{
    Task<RenderedEmail> RenderAsync<TModel>(string templateKey, TModel model, CancellationToken ct);
}

public sealed record RenderedEmail(string Subject, string HtmlBody, string PlainTextBody);
