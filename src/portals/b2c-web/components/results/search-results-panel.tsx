'use client';

// Results page client component (B2C-04, D-12). Wraps the filter rail,
// sort bar, and stacked cards. Filter/sort computation is `useMemo`ed
// over the TanStack-cached offer payload — NEVER a refetch.
//
// Loading state shows the UI-SPEC skeleton (6 card-shaped placeholders).

import { useMemo } from 'react';
import { useRouter } from 'next/navigation';
import { useQueryStates } from 'nuqs';
import { FilterRail } from '@/components/results/filter-rail';
import { SortBar } from '@/components/results/sort-bar';
import { FlightCard } from '@/components/results/flight-card';
import { searchParsers, type StopsOption, type TimeWindow } from '@/lib/search-params';
import {
  useFlightSearch,
  type FlightOffer,
} from '@/hooks/use-flight-search';

function matchesStops(o: FlightOffer, stops: StopsOption[] | null): boolean {
  if (!stops || stops.length === 0 || stops.includes('any')) return true;
  return stops.some((s) => {
    if (s === '2+') return o.stops >= 2;
    return o.stops === Number(s);
  });
}

function matchesTime(o: FlightOffer, wins: TimeWindow[] | null): boolean {
  if (!wins || wins.length === 0) return true;
  const h = new Date(o.segments[0].departure).getHours();
  return wins.some((w) => {
    if (w === 'early') return h >= 0 && h < 6;
    if (w === 'morning') return h >= 6 && h < 12;
    if (w === 'afternoon') return h >= 12 && h < 18;
    return h >= 18 && h < 24;
  });
}

export function SearchResultsPanel() {
  const router = useRouter();
  const { data = [], isLoading, isError } = useFlightSearch();
  const [q] = useQueryStates(searchParsers);

  const filtered = useMemo(() => {
    return data.filter((o) => {
      if (!matchesStops(o, q.stops)) return false;
      if (q.airlines && q.airlines.length && !q.airlines.includes(o.airline.code)) return false;
      if (!matchesTime(o, q.timeWindow)) return false;
      if (q.price != null && o.price.total > q.price) return false;
      return true;
    });
  }, [data, q.stops, q.airlines, q.timeWindow, q.price]);

  const sorted = useMemo(() => {
    const arr = [...filtered];
    switch (q.sort) {
      case 'duration':
        arr.sort((a, b) => a.durationMinutes - b.durationMinutes);
        break;
      case 'departure':
        arr.sort(
          (a, b) =>
            new Date(a.segments[0].departure).getTime() -
            new Date(b.segments[0].departure).getTime(),
        );
        break;
      case 'price':
      default:
        arr.sort((a, b) => a.price.total - b.price.total);
    }
    return arr;
  }, [filtered, q.sort]);

  return (
    <div className="grid gap-6 md:grid-cols-[280px_1fr]">
      <FilterRail offers={data} />
      <section className="flex flex-col gap-4">
        <SortBar total={data.length} filtered={sorted.length} />
        {isError && (
          <div role="alert" className="rounded-md border border-red-200 bg-red-50 p-4 text-sm text-red-800">
            We couldn&apos;t load flights right now. Please try again.
          </div>
        )}
        {isLoading && (
          <ul className="flex flex-col gap-3">
            {Array.from({ length: 6 }).map((_, i) => (
              <li
                key={i}
                className="h-32 animate-pulse rounded-lg border border-border bg-muted/40"
                aria-hidden
              />
            ))}
          </ul>
        )}
        {!isLoading && !isError && sorted.length === 0 && data.length > 0 && (
          <p className="rounded-md border border-dashed border-border p-6 text-center text-sm text-muted-foreground">
            No flights match your filters.
          </p>
        )}
        <ul className="flex flex-col gap-3">
          {sorted.map((o) => (
            <li key={o.offerId}>
              <FlightCard
                offer={o}
                onSelect={() => router.push(`/flights/${o.offerId}`)}
              />
            </li>
          ))}
        </ul>
      </section>
    </div>
  );
}
