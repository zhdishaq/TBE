// nuqs parsers for hotel search state (D-11 — URL is the source of truth).
//
// Mirrors `lib/search-params.ts` (flights). Criteria that drive the
// TanStack queryKey (destination, dates, occupancy) live alongside
// client-side filter/sort state that MUST stay out of the queryKey
// (D-12 — filters apply over the cached payload without refetching).
//
// Parser names match the plan's <action>:
//   destinationCityCode, checkin, checkout, rooms, adults, children,
//   sortKey, maxPrice, minStars, propertyTypes.
//
// Bounds (rooms 1-5, adults 1-9, children 0-4) are enforced by the
// search form (zod schema) — parsers only shape + default. We keep the
// default defensive (rooms=1, adults=2, children=0) so a share-link
// without values renders sensibly.

import {
  parseAsArrayOf,
  parseAsInteger,
  parseAsIsoDate,
  parseAsString,
  parseAsStringLiteral,
} from 'nuqs/server';

export const HOTEL_SORT_KEYS = [
  'price-asc',
  'price-desc',
  'stars-desc',
  'distance-asc',
] as const;
export type HotelSortKey = (typeof HOTEL_SORT_KEYS)[number];

export const PROPERTY_TYPES = [
  'hotel',
  'apartment',
  'resort',
  'hostel',
  'guesthouse',
] as const;
export type PropertyType = (typeof PROPERTY_TYPES)[number];

export const hotelSearchParsers = {
  // --- Criteria (drive the TanStack queryKey) --------------------------
  destinationCityCode: parseAsString.withDefault(''),
  checkin: parseAsIsoDate,
  checkout: parseAsIsoDate,
  rooms: parseAsInteger.withDefault(1),
  adults: parseAsInteger.withDefault(2),
  children: parseAsInteger.withDefault(0),

  // --- Client-side filters + sort (NOT in the queryKey) ----------------
  sortKey: parseAsStringLiteral(HOTEL_SORT_KEYS).withDefault('price-asc'),
  maxPrice: parseAsInteger,
  minStars: parseAsInteger,
  propertyTypes: parseAsArrayOf(parseAsStringLiteral(PROPERTY_TYPES)),
};

export type HotelSearchParams = {
  destinationCityCode: string;
  checkin: Date | null;
  checkout: Date | null;
  rooms: number;
  adults: number;
  children: number;
  sortKey: HotelSortKey;
  maxPrice: number | null;
  minStars: number | null;
  propertyTypes: PropertyType[] | null;
};
