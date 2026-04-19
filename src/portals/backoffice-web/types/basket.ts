// Trip Builder basket domain types (Plan 04-04 / PKG-01..04, D-08).
//
// Mirrors the gateway contract:
//   POST /baskets → BasketDtoPublic
//   POST /baskets/{id}/payment-intents → { clientSecret }
//
// D-08 invariant: ONE `clientSecret` / ONE `paymentIntentId` per basket.
// A `flightClientSecret` + `hotelClientSecret` pair would be the
// forbidden two-PI strategy.

import type { CancellationPolicy, Money } from './hotel';

export type BasketStatus =
  | 'Pending'
  | 'Authorizing'
  | 'Confirmed'
  | 'PartiallyConfirmed'
  | 'Failed'
  | 'Cancelled';

/** Flight line-item inside a basket. Carries its OWN cancellation policy (PKG-04). */
export interface FlightLineItem {
  offerId: string;
  /** Human-readable label for the basket footer (e.g. "LHR → JFK, 04 May"). */
  summary: string;
  amount: Money;
  cancellationPolicy: CancellationPolicy;
}

/** Hotel line-item inside a basket. Carries its OWN cancellation policy (PKG-04). */
export interface HotelLineItem {
  offerId: string;
  summary: string;
  amount: Money;
  cancellationPolicy: CancellationPolicy;
}

/** Car line-item — included for symmetry; the Phase-4 Trip Builder only wires flight+hotel. */
export interface CarLineItem {
  offerId: string;
  summary: string;
  amount: Money;
  cancellationPolicy: CancellationPolicy;
}

/**
 * In-browser basket projection. `basketId`, `clientSecret`,
 * `paymentIntentId` are populated only after `createServerBasket()` +
 * `initPaymentIntent()` have run.
 */
export interface Basket {
  basketId: string | null;
  flight?: FlightLineItem;
  hotel?: HotelLineItem;
  car?: CarLineItem;
  totalAmount: number;
  currency: string;
  clientSecret: string | null;
  paymentIntentId: string | null;
  status: BasketStatus;
}
