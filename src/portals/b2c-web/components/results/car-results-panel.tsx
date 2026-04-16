'use client';

// Car results page client component (Plan 04-04 / CARB-01, D-12).
//
// Minimal stacked list (no filter rail for capstone scope). Filter + sort
// computation is `useMemo`ed over the TanStack-cached offer payload —
// NEVER a refetch. Loading state shows 6 card-shaped skeletons.

import { useMemo } from 'react';
import { useRouter } from 'next/navigation';
import { useQueryStates } from 'nuqs';
import { CarCard } from '@/components/results/car-card';
import { carSearchParsers } from '@/lib/car-search-params';
import { useCarSearch } from '@/hooks/use-car-search';

export function CarResultsPanel() {
  const router = useRouter();
  const { data = [], isLoading, isError } = useCarSearch();
  const [q] = useQueryStates(carSearchParsers);

  const filtered = useMemo(() => {
    return data.filter((o) => {
      if (q.maxPrice != null && o.totalAmount.amount > q.maxPrice) return false;
      if (q.transmissions && q.transmissions.length > 0 && !q.transmissions.includes(o.transmission)) {
        return false;
      }
      if (q.categories && q.categories.length > 0 && !q.categories.includes(o.category)) {
        return false;
      }
      return true;
    });
  }, [data, q.maxPrice, q.transmissions, q.categories]);

  const sorted = useMemo(() => {
    const arr = [...filtered];
    switch (q.sortKey) {
      case 'price-desc':
        arr.sort((a, b) => b.totalAmount.amount - a.totalAmount.amount);
        break;
      case 'category-asc':
        arr.sort((a, b) => a.category.localeCompare(b.category));
        break;
      case 'price-asc':
      default:
        arr.sort((a, b) => a.totalAmount.amount - b.totalAmount.amount);
    }
    return arr;
  }, [filtered, q.sortKey]);

  return (
    <section className="flex flex-col gap-4">
      <header className="flex items-baseline justify-between">
        <h2 className="text-lg font-semibold">Cars</h2>
        <p className="text-xs text-muted-foreground">
          {isLoading ? 'Searching…' : `${sorted.length} of ${data.length} offers`}
        </p>
      </header>
      {isError && (
        <div role="alert" className="rounded-md border border-red-200 bg-red-50 p-4 text-sm text-red-800">
          We couldn&apos;t load cars right now. Please try again.
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
          No cars match your filters.
        </p>
      )}
      <ul className="flex flex-col gap-3">
        {sorted.map((o) => (
          <li key={o.offerId}>
            <CarCard
              offer={o}
              onSelect={() => router.push(`/cars/${o.offerId}`)}
            />
          </li>
        ))}
      </ul>
    </section>
  );
}
