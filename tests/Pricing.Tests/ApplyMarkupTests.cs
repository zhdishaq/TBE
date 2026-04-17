using Xunit;

namespace Pricing.Tests;

/// <summary>
/// Red placeholders for Plan 05-02 (ApplyMarkup) covering D-36 two-row
/// markup resolver (base + optional RouteClass override) and D-41
/// commission-equals-markup v1 semantics.
/// </summary>
public class ApplyMarkupTests
{
    /// <summary>D-36 resolver — when a RouteClass-specific row exists for the agency, it wins.</summary>
    [Fact]
    [Trait("Category", "RedPlaceholder")]
    public void ApplyMarkup_uses_RouteClass_specific_row_when_present()
    {
        Assert.Fail("MISSING — Plan 05-02 Task 4 (D-36 RouteClass override resolver).");
    }

    /// <summary>D-36 fallback — when no RouteClass-specific row matches, fall back to the agency's base row.</summary>
    [Fact]
    [Trait("Category", "RedPlaceholder")]
    public void ApplyMarkup_falls_back_to_base_row_when_RouteClass_not_matched()
    {
        Assert.Fail("MISSING — Plan 05-02 Task 4 (D-36 base-row fallback).");
    }

    /// <summary>D-41 v1 — commission equals markup at this stage; PricingService returns net / markup / gross / commission.</summary>
    [Fact]
    [Trait("Category", "RedPlaceholder")]
    public void ApplyMarkup_returns_net_markup_gross_commission_and_commission_equals_markup_v1()
    {
        Assert.Fail("MISSING — Plan 05-02 Task 4 (D-41 commission==markup; v1 PricingService result shape).");
    }
}
