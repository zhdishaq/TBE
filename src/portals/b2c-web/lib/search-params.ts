// nuqs parsers — URL is the single source of truth for search state (D-11).
//
// The shape here is consumed by:
//   - hooks/use-flight-search.ts   (query key + enabled flag)
//   - components/search/flight-search-form.tsx  (form state ↔ URL)
//   - components/results/*         (filters + sort)
//
// Naming follows 04-02-PLAN step 1: adt / chd / infl / infs (NOT the
// Pattern-3 `inf`). The RESEARCH example was written before the split
// into infantsLap/infantsSeat was finalised.
//
// Short keys (adt, chd, …) are deliberate: they reduce URL bloat on
// mobile share-links per UI-SPEC §URL Hygiene.

import {
  parseAsArrayOf,
  parseAsInteger,
  parseAsIsoDate,
  parseAsString,
  parseAsStringLiteral,
} from 'nuqs/server';

export const CABIN_CLASSES = ['economy', 'premium', 'business', 'first'] as const;
export type CabinClass = (typeof CABIN_CLASSES)[number];

export const STOPS_OPTIONS = ['any', '0', '1', '2+'] as const;
export type StopsOption = (typeof STOPS_OPTIONS)[number];

export const SORT_OPTIONS = ['price', 'duration', 'departure'] as const;
export type SortOption = (typeof SORT_OPTIONS)[number];

export const TIME_WINDOWS = ['early', 'morning', 'afternoon', 'evening'] as const;
export type TimeWindow = (typeof TIME_WINDOWS)[number];

export const searchParsers = {
  // --- Criteria (these drive the TanStack queryKey) --------------------
  from: parseAsString.withDefault(''),
  to: parseAsString.withDefault(''),
  dep: parseAsIsoDate,
  ret: parseAsIsoDate,
  adt: parseAsInteger.withDefault(1),
  chd: parseAsInteger.withDefault(0),
  infl: parseAsInteger.withDefault(0),
  infs: parseAsInteger.withDefault(0),
  cabin: parseAsStringLiteral(CABIN_CLASSES).withDefault('economy'),

  // --- Client-side filters + sort (NOT in the queryKey) ----------------
  stops: parseAsArrayOf(parseAsStringLiteral(STOPS_OPTIONS)),
  airlines: parseAsArrayOf(parseAsString),
  timeWindow: parseAsArrayOf(parseAsStringLiteral(TIME_WINDOWS)),
  price: parseAsInteger, // max price in minor units (user-selected slider value)
  sort: parseAsStringLiteral(SORT_OPTIONS).withDefault('price'),
};

export type SearchParams = {
  from: string;
  to: string;
  dep: Date | null;
  ret: Date | null;
  adt: number;
  chd: number;
  infl: number;
  infs: number;
  cabin: CabinClass;
  stops: StopsOption[] | null;
  airlines: string[] | null;
  timeWindow: TimeWindow[] | null;
  price: number | null;
  sort: SortOption;
};
