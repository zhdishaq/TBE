using Xunit;

namespace TBE.BookingService.Tests;

/// <summary>
/// Red placeholders for Plan 05-01 Task 3 (B2B bookings listing) and Plan
/// 05-04 (per-booking markup override + invoice PDF). Every fact asserts
/// Fail with a "MISSING — Plan XX-YY Task Z" message so the downstream
/// planner knows which task will turn each test green.
///
/// Trait Category=RedPlaceholder so the CI quick command filter keeps
/// the baseline green (mitigation T-05-00-04 in 05-00-PLAN threat register).
/// </summary>
public class AgentBookingsControllerTests
{
    /// <summary>
    /// D-34 OVERRIDE — agency-wide visibility for every agent role
    /// (agent, agent-admin, agent-readonly). Filter by agency_id ONLY,
    /// never additionally by sub. Planner MUST cite D-34 in a comment at
    /// the controller boundary.
    /// </summary>
    [Fact]
    [Trait("Category", "RedPlaceholder")]
    public void ListForMe_returns_bookings_filtered_by_agency_id_claim_only_not_sub()
    {
        Assert.Fail("MISSING — Plan 05-01 Task 3 (D-34 agency-wide booking visibility).");
    }

    /// <summary>Pitfall 26 — missing agency_id claim must hard-fail 401, never return other agencies' data.</summary>
    [Fact]
    [Trait("Category", "RedPlaceholder")]
    public void ListForMe_returns_401_when_agency_id_claim_missing()
    {
        Assert.Fail("MISSING — Plan 05-01 Task 3 (Pitfall 26 agency_id claim gate).");
    }

    /// <summary>IDOR guard — cross-tenant read on /bookings/{id} must 403 even when the booking id is valid.</summary>
    [Fact]
    [Trait("Category", "RedPlaceholder")]
    public void GetById_returns_403_when_booking_belongs_to_different_agency()
    {
        Assert.Fail("MISSING — Plan 05-01 Task 3 (cross-tenant IDOR guard per D-34).");
    }

    /// <summary>D-37 — per-booking markup override is agent-admin only; other agent roles get 403.</summary>
    [Fact]
    [Trait("Category", "RedPlaceholder")]
    public void Post_accepts_override_only_from_agent_admin_role()
    {
        Assert.Fail("MISSING — Plan 05-04 Task 2 (D-37 per-booking markup override; agent-admin only).");
    }
}
