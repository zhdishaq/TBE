using Xunit;

namespace Notifications.Tests;

/// <summary>
/// Red placeholders for Plan 05-04 Task 2 (AgencyInvoiceController). Enforces
/// Pitfall 26 (server-side agency_id claim filter — never trust the request
/// body) and Pitfall 28 (fail-closed when the agency_id claim is missing).
/// </summary>
public class AgencyInvoiceControllerTests
{
    /// <summary>Pitfall 26 — when the booking's AgencyId differs from the caller's agency_id claim, return 403 (cross-tenant access).</summary>
    [Fact]
    [Trait("Category", "RedPlaceholder")]
    public void GetInvoice_returns_403_when_booking_AgencyId_differs_from_claim()
    {
        Assert.Fail("MISSING — Plan 05-04 Task 2 (Pitfall 26 cross-agency 403).");
    }

    /// <summary>Pitfall 28 — when the agency_id claim is missing from the token, return 401 (fail-closed, no fallback).</summary>
    [Fact]
    [Trait("Category", "RedPlaceholder")]
    public void GetInvoice_returns_401_when_agency_id_claim_missing()
    {
        Assert.Fail("MISSING — Plan 05-04 Task 2 (Pitfall 28 missing-claim 401 fail-closed).");
    }

    /// <summary>Shape check — successful invoice response must set content-type application/pdf so browsers render inline.</summary>
    [Fact]
    [Trait("Category", "RedPlaceholder")]
    public void GetInvoice_returns_stream_content_type_application_pdf()
    {
        Assert.Fail("MISSING — Plan 05-04 Task 2 (application/pdf content-type shape).");
    }
}
