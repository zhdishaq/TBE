using Xunit;

namespace Payments.Tests;

/// <summary>
/// Red placeholders for Plan 05-03 Task 2 (wallet top-up caps per D-40).
/// Min/max are configured via Wallet__TopUp__MinAmount and
/// Wallet__TopUp__MaxAmount env vars (defaults £10 / £50,000); the controller
/// MUST enforce them BEFORE creating a Stripe PaymentIntent so no idempotency
/// key is consumed on a 400 response (T-05-03-01).
/// </summary>
public class WalletTopUpCapsTests
{
    /// <summary>D-40 — below minimum returns 400 problem+json, never touches Stripe.</summary>
    [Fact]
    [Trait("Category", "RedPlaceholder")]
    public void TopUp_returns_400_problem_json_when_amount_below_MinTopUpAmountCents()
    {
        Assert.Fail("MISSING — Plan 05-03 Task 2 (D-40 min cap; 400 problem+json before Stripe).");
    }

    /// <summary>D-40 — above maximum returns 400 problem+json, never touches Stripe.</summary>
    [Fact]
    [Trait("Category", "RedPlaceholder")]
    public void TopUp_returns_400_problem_json_when_amount_above_MaxTopUpAmountCents()
    {
        Assert.Fail("MISSING — Plan 05-03 Task 2 (D-40 max cap; 400 problem+json before Stripe).");
    }

    /// <summary>T-05-03-01 — the problem+json body includes min, max, and attempted fields so the portal can render a helpful message.</summary>
    [Fact]
    [Trait("Category", "RedPlaceholder")]
    public void TopUp_problem_json_contains_min_max_attempted_fields()
    {
        Assert.Fail("MISSING — Plan 05-03 Task 2 (T-05-03-01 problem+json shape).");
    }
}
