using System.Text.RegularExpressions;
using RazorLight;
using TBE.NotificationService.Application.Email;

namespace TBE.NotificationService.Infrastructure.Email;

/// <summary>
/// RazorLight-backed implementation of <see cref="IEmailTemplateRenderer"/>.
/// Loads cshtml templates from <c>{AppContext.BaseDirectory}/Templates</c> (copied as
/// content via NotificationService.API.csproj) and supports the shared <c>_Header</c>
/// / <c>_Footer</c> partials via <c>IncludeAsync</c>.
/// Subject line is conveyed via a single-line HTML comment on line 1 of each template:
/// <c><!--SUBJECT:Booking Confirmed — ABC123--></c>. The comment is parsed off the
/// final HTML body before delivery. NOTF-06.
/// </summary>
public sealed class RazorLightEmailTemplateRenderer : IEmailTemplateRenderer
{
    private static readonly Regex SubjectRegex = new(
        "<!--SUBJECT:(.*?)-->",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex HtmlTagRegex = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new("\\s+", RegexOptions.Compiled);

    private static readonly Lazy<RazorLightEngine> Engine = new(() =>
        new RazorLightEngineBuilder()
            .UseFileSystemProject(Path.Combine(AppContext.BaseDirectory, "Templates"))
            .UseMemoryCachingProvider()
            .Build());

    private readonly RazorLightEngine _engine;

    public RazorLightEmailTemplateRenderer()
    {
        _engine = Engine.Value;
    }

    // Visible for tests — allows pointing at an arbitrary Templates root.
    public RazorLightEmailTemplateRenderer(string templatesRoot)
    {
        _engine = new RazorLightEngineBuilder()
            .UseFileSystemProject(templatesRoot)
            .UseMemoryCachingProvider()
            .Build();
    }

    public async Task<RenderedEmail> RenderAsync<TModel>(string templateKey, TModel model, CancellationToken ct)
    {
        var rawHtml = await _engine.CompileRenderAsync($"{templateKey}.cshtml", model!);

        var subject = ExtractSubject(rawHtml);
        var cleanedHtml = SubjectRegex.Replace(rawHtml, string.Empty);
        var plainText = HtmlToPlainText(cleanedHtml);

        return new RenderedEmail(subject, cleanedHtml, plainText);
    }

    private static string ExtractSubject(string html)
    {
        var match = SubjectRegex.Match(html);
        return match.Success ? match.Groups[1].Value.Trim() : "TBE notification";
    }

    private static string HtmlToPlainText(string html)
    {
        var text = HtmlTagRegex.Replace(html, " ");
        text = WhitespaceRegex.Replace(text, " ").Trim();
        return text;
    }
}
