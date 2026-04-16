// Task 2 RED — useFlightSearch queryKey stability + staleTime.
//
// Contract:
//   - queryKey derives ONLY from search criteria (from / to / dep / ret /
//     adt / chd / infl / infs / cabin). Filters + sort are NOT in the key.
//   - staleTime is 90_000ms (selection-phase TTL, Pitfall 11).
//   - `enabled` is false until {from, to, dep} are all set.

import { describe, it, expect } from 'vitest';
import { buildFlightSearchQueryKey, FLIGHT_SEARCH_STALE_TIME } from '@/hooks/use-flight-search';

describe('buildFlightSearchQueryKey', () => {
  const base = {
    from: 'LHR',
    to: 'JFK',
    dep: new Date('2026-06-01T00:00:00.000Z'),
    ret: null as Date | null,
    adt: 1,
    chd: 0,
    infl: 0,
    infs: 0,
    cabin: 'economy' as const,
  };

  it('produces a stable key for identical inputs', () => {
    const a = buildFlightSearchQueryKey(base);
    const b = buildFlightSearchQueryKey({ ...base });
    expect(a).toEqual(b);
  });

  it('changes when search criteria change (origin)', () => {
    const a = buildFlightSearchQueryKey(base);
    const b = buildFlightSearchQueryKey({ ...base, from: 'LGW' });
    expect(a).not.toEqual(b);
  });

  it('does NOT change when sort / filters change', () => {
    // The shared builder intentionally accepts NO filter/sort args so it is
    // impossible for the caller to accidentally include them.
    // This is enforced by the function's signature + a runtime check that
    // the returned array has length 10 (['flights'] + 9 criteria fields).
    const key = buildFlightSearchQueryKey(base);
    expect(key[0]).toBe('flights');
    expect(key.length).toBe(10);
  });

  it('exports FLIGHT_SEARCH_STALE_TIME = 90_000 (Pitfall 11)', () => {
    expect(FLIGHT_SEARCH_STALE_TIME).toBe(90_000);
  });
});
