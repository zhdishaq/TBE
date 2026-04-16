'use client';

// Hotel results page client component (HOTB-02, D-12).
//
// Wraps the filter rail, sort bar, and stacked hotel cards. Filter/sort
// computation is `useMemo`ed over the TanStack-cached offer payload —
// NEVER a refetch. Identical to the flights panel structurally.
//
// Loading state shows 6 card-shaped skeletons (same budget as flights).

import { useMemo } from 'react';
import { useRouter } from 'next/navigation';
import { useQueryStates } from 'nuqs';
import { HotelFilterRail } from '@/components/results/hotel-filter-rail';
import { HotelSortBar } from '@/components/results/hotel-sort-bar';
import { HotelCard } from '@/components/results/hotel-card';
import {
  hotelSearchParsers,
  type PropertyType,
} from '@/lib/hotel-search-params';
import { useHotelSearch } from '@/hooks/use-hotel-search';
import type { HotelOffer } from '@/types/hotel';

function matchesPropertyType(o: HotelOffer, types: PropertyType[] | null): boolean {
  if (!types || types.length === 0) return true;
  return types.some((t) => o.amenities.includes(t) || o.amenities.includes(`type:${t}`));
}

export function HotelResultsPanel() {
  const router = useRouter();
  const { data = [], isLoading, isError } = useHotelSearch();
  const [q] = useQueryStates(hotelSearchParsers);

  const filtered = useMemo(() => {
    return data.filter((o) => {
      if (q.maxPrice != null && o.totalAmount.amount > q.maxPrice) return false;
      if (q.minStars != null && Math.round(o.starRating) < q.minStars) return false;
      if (!matchesPropertyType(o, q.propertyTypes)) return false;
      return true;
    });
  }, [data, q.maxPrice, q.minStars, q.propertyTypes]);

  const sorted = useMemo(() => {
    const arr = [...filtered];
    switch (q.sortKey) {
      case 'price-desc':
        arr.sort((a, b) => b.totalAmount.amount - a.totalAmount.amount);
        break;
      case 'stars-desc':
        arr.sort((a, b) => b.starRating - a.starRating);
        break;
      case 'distance-asc':
        // Distance is an upstream-supplied order hint; when absent we
        // fall back to the natural server order.
        break;
      case 'price-asc':
      default:
        arr.sort((a, b) => a.totalAmount.amount - b.totalAmount.amount);
    }
    return arr;
  }, [filtered, q.sortKey]);

  return (
    <div className="grid gap-6 md:grid-cols-[280px_1fr]">
      <HotelFilterRail offers={data} />
      <section className="flex flex-col gap-4">
        <HotelSortBar total={data.length} filtered={sorted.length} />
        {isError && (
          <div role="alert" className="rounded-md border border-red-200 bg-red-50 p-4 text-sm text-red-800">
            We couldn&apos;t load hotels right now. Please try again.
          </div>
        )}
        {isLoading && (
          <ul className="flex flex-col gap-3">
            {Array.from({ length: 6 }).map((_, i) => (
              <li
                key={i}
                className="h-40 animate-pulse rounded-lg border border-border bg-muted/40"
                aria-hidden
              />
            ))}
          </ul>
        )}
        {!isLoading && !isError && sorted.length === 0 && data.length > 0 && (
          <p className="rounded-md border border-dashed border-border p-6 text-center text-sm text-muted-foreground">
            No hotels match your filters.
          </p>
        )}
        <ul className="flex flex-col gap-3">
          {sorted.map((o) => (
            <li key={o.offerId}>
              <HotelCard
                offer={o}
                onSelect={() => router.push(`/hotels/${o.offerId}`)}
              />
            </li>
          ))}
        </ul>
      </section>
    </div>
  );
}
