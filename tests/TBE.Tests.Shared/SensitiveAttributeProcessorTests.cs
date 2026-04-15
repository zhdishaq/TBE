using System.Diagnostics;
using FluentAssertions;
using TBE.Common.Telemetry;
using Xunit;

namespace TBE.Tests.Shared;

[Trait("Category", "Unit")]
public class SensitiveAttributeProcessorTests
{
    private static readonly ActivitySource TestSource = new("TBE.Tests.SensitiveAttributeProcessor");

    static SensitiveAttributeProcessorTests()
    {
        // Ensure ActivitySource is always sampled for the tests.
        ActivitySource.AddActivityListener(new ActivityListener
        {
            ShouldListenTo = s => s.Name == TestSource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData,
        });
    }

    private static Activity CreateActivity()
    {
        var act = TestSource.StartActivity("test")
            ?? throw new InvalidOperationException("Could not start Activity for test");
        return act;
    }

    [Fact(DisplayName = "COMP06: redacts card.* prefix keys")]
    public void COMP06_redacts_card_dot_prefix_keys()
    {
        using var act = CreateActivity();
        act.SetTag("card.number", "4242424242424242");
        act.SetTag("card.expiry", "12/29");
        act.SetTag("card.issuer", "visa"); // also prefixed — redacted by regex

        new SensitiveAttributeProcessor().OnEnd(act);

        act.GetTagItem("card.number").Should().Be(SensitiveAttributeProcessor.Redacted);
        act.GetTagItem("card.expiry").Should().Be(SensitiveAttributeProcessor.Redacted);
        act.GetTagItem("card.issuer").Should().Be(SensitiveAttributeProcessor.Redacted);
    }

    [Fact(DisplayName = "COMP06: redacts stripe.raw_* keys")]
    public void COMP06_redacts_stripe_raw_star()
    {
        using var act = CreateActivity();
        act.SetTag("stripe.raw_body", "{\"foo\":\"bar\"}");
        act.SetTag("stripe.raw_payment_method", "pm_raw");

        new SensitiveAttributeProcessor().OnEnd(act);

        act.GetTagItem("stripe.raw_body").Should().Be(SensitiveAttributeProcessor.Redacted);
        act.GetTagItem("stripe.raw_payment_method").Should().Be(SensitiveAttributeProcessor.Redacted);
    }

    [Fact(DisplayName = "COMP06: passes benign keys unchanged")]
    public void COMP06_passes_benign_keys()
    {
        using var act = CreateActivity();
        act.SetTag("http.method", "POST");
        act.SetTag("booking.id", "abc-123");
        act.SetTag("stripe.payment_intent_id", "pi_1"); // NOT raw_*, allowed

        new SensitiveAttributeProcessor().OnEnd(act);

        act.GetTagItem("http.method").Should().Be("POST");
        act.GetTagItem("booking.id").Should().Be("abc-123");
        act.GetTagItem("stripe.payment_intent_id").Should().Be("pi_1");
    }

    [Fact(DisplayName = "COMP06: redacts cvv and pan exact-match keys")]
    public void COMP06_redacts_cvv_and_pan_exact_match()
    {
        using var act = CreateActivity();
        act.SetTag("cvv", "123");
        act.SetTag("pan", "4242424242424242");
        act.SetTag("passport.number", "X123");
        act.SetTag("document.number", "D-987");

        new SensitiveAttributeProcessor().OnEnd(act);

        act.GetTagItem("cvv").Should().Be(SensitiveAttributeProcessor.Redacted);
        act.GetTagItem("pan").Should().Be(SensitiveAttributeProcessor.Redacted);
        act.GetTagItem("passport.number").Should().Be(SensitiveAttributeProcessor.Redacted);
        act.GetTagItem("document.number").Should().Be(SensitiveAttributeProcessor.Redacted);
    }
}
