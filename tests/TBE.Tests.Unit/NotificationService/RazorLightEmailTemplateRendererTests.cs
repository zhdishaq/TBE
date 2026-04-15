using FluentAssertions;
using TBE.NotificationService.API.Templates.Models;
using TBE.NotificationService.Infrastructure.Email;
using Xunit;

namespace TBE.Tests.Unit.NotificationService;

[Trait("Category", "Unit")]
public sealed class RazorLightEmailTemplateRendererTests
{
    private static string TemplatesRoot =>
        // Templates are copied to output via API.csproj CopyToOutputDirectory.
        Path.Combine(AppContext.BaseDirectory, "Templates");

    [Fact]
    public async Task NOTF06_FlightConfirmation_renders_passenger_pnr_and_eticket()
    {
        var renderer = new RazorLightEmailTemplateRenderer(TemplatesRoot);
        var model = new FlightConfirmationModel(
            PassengerName: "Alice",
            Pnr: "ABC123",
            ETicketNumber: "014-ETKT",
            Total: 450.00m,
            Currency: "GBP");

        var rendered = await renderer.RenderAsync("FlightConfirmation", model, CancellationToken.None);

        rendered.HtmlBody.Should().Contain("Hello Alice");
        rendered.HtmlBody.Should().Contain("ABC123");
        rendered.HtmlBody.Should().Contain("014-ETKT");
    }

    [Fact]
    public async Task NOTF06_subject_extracted_from_html_comment()
    {
        var renderer = new RazorLightEmailTemplateRenderer(TemplatesRoot);
        var model = new FlightConfirmationModel("Alice", "ABC123", "014-ETKT", 10m, "GBP");

        var rendered = await renderer.RenderAsync("FlightConfirmation", model, CancellationToken.None);

        rendered.Subject.Should().Contain("Booking Confirmed");
        rendered.Subject.Should().Contain("ABC123");
        rendered.HtmlBody.Should().NotContain("<!--SUBJECT:");
    }

    [Fact]
    public async Task NOTF06_header_and_footer_partials_included()
    {
        var renderer = new RazorLightEmailTemplateRenderer(TemplatesRoot);
        var model = new FlightConfirmationModel("Alice", "ABC123", "014-ETKT", 10m, "GBP");

        var rendered = await renderer.RenderAsync("FlightConfirmation", model, CancellationToken.None);

        rendered.HtmlBody.Should().Contain("TBE — Travel Booking Engine"); // header brand bar
        rendered.HtmlBody.Should().Contain("Unsubscribe");                  // footer unsubscribe link
    }

    [Fact]
    public async Task NOTF06_plain_text_body_strips_html_tags()
    {
        var renderer = new RazorLightEmailTemplateRenderer(TemplatesRoot);
        var model = new FlightConfirmationModel("Alice", "ABC123", "014-ETKT", 10m, "GBP");

        var rendered = await renderer.RenderAsync("FlightConfirmation", model, CancellationToken.None);

        rendered.PlainTextBody.Should().NotContain("<");
        rendered.PlainTextBody.Should().NotContain(">");
        rendered.PlainTextBody.Should().Contain("Alice");
    }
}
