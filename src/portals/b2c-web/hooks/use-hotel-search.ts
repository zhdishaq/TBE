// Client-side hotel search hook (D-11, D-12).
//
// Follows the exact pattern of `use-flight-search.ts`:
//   - queryKey derives from CRITERIA ONLY (destination + dates + occupancy)
//   - client-side filter + sort run over the cached payload via useMemo
//     in the consumer, never by refetching
//   - staleTime 90_000 mirrors flights (hotel prices are similarly volatile)
//
// The shared key builder is exported so tests can assert stability without
// mounting React / a QueryClient.

'use client';

import { useMemo } from 'react';
import { useQueryStates } from 'nuqs';
import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import { hotelSearchParsers } from '@/lib/hotel-search-params';
import type { HotelOffer } from '@/types/hotel';

export const HOTEL_SEARCH_STALE_TIME = 90_000;

export interface HotelSearchCriteria {
  destinationCityCode: string;
  checkin: Date | null;
  checkout: Date | null;
  rooms: number;
  adults: number;
  children: number;
}

/**
 * Build the queryKey used by every hotel search query. Tuple length is 7:
 * `['hotels', dest, checkinIso, checkoutIso, rooms, adults, children]`.
 * Filters + sort are intentionally NOT accepted so they cannot leak into
 * the key and cause a refetch on toggle.
 */
export function buildHotelSearchQueryKey(
  q: HotelSearchCriteria,
): readonly [
  'hotels',
  string,
  string | null,
  string | null,
  number,
  number,
  number,
] {
  return [
    'hotels',
    q.destinationCityCode,
    q.checkin ? q.checkin.toISOString() : null,
    q.checkout ? q.checkout.toISOString() : null,
    q.rooms,
    q.adults,
    q.children,
  ] as const;
}

interface HotelSearchEnvelope {
  results?: HotelOffer[];
  cappedAt?: number;
}

export function useHotelSearch(): UseQueryResult<HotelOffer[]> & {
  criteria: HotelSearchCriteria;
} {
  const [q] = useQueryStates(hotelSearchParsers);

  const criteria = useMemo<HotelSearchCriteria>(
    () => ({
      destinationCityCode: q.destinationCityCode,
      checkin: q.checkin,
      checkout: q.checkout,
      rooms: q.rooms,
      adults: q.adults,
      children: q.children,
    }),
    [q.destinationCityCode, q.checkin, q.checkout, q.rooms, q.adults, q.children],
  );

  const queryKey = useMemo(() => buildHotelSearchQueryKey(criteria), [criteria]);

  const result = useQuery<HotelOffer[]>({
    queryKey,
    queryFn: async ({ signal }) => {
      const qs = new URLSearchParams({
        destination: criteria.destinationCityCode,
        checkin: criteria.checkin?.toISOString().slice(0, 10) ?? '',
        checkout: criteria.checkout?.toISOString().slice(0, 10) ?? '',
        rooms: String(criteria.rooms),
        adults: String(criteria.adults),
        children: String(criteria.children),
      });
      const resp = await fetch(`/api/hotels/search?${qs.toString()}`, {
        method: 'GET',
        headers: { Accept: 'application/json' },
        signal,
        cache: 'no-store',
      });
      if (!resp.ok) throw new Error(`Hotel search failed: ${resp.status}`);
      const json = (await resp.json()) as HotelSearchEnvelope | HotelOffer[];
      if (Array.isArray(json)) return json;
      return json.results ?? [];
    },
    staleTime: HOTEL_SEARCH_STALE_TIME,
    enabled: Boolean(
      criteria.destinationCityCode && criteria.checkin && criteria.checkout,
    ),
  });

  return { ...result, criteria };
}
