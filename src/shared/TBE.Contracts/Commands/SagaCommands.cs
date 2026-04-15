namespace TBE.Contracts.Commands;

/// <summary>
/// Command to PricingService: re-confirm the live fare for the stored offer token.
/// </summary>
public record ReconfirmPriceCommand(Guid BookingId, string OfferToken);

/// <summary>
/// Command to FlightConnectorService: create a PNR in the GDS for the re-confirmed offer.
/// </summary>
public record CreatePnrCommand(Guid BookingId, string OfferToken, string[] PassengerRefs);

/// <summary>
/// Compensation command to FlightConnectorService: void the PNR in the GDS.
/// </summary>
public record VoidPnrCommand(Guid BookingId, string Pnr, string Reason);

/// <summary>
/// Command to FlightConnectorService: issue the ticket against the existing PNR.
/// </summary>
public record IssueTicketCommand(Guid BookingId, string Pnr);

/// <summary>
/// Command to PaymentService: place an authorization hold on the card.
/// AmountCents is the minor-unit representation per Stripe convention.
/// </summary>
public record AuthorizePaymentCommand(
    Guid BookingId,
    decimal AmountCents,
    string Currency,
    string StripeCustomerId,
    string PaymentMethodId);

/// <summary>
/// Command to PaymentService: capture a previously authorized PaymentIntent.
/// </summary>
public record CapturePaymentCommand(Guid BookingId, string PaymentIntentId, decimal AmountCents);

/// <summary>
/// Compensation command to PaymentService: cancel an authorization that was not captured.
/// </summary>
public record CancelAuthorizationCommand(Guid BookingId, string PaymentIntentId);

/// <summary>
/// Compensation command to PaymentService: refund a captured PaymentIntent.
/// </summary>
public record RefundPaymentCommand(Guid BookingId, string PaymentIntentId, decimal AmountCents);

/// <summary>
/// B2B wallet command: place a reservation hold on the agency wallet.
/// </summary>
public record WalletReserveCommand(Guid BookingId, Guid WalletId, decimal Amount, string Currency);

/// <summary>
/// B2B wallet command: commit a previously reserved amount.
/// </summary>
public record WalletCommitCommand(Guid BookingId, Guid WalletId, Guid ReservationTxId);

/// <summary>
/// B2B wallet command: release a previously reserved amount without committing.
/// </summary>
public record WalletReleaseCommand(Guid BookingId, Guid WalletId, Guid ReservationTxId);
