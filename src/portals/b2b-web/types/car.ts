// Car hire domain types for the B2C portal (Plan 04-04 / CARB-01..03).
//
// Shape mirrors the gateway contract:
//   GET /cars/search → { results: CarOffer[], cappedAt: 200 }
//
// `pickupAt` / `dropoffAt` are serialised as ISO-8601 strings for URL round-trip safety;
// components parse them to Date as needed.

import type { CancellationPolicy, Money } from './hotel';

/** Transmission discriminator — UI-SPEC lowercase wire values. */
export type Transmission = 'automatic' | 'manual';

/**
 * A single car-hire offer returned by the search fan-out. `dailyRate` is per-rental-day
 * (24h windows); `totalAmount` is the all-in price shown on the card and locked at
 * checkout.
 */
export interface CarOffer {
  offerId: string;
  vendorName: string;
  vendorLogo?: string;
  category: string;           // e.g. "Compact", "SUV", "Luxury"
  transmission: Transmission;
  seats: number;
  bags: number;
  pickupLocation: string;
  dropoffLocation: string;
  pickupAt: string;           // ISO-8601
  dropoffAt: string;          // ISO-8601
  dailyRate: Money;
  totalAmount: Money;
  cancellationPolicy: CancellationPolicy;
}

/** Raw search response envelope — `cappedAt` is the server-enforced cap (200). */
export interface CarSearchResponse {
  results: CarOffer[];
  cappedAt: number;
}
