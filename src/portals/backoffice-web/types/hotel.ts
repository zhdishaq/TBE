// Hotel domain types for the B2C portal.
//
// Shapes mirror the gateway contract from
// `.planning/phases/04-b2c-portal-customer-facing/04-03-PLAN.md` <interfaces>:
//   GET /hotels/search → { results: HotelOffer[], cappedAt: 200 }
//
// All currency is rendered via `Intl.NumberFormat` (Pitfall 13 — never a
// hardcoded £). The `Money` helper keeps the numeric + ISO-4217 pair
// together so we never drift the two apart through prop drilling.

/** ISO-4217 currency + decimal amount. Optional price breakdown for the card expand. */
export interface Money {
  amount: number;
  currency: string;
  displayBreakdown?: {
    base: number;
    taxes: number;
  };
}

/**
 * Cancellation policy discriminator — UI-SPEC strings are:
 *   free           → "Free cancellation"
 *   nonRefundable  → "Non-refundable"
 *   flexible       → "Flexible"
 *
 * The constants below are the wire values; components map them to UI
 * copy. We keep the wire shape the same as the gateway to avoid a second
 * translation layer.
 */
export type CancellationPolicy = 'free' | 'nonRefundable' | 'flexible';

/** Occupancy hint used by the search form and result cards. */
export interface OccupancySpec {
  rooms: number;
  adults: number;
  children: number;
}

/**
 * A single bookable room within a HotelOffer. The room carries its own
 * cancellation policy + price — the HotelOffer's top-level nightlyRate /
 * totalAmount is the "lead" room (cheapest matching the occupancy).
 */
export interface Room {
  roomId: string;
  name: string;
  description?: string;
  bedType?: string;
  maxOccupancy: number;
  nightlyRate: Money;
  totalAmount: Money;
  cancellationPolicy: CancellationPolicy;
  amenities?: string[];
}

/** A HotelOffer as returned by the Phase-2 search adapter fan-out. */
export interface HotelOffer {
  offerId: string;
  propertyId: string;
  name: string;
  starRating: number;
  address: string;
  photos: string[];
  amenities: string[];
  cancellationPolicy: CancellationPolicy;
  rooms: Room[];
  nightlyRate: Money;
  totalAmount: Money;
}

/** Raw search response envelope — `cappedAt` is the server-enforced cap (200). */
export interface HotelSearchResponse {
  results: HotelOffer[];
  cappedAt: number;
}
