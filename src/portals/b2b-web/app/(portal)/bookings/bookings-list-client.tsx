// Plan 05-04 Task 3 — /bookings client island.
//
// Hosts the filter form + table + pager. URL-synchronises filters and
// paging via `useRouter().replace(href)` so the state is shareable and
// Back/Forward buttons work as expected.

'use client';

import { useCallback, useEffect, useState, useTransition } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import {
  BookingsFilters,
  type BookingsFiltersValue,
} from '@/components/bookings/filters';
import { BookingsTable, type BookingRow } from '@/components/bookings/table';
import { BookingsPager } from '@/components/bookings/pager';

interface BookingsListClientProps {
  initialRows: BookingRow[];
  initialTotal: number;
  initialPage: number;
  initialSize: number;
  initialClient: string;
  initialPnr: string;
}

export function BookingsListClient(props: BookingsListClientProps) {
  const router = useRouter();
  const searchParams = useSearchParams();
  const [, startTransition] = useTransition();
  const [filters, setFilters] = useState<BookingsFiltersValue>({
    client: props.initialClient,
    pnr: props.initialPnr,
    status: (searchParams.get('status') as string) ?? '',
    from: (searchParams.get('from') as string) ?? '',
    to: (searchParams.get('to') as string) ?? '',
  });

  const syncUrl = useCallback(
    (patch: Partial<{
      page: number;
      size: number;
      client: string;
      pnr: string;
      status: string;
      from: string;
      to: string;
    }>) => {
      const sp = new URLSearchParams(searchParams.toString());
      for (const [k, v] of Object.entries(patch)) {
        if (v === undefined || v === null || v === '') sp.delete(k);
        else sp.set(k, String(v));
      }
      const qs = sp.toString();
      startTransition(() => {
        router.replace(qs ? `/bookings?${qs}` : '/bookings');
      });
    },
    [router, searchParams],
  );

  // Debounce filter → URL sync so typing doesn't spam the server.
  useEffect(() => {
    const id = setTimeout(() => {
      syncUrl({
        client: filters.client,
        pnr: filters.pnr,
        status: filters.status,
        from: filters.from,
        to: filters.to,
        page: 1, // reset to page 1 on filter change
      });
    }, 300);
    return () => clearTimeout(id);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filters.client, filters.pnr, filters.status, filters.from, filters.to]);

  return (
    <div className="flex flex-col gap-4">
      <BookingsFilters value={filters} onChange={setFilters} />

      <div className="rounded-lg border border-border bg-card overflow-hidden">
        <BookingsTable rows={props.initialRows} />
        <BookingsPager
          page={props.initialPage}
          size={props.initialSize}
          total={props.initialTotal}
          onNavigate={(p) => syncUrl({ page: p })}
          onSizeChange={(s) => syncUrl({ size: s, page: 1 })}
        />
      </div>
    </div>
  );
}
