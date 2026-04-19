using Xunit;

namespace TBE.CrmService.Tests;

/// <summary>
/// Plan 06-04 / CRM-04 / D-62 — free-form ops communication log must
/// persist markdown body, record the <c>preferred_username</c> actor
/// (Pitfall 28 fail-closed when claim missing), publish the
/// <c>CustomerCommunicationLogged</c> event via the EF outbox in the
/// same transaction as the write, and enforce a 10 000-char body cap.
///
/// <para>
/// Status: RED placeholder. Requires a live MSSQL Testcontainer +
/// MassTransitHarness to observe the outbox publish. Tagged
/// <c>Category=RedPlaceholder</c>.
/// </para>
/// </summary>
public sealed class CommunicationLogTests
{
    [Fact]
    [Trait("Category", "RedPlaceholder")]
    [Trait("Category", "Phase06")]
    public void Post_persists_log_row_with_actor_from_preferred_username_claim()
    {
        Assert.Fail("MISSING — requires WebApplicationFactory + MSSQL Testcontainer (CRM-04).");
    }

    [Fact]
    [Trait("Category", "RedPlaceholder")]
    [Trait("Category", "Phase06")]
    public void Post_without_preferred_username_claim_returns_401_Pitfall_28()
    {
        Assert.Fail("MISSING — requires WebApplicationFactory + JWT test harness (Pitfall 28).");
    }

    [Fact]
    [Trait("Category", "RedPlaceholder")]
    [Trait("Category", "Phase06")]
    public void Post_publishes_CustomerCommunicationLogged_via_EF_outbox_in_same_tx()
    {
        Assert.Fail("MISSING — requires MSSQL Testcontainer + MassTransitHarness + EF outbox (D-62).");
    }

    [Fact]
    [Trait("Category", "RedPlaceholder")]
    [Trait("Category", "Phase06")]
    public void Post_with_body_over_10000_chars_returns_400()
    {
        Assert.Fail("MISSING — requires WebApplicationFactory + MSSQL Testcontainer (body cap).");
    }

    [Fact]
    [Trait("Category", "RedPlaceholder")]
    [Trait("Category", "Phase06")]
    public void Post_with_invalid_entity_type_returns_400()
    {
        Assert.Fail("MISSING — requires WebApplicationFactory + MSSQL Testcontainer (CRM-04).");
    }

    [Fact]
    [Trait("Category", "RedPlaceholder")]
    [Trait("Category", "Phase06")]
    public void Get_filters_log_rows_by_entity_type_and_id_with_pagination()
    {
        Assert.Fail("MISSING — requires WebApplicationFactory + MSSQL Testcontainer (CRM-04).");
    }
}
