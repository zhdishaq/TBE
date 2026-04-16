// nuqs parsers for car-hire search state (Plan 04-04 / CARB-01, D-11).
//
// Mirrors `hotel-search-params.ts`. Criteria that drive the TanStack
// queryKey (pickupLocation, pickupAt, dropoffAt, driverAge) live alongside
// client-side filter/sort state that MUST stay out of the queryKey so the
// user can toggle filters without a refetch (D-12).
//
// Parser names match the plan's <action>:
//   pickupLocation, pickupAt, dropoffAt, driverAge,
//   sortKey, maxPrice, transmissions, categories.
//
// Bounds (driverAge 18-99) are enforced by the search form's validator
// below — parsers only shape + default. We keep defaults defensive
// (driverAge=30) so a share-link without values still renders.

import {
  parseAsArrayOf,
  parseAsInteger,
  parseAsIsoDateTime,
  parseAsString,
  parseAsStringLiteral,
} from 'nuqs/server';
import type { Transmission } from '@/types/car';

export const CAR_SORT_KEYS = [
  'price-asc',
  'price-desc',
  'category-asc',
] as const;
export type CarSortKey = (typeof CAR_SORT_KEYS)[number];

export const TRANSMISSIONS = ['automatic', 'manual'] as const satisfies readonly Transmission[];

export const carSearchParsers = {
  // --- Criteria (drive the TanStack queryKey) --------------------------
  pickupLocation: parseAsString.withDefault(''),
  pickupAt: parseAsIsoDateTime,
  dropoffAt: parseAsIsoDateTime,
  driverAge: parseAsInteger.withDefault(30),

  // --- Client-side filters + sort (NOT in the queryKey) ----------------
  sortKey: parseAsStringLiteral(CAR_SORT_KEYS).withDefault('price-asc'),
  maxPrice: parseAsInteger,
  transmissions: parseAsArrayOf(parseAsStringLiteral(TRANSMISSIONS)),
  categories: parseAsArrayOf(parseAsString),
};

export type CarSearchParams = {
  pickupLocation: string;
  pickupAt: Date | null;
  dropoffAt: Date | null;
  driverAge: number;
  sortKey: CarSortKey;
  maxPrice: number | null;
  transmissions: Transmission[] | null;
  categories: string[] | null;
};
