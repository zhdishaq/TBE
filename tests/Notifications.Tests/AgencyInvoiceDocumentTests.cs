using Xunit;

namespace Notifications.Tests;

/// <summary>
/// Red placeholders for Plan 05-04 (AgencyInvoiceDocument). Enforces D-43
/// which mandates that agency invoices render GROSS only — NET, markup and
/// commission numbers MUST NEVER appear in the PDF. Substring-level negative
/// assertions (via PdfPig text extraction) guarantee this at ship-time.
/// </summary>
public class AgencyInvoiceDocumentTests
{
    /// <summary>D-43 — invoice renders gross (base + surcharges + taxes) and total only.</summary>
    [Fact]
    [Trait("Category", "RedPlaceholder")]
    public void Invoice_renders_gross_base_surcharges_taxes_and_total_only()
    {
        Assert.Fail("MISSING — Plan 05-04 Task 1 (D-43 GROSS-only invoice rendering).");
    }

    /// <summary>D-43 — negative assertion: extracted PDF text must not contain NET, markup, or commission strings.</summary>
    [Fact]
    [Trait("Category", "RedPlaceholder")]
    public void Invoice_never_renders_NET_markup_or_commission_strings()
    {
        Assert.Fail("MISSING — Plan 05-04 Task 1 (D-43 PdfPig decompress + substring negative assertion).");
    }

    /// <summary>D-43 — VAT line is rendered only when the invoice model's Vat value is not null.</summary>
    [Fact]
    [Trait("Category", "RedPlaceholder")]
    public void Invoice_renders_VAT_line_when_model_Vat_is_not_null()
    {
        Assert.Fail("MISSING — Plan 05-04 Task 1 (D-43 conditional VAT rendering).");
    }
}
