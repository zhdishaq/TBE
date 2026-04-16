using Xunit;

namespace Notifications.Tests;

/// <summary>
/// RED placeholders authored in Wave 0 (Plan 04-00 Task 3). Plan 04-03
/// implements HotelBookingConfirmedConsumer (reuses RazorLight +
/// QuestPDF + SendGrid pipeline per CONTEXT D-16 / NOTF-02) and turns
/// these green.
/// </summary>
public class HotelBookingConfirmedConsumerTests
{
    [Fact]
    [Trait("Category", "RedPlaceholder")]
    public void Consume_inserts_EmailIdempotencyLog_with_HotelVoucher_type()
    {
        Assert.Fail("Red placeholder — implemented by Plan 04-03");
    }

    [Fact]
    [Trait("Category", "RedPlaceholder")]
    public void Consume_duplicate_event_is_swallowed()
    {
        Assert.Fail("Red placeholder — implemented by Plan 04-03");
    }

    [Fact]
    [Trait("Category", "RedPlaceholder")]
    public void Consume_sends_email_with_voucher_attachment()
    {
        Assert.Fail("Red placeholder — implemented by Plan 04-03");
    }
}
