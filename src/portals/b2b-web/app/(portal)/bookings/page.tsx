// Plan 05-04 Task 3 — /bookings list.
//
// RSC page that server-renders the filtered + paged bookings table for
// the caller's agency. Filters + pagination state are URL-synced via
// search params so the page is shareable / bookmarkable.
//
// Backend contract: GET /api/b2b/agent/bookings?page=N&size=K&client=…&pnr=…
// returns `{ items: BookingRow[], total: number }`. The D-34 agency_id
// filter is applied server-side (BookingService.AgentBookingsController)
// — this page passes the caller's session cookie through YARP so Keycloak
// authenticates the backend request.

import { auth } from '@/lib/auth';
import { gatewayFetch } from '@/lib/api-client';
import type { BookingRow } from '@/components/bookings/table';
import { BookingsListClient } from './bookings-list-client';

export const dynamic = 'force-dynamic';
export const runtime = 'nodejs';

interface BookingsListResponse {
  items: BookingRow[];
  total: number;
  page: number;
  size: number;
}

const CLAMPED_SIZES = [20, 50, 100] as const;

async function fetchList(params: {
  page: number;
  size: number;
  client: string;
  pnr: string;
}): Promise<BookingsListResponse> {
  const qs = new URLSearchParams();
  qs.set('page', String(params.page));
  qs.set('size', String(params.size));
  if (params.client) qs.set('client', params.client);
  if (params.pnr) qs.set('pnr', params.pnr);
  try {
    const r = await gatewayFetch(`/api/b2b/agent/bookings?${qs.toString()}`);
    if (!r.ok) {
      return { items: [], total: 0, page: params.page, size: params.size };
    }
    return (await r.json()) as BookingsListResponse;
  } catch {
    return { items: [], total: 0, page: params.page, size: params.size };
  }
}

// Pitfall 14 — searchParams is a Promise in Next.js 16.
export default async function BookingsListPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const sp = await searchParams;
  await auth(); // ensures the request is authenticated; session isn't needed here.

  const rawPage = Number(sp.page ?? '1');
  const rawSize = Number(sp.size ?? '20');
  const size = (CLAMPED_SIZES as ReadonlyArray<number>).includes(rawSize)
    ? rawSize
    : 20;
  const page = Number.isFinite(rawPage) && rawPage >= 1 ? Math.floor(rawPage) : 1;
  const client = typeof sp.client === 'string' ? sp.client : '';
  const pnr = typeof sp.pnr === 'string' ? sp.pnr : '';

  const initial = await fetchList({ page, size, client, pnr });

  return (
    <main className="mx-auto flex max-w-6xl flex-col gap-6 px-6 py-8">
      <header>
        <h1 className="text-2xl font-semibold text-foreground">Bookings</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Every booking owned by your agency, filterable by client or PNR.
        </p>
      </header>
      <BookingsListClient
        initialRows={initial.items}
        initialTotal={initial.total}
        initialPage={page}
        initialSize={size}
        initialClient={client}
        initialPnr={pnr}
      />
    </main>
  );
}
