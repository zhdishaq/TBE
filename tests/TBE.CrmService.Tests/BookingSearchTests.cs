using Xunit;

namespace TBE.CrmService.Tests;

/// <summary>
/// Plan 06-04 / CRM-03 / T-6-58 — unified backoffice search must fan out
/// to Customer / Agency / Booking projections with <see cref="Task.WhenAll{TResult}"/>,
/// cap each source at 20 rows, and cap the merged result at 50. Exposed
/// on both <c>/api/crm/search</c> and <c>/api/backoffice/search</c> so
/// the YARP gateway can route through either cluster.
///
/// <para>
/// Status: RED placeholder. Requires a live MSSQL Testcontainer +
/// WebApplicationFactory hooked to CrmService.API so the controller
/// executes against real SQL LIKE queries (EF InMemory does not model
/// LIKE semantics correctly). Tagged <c>Category=RedPlaceholder</c>.
/// </para>
/// </summary>
public sealed class BookingSearchTests
{
    [Fact]
    [Trait("Category", "RedPlaceholder")]
    [Trait("Category", "Phase06")]
    public void Search_term_shorter_than_2_chars_returns_400_problem_json()
    {
        Assert.Fail("MISSING — requires WebApplicationFactory + MSSQL Testcontainer (CRM-03).");
    }

    [Fact]
    [Trait("Category", "RedPlaceholder")]
    [Trait("Category", "Phase06")]
    public void Search_fans_out_to_BookingProjection_CustomerProjection_AgencyProjection()
    {
        Assert.Fail("MISSING — requires WebApplicationFactory + MSSQL Testcontainer (CRM-03).");
    }

    [Fact]
    [Trait("Category", "RedPlaceholder")]
    [Trait("Category", "Phase06")]
    public void Search_caps_each_source_at_20_and_merged_result_at_50_T_6_58()
    {
        Assert.Fail("MISSING — requires WebApplicationFactory + MSSQL Testcontainer (T-6-58 DoS cap).");
    }

    [Fact]
    [Trait("Category", "RedPlaceholder")]
    [Trait("Category", "Phase06")]
    public void Search_prefers_prefix_matches_in_ranking()
    {
        Assert.Fail("MISSING — requires WebApplicationFactory + MSSQL Testcontainer (CRM-03).");
    }

    [Fact]
    [Trait("Category", "RedPlaceholder")]
    [Trait("Category", "Phase06")]
    public void Search_excludes_erased_customers_from_results()
    {
        Assert.Fail("MISSING — requires WebApplicationFactory + MSSQL Testcontainer (D-57 erasure filter).");
    }

    [Fact]
    [Trait("Category", "RedPlaceholder")]
    [Trait("Category", "Phase06")]
    public void Search_is_reachable_via_both_crm_and_backoffice_routes()
    {
        Assert.Fail("MISSING — requires WebApplicationFactory + MSSQL Testcontainer (dual-route CRM-03).");
    }
}
