using Xunit;

namespace Notifications.Tests;

/// <summary>
/// RED placeholders authored in Wave 0 (Plan 04-00 Task 3). Plan 04-03
/// implements HotelVoucherDocument (QuestPDF IDocument, Community
/// licensed) and turns these green.
/// </summary>
public class HotelVoucherDocumentTests
{
    [Fact]
    [Trait("Category", "RedPlaceholder")]
    public void Generate_produces_nonempty_PDF()
    {
        Assert.Fail("Red placeholder — implemented by Plan 04-03");
    }

    [Fact]
    [Trait("Category", "RedPlaceholder")]
    public void Generate_uses_Community_license()
    {
        Assert.Fail("Red placeholder — implemented by Plan 04-03");
    }
}
