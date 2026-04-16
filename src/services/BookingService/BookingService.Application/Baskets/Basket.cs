namespace TBE.BookingService.Application.Baskets;

/// <summary>
/// Trip Builder basket aggregate — Plan 04-04, PKG-01..04. Represents ONE combined
/// checkout binding a flight component and/or a hotel component (and forward-compat car)
/// under a SINGLE Stripe PaymentIntent per CONTEXT D-08. The <see cref="StripePaymentIntentId"/>
/// field is intentionally SINGULAR — any split into FlightPaymentIntentId / HotelPaymentIntentId
/// is explicitly forbidden by D-08 ("One charge on the customer's statement"). Capture runs
/// sequentially across two stages bound to saga outcomes (D-10):
/// <list type="number">
///   <item>Flight ticketed → <c>CapturePartial(AmountToCapture = FlightSubtotal, FinalCapture = false)</c></item>
///   <item>Hotel confirmed → <c>CapturePartial(AmountToCapture = HotelSubtotal, FinalCapture = true)</c></item>
/// </list>
/// Partial failure (D-09): flight captured, hotel failed → orchestrator calls
/// <c>CapturePartial(AmountToCapture = 0, FinalCapture = true)</c> to release the uncaptured
/// remainder; <see cref="RefundedAmount"/> records the released (not refunded) hotel portion
/// because it was never captured.
/// <para>
/// Version is mapped <c>IsRowVersion()</c> per the 04-PATTERNS §BasketMap concurrency rule.
/// </para>
/// </summary>
public class Basket
{
    public Guid BasketId { get; set; }
    public string UserId { get; set; } = string.Empty;

    // Line-item correlation. Null until the sub-booking is created.
    public Guid? FlightBookingId { get; set; }
    public Guid? HotelBookingId { get; set; }
    public Guid? CarBookingId { get; set; }

    /// <summary>
    /// The SINGLE combined Stripe PaymentIntent for this basket (D-08). Null until the
    /// customer initializes payment via POST /baskets/{id}/payment-intents.
    /// </summary>
    public string? StripePaymentIntentId { get; set; }

    /// <summary>One of: Initiated | PaymentAuthorized | Confirmed | PartiallyConfirmed | Failed.</summary>
    public string Status { get; set; } = "Initiated";

    // Pricing (D-08 single-PI — TotalAmount = FlightSubtotal + HotelSubtotal + CarSubtotal).
    public decimal TotalAmount { get; set; }
    public decimal FlightSubtotal { get; set; }
    public decimal HotelSubtotal { get; set; }

    /// <summary>Running total of captured amounts across partial captures (D-10).</summary>
    public decimal ChargedAmount { get; set; }

    /// <summary>
    /// For D-09 partial-failure reporting: the released-remainder portion of the single PI.
    /// Never a real Stripe refund — the amount was held but not captured.
    /// </summary>
    public decimal RefundedAmount { get; set; }

    /// <summary>True once flight stage capture succeeded (FinalCapture=false).</summary>
    public bool FlightCaptured { get; set; }

    /// <summary>True once the PI was closed via the final capture (success or release-remainder path).</summary>
    public bool HotelCaptured { get; set; }

    public string Currency { get; set; } = "GBP";

    public string GuestEmail { get; set; } = string.Empty;
    public string GuestFullName { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    /// <summary>EF Core row-version concurrency token (04-PATTERNS §BasketMap — IsRowVersion()).</summary>
    public byte[] Version { get; set; } = Array.Empty<byte>();
}
