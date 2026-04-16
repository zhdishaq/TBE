'use client';

// Sort bar above the result list (UI-SPEC §Results). The `sort` param
// flows through nuqs so a refresh keeps the user's selection. Changing
// `sort` does NOT refetch (it's not part of the TanStack queryKey).
//
// Empty-state copy is verbatim UI-SPEC: "No flights match your filters.
// Clear filters?"

import { useRouter } from 'next/navigation';
import { useQueryStates } from 'nuqs';
import { searchParsers, type SortOption } from '@/lib/search-params';

export interface SortBarProps {
  total: number;
  filtered: number;
}

const OPTIONS: Array<{ value: SortOption; label: string }> = [
  { value: 'price', label: 'Price (low → high)' },
  { value: 'duration', label: 'Duration (short → long)' },
  { value: 'departure', label: 'Departure time' },
];

export function SortBar({ total, filtered }: SortBarProps) {
  const router = useRouter();
  const [q, setQ] = useQueryStates(searchParsers);

  function clearFilters() {
    void setQ({
      stops: null,
      airlines: null,
      timeWindow: null,
      price: null,
    });
  }

  return (
    <div className="flex flex-col gap-2 border-b border-border pb-3 md:flex-row md:items-center md:justify-between">
      <div className="flex flex-col">
        <p className="text-sm font-medium">
          {filtered} of {total} flights
        </p>
        {filtered === 0 && total > 0 && (
          <p className="text-xs text-red-600">
            No flights match your filters.{' '}
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
              onClick={() => router.push('/flights')}
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
          value={q.sort}
          onChange={(e) => void setQ({ sort: e.target.value as SortOption })}
          className="rounded-md border border-input bg-background px-2 py-1 text-sm"
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
