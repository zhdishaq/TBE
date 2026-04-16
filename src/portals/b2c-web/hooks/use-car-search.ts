// Client-side car-hire search hook (Plan 04-04 / CARB-01, D-11, D-12).
//
// Follows the exact pattern of `use-hotel-search.ts`:
//   - queryKey derives from CRITERIA ONLY (pickup location + dates + driver age).
//   - client-side filter + sort run over the cached payload via useMemo in
//     the consumer, never by refetching.
//   - staleTime 90_000 mirrors hotels (car prices are similarly volatile).
//
// The shared key builder is exported so tests can assert stability without
// mounting React / a QueryClient.

'use client';

import { useMemo } from 'react';
import { useQueryStates } from 'nuqs';
import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import { carSearchParsers } from '@/lib/car-search-params';
import type { CarOffer } from '@/types/car';

export const CAR_SEARCH_STALE_TIME = 90_000;

export interface CarSearchCriteria {
  pickupLocation: string;
  pickupAt: Date | null;
  dropoffAt: Date | null;
  driverAge: number;
}

/**
 * Build the queryKey used by every car search query. Tuple length is 5:
 * `['cars', pickupLocation, pickupIso, dropoffIso, driverAge]`.
 * Filters + sort are intentionally NOT accepted so they cannot leak into
 * the key and cause a refetch on toggle.
 */
export function buildCarSearchQueryKey(
  q: CarSearchCriteria,
): readonly ['cars', string, string | null, string | null, number] {
  return [
    'cars',
    q.pickupLocation,
    q.pickupAt ? q.pickupAt.toISOString() : null,
    q.dropoffAt ? q.dropoffAt.toISOString() : null,
    q.driverAge,
  ] as const;
}

interface CarSearchEnvelope {
  results?: CarOffer[];
  cappedAt?: number;
}

export function useCarSearch(): UseQueryResult<CarOffer[]> & {
  criteria: CarSearchCriteria;
} {
  const [q] = useQueryStates(carSearchParsers);

  const criteria = useMemo<CarSearchCriteria>(
    () => ({
      pickupLocation: q.pickupLocation,
      pickupAt: q.pickupAt,
      dropoffAt: q.dropoffAt,
      driverAge: q.driverAge,
    }),
    [q.pickupLocation, q.pickupAt, q.dropoffAt, q.driverAge],
  );

  const queryKey = useMemo(() => buildCarSearchQueryKey(criteria), [criteria]);

  const result = useQuery<CarOffer[]>({
    queryKey,
    queryFn: async ({ signal }) => {
      const qs = new URLSearchParams({
        pickupLocation: criteria.pickupLocation,
        pickupAt: criteria.pickupAt?.toISOString() ?? '',
        dropoffAt: criteria.dropoffAt?.toISOString() ?? '',
        driverAge: String(criteria.driverAge),
      });
      const resp = await fetch(`/api/cars/search?${qs.toString()}`, {
        method: 'GET',
        headers: { Accept: 'application/json' },
        signal,
        cache: 'no-store',
      });
      if (!resp.ok) throw new Error(`Car search failed: ${resp.status}`);
      const json = (await resp.json()) as CarSearchEnvelope | CarOffer[];
      if (Array.isArray(json)) return json;
      return json.results ?? [];
    },
    staleTime: CAR_SEARCH_STALE_TIME,
    enabled: Boolean(
      criteria.pickupLocation && criteria.pickupAt && criteria.dropoffAt,
    ),
  });

  return { ...result, criteria };
}
