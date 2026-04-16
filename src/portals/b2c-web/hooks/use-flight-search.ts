// Client-side flight search hook (D-11, D-12).
//
// The queryKey MUST derive from search criteria only — filters + sort
// apply client-side in the consumer via useMemo over the cached data.
// This is what lets us do a single upstream POST /search/flights and
// then twiddle filters without re-hitting the GDS fan-out.
//
// The shared key builder is exported so the suite can assert stability
// without mounting React / a QueryClient (see tests/search/use-flight-search.test.ts).

'use client';

import { useMemo } from 'react';
import { useQueryStates } from 'nuqs';
import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import { searchParsers, type CabinClass } from '@/lib/search-params';

export const FLIGHT_SEARCH_STALE_TIME = 90_000;

export interface FlightSearchCriteria {
  from: string;
  to: string;
  dep: Date | null;
  ret: Date | null;
  adt: number;
  chd: number;
  infl: number;
  infs: number;
  cabin: CabinClass;
}

export type FlightOffer = {
  offerId: string;
  airline: { code: string; name: string };
  segments: Array<{
    from: string;
    to: string;
    departure: string;
    arrival: string;
    flightNumber: string;
    cabin: CabinClass;
  }>;
  stops: number;
  durationMinutes: number;
  price: {
    base: number;
    yqYr: number;
    taxes: number;
    total: number;
    currency: string;
  };
  baggage?: { included: number; unit: 'kg' | 'pcs' } | null;
  expiresAt: string;
};

/**
 * Build the queryKey used by every flight search query. The returned
 * tuple always has length 10: `['flights', from, to, depIso, retIso,
 * adt, chd, infl, infs, cabin]`. Filters + sort are intentionally NOT
 * accepted as arguments so they cannot sneak into the key.
 */
export function buildFlightSearchQueryKey(
  q: FlightSearchCriteria,
): readonly [
  'flights',
  string,
  string,
  string | null,
  string | null,
  number,
  number,
  number,
  number,
  CabinClass,
] {
  return [
    'flights',
    q.from,
    q.to,
    q.dep ? q.dep.toISOString() : null,
    q.ret ? q.ret.toISOString() : null,
    q.adt,
    q.chd,
    q.infl,
    q.infs,
    q.cabin,
  ] as const;
}

export function useFlightSearch(): UseQueryResult<FlightOffer[]> & {
  criteria: FlightSearchCriteria;
} {
  const [q] = useQueryStates(searchParsers);

  const criteria = useMemo<FlightSearchCriteria>(
    () => ({
      from: q.from,
      to: q.to,
      dep: q.dep,
      ret: q.ret,
      adt: q.adt,
      chd: q.chd,
      infl: q.infl,
      infs: q.infs,
      cabin: q.cabin,
    }),
    [q.from, q.to, q.dep, q.ret, q.adt, q.chd, q.infl, q.infs, q.cabin],
  );

  const queryKey = useMemo(() => buildFlightSearchQueryKey(criteria), [criteria]);

  const result = useQuery<FlightOffer[]>({
    queryKey,
    queryFn: async ({ signal }) => {
      const body = JSON.stringify({
        from: criteria.from,
        to: criteria.to,
        dep: criteria.dep?.toISOString().slice(0, 10),
        ret: criteria.ret?.toISOString().slice(0, 10) ?? null,
        adults: criteria.adt,
        children: criteria.chd,
        infantsLap: criteria.infl,
        infantsSeat: criteria.infs,
        cabin: criteria.cabin,
      });
      const resp = await fetch('/api/search/flights', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body,
        signal,
        cache: 'no-store',
      });
      if (!resp.ok) throw new Error(`Search failed: ${resp.status}`);
      const json = (await resp.json()) as { offers?: FlightOffer[] } | FlightOffer[];
      return Array.isArray(json) ? json : (json.offers ?? []);
    },
    staleTime: FLIGHT_SEARCH_STALE_TIME,
    enabled: Boolean(criteria.from && criteria.to && criteria.dep),
  });

  return { ...result, criteria };
}
