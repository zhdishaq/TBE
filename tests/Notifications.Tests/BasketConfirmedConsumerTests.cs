using Xunit;

namespace Notifications.Tests;

/// <summary>
/// RED placeholders authored in Wave 0 (Plan 04-00 Task 3). Plan 04-04
/// implements BasketConfirmedConsumer (single combined email for the
/// basket per CONTEXT D-09 — partial-failure path sends a combined
/// "flight booked, hotel unavailable" email) and turns these green.
/// </summary>
public class BasketConfirmedConsumerTests
{
    [Fact]
    [Trait("Category", "RedPlaceholder")]
    public void Consume_sends_combined_email()
    {
        Assert.Fail("Red placeholder — implemented by Plan 04-04");
    }

    [Fact]
    [Trait("Category", "RedPlaceholder")]
    public void Consume_partial_failure_sends_partial_success_email()
    {
        Assert.Fail("Red placeholder — implemented by Plan 04-04");
    }
}
