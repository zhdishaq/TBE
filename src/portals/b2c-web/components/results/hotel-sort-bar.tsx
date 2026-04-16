'use client';

// Hotel sort bar (HOTB-02, UI-SPEC §Results Sort).
//
// Mirrors the flights `SortBar`: `sort` flows through nuqs so a refresh
// keeps the user's selection. Changing `sortKey` does NOT refetch — it
// lives OUTSIDE the TanStack queryKey by design (D-12).
//
// The 4 sort keys are the ones declared in `hotel-search-params.ts`:
//   price-asc, price-desc, stars-desc, distance-asc.

import { useRouter } from 'next/navigation';
import { useQueryStates } from 'nuqs';
import {
  hotelSearchParsers,
  type HotelSortKey,
} from '@/lib/hotel-search-params';

export interface HotelSortBarProps {
  total: number;
  filtered: number;
}

const OPTIONS: Array<{ value: HotelSortKey; label: string }> = [
  { value: 'price-asc', label: 'Price (low → high)' },
  { value: 'price-desc', label: 'Price (high → low)' },
  { value: 'stars-desc', label: 'Stars (high → low)' },
  { value: 'distance-asc', label: 'Distance (near → far)' },
];

export function HotelSortBar({ total, filtered }: HotelSortBarProps) {
  const router = useRouter();
  const [q, setQ] = useQueryStates(hotelSearchParsers);

  function clearFilters() {
    void setQ({
      maxPrice: null,
      minStars: null,
      propertyTypes: null,
    });
  }

  return (
    <div className="flex flex-col gap-2 border-b border-border pb-3 md:flex-row md:items-center md:justify-between">
      <div className="flex flex-col">
        <p className="text-sm font-medium">
          {filtered} of {total} hotels
        </p>
        {filtered === 0 && total > 0 && (
          <p className="text-xs text-red-600">
            No hotels match your filters.{' '}
            <button
              type="button"
              onClick={clearFilters}
              className="underline hover:no-underline"
            >
              Clear filters?
            </button>
          </p>
        )}
        {total === 0 && (
          <p className="text-xs text-muted-foreground">
            No results.{' '}
            <button
              type="button"
              onClick={() => router.push('/hotels')}
              className="underline hover:no-underline"
            >
              Change search
            </button>
          </p>
        )}
      </div>
      <label className="flex items-center gap-2 text-sm">
        <span className="text-muted-foreground">Sort by</span>
        <select
          value={q.sortKey}
          onChange={(e) => void setQ({ sortKey: e.target.value as HotelSortKey })}
          className="rounded-md border border-input bg-background px-2 py-1 text-sm"
          aria-label="Sort hotels"
        >
          {OPTIONS.map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </select>
      </label>
    </div>
  );
}
