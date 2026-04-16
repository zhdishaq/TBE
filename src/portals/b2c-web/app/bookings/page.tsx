// Customer dashboard (D-17 + UI-SPEC §Dashboard).
//
// RSC: reads `GET /customers/me/bookings` server-side via `gatewayFetch`
// (Bearer is forwarded automatically), partitions into Upcoming / Past
// by `departureDate ?? createdAt`, and renders the client-side
// `<DashboardTabs>`.
//
// Pitfall 15 — `force-dynamic` so the page always sees the freshest list
// (a brand-new booking would otherwise stay invisible until cache TTL).
// T-04-01-06 mitigation.

import { redirect } from 'next/navigation';
import { auth } from '@/lib/auth';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';
import { DashboardTabs } from '@/components/account/dashboard-tabs';
import type { BookingDtoPublic, CustomerBookingsPage } from '@/types/api';

export const dynamic = 'force-dynamic';

export const metadata = {
  title: 'Your bookings',
};

export default async function BookingsPage() {
  const session = await auth();
  if (!session) {
    redirect('/login?callbackUrl=/bookings');
  }

  let upcoming: BookingDtoPublic[] = [];
  let past: BookingDtoPublic[] = [];

  try {
    const res = await gatewayFetch('/customers/me/bookings');
    if (res.ok) {
      const payload = (await res.json()) as Partial<CustomerBookingsPage>;
      const items = payload.items ?? [];
      ({ upcoming, past } = partition(items));
    }
  } catch (err) {
    if (err instanceof UnauthenticatedError) {
      redirect('/login?callbackUrl=/bookings');
    }
    // Non-auth errors fall through to an empty dashboard so the page
    // still renders the empty-state copy instead of crashing into an
    // error boundary. The gateway layer already logs the upstream
    // failure with full context.
  }

  return (
    <main className="mx-auto flex max-w-3xl flex-col gap-8 p-8">
      <header className="flex flex-col gap-2">
        <h1 className="text-display font-semibold">Your bookings</h1>
        <p className="text-muted-foreground">
          Upcoming trips and past bookings, all in one place.
        </p>
      </header>
      <DashboardTabs upcoming={upcoming} past={past} />
    </main>
  );
}

/**
 * Split bookings into Upcoming (departure in the future) and Past
 * (departure in the past). Falls back to `createdAt` when the backend
 * has not yet populated `departureDate` for that product type.
 *
 * Upcoming is sorted ascending (soonest first); Past is sorted
 * descending (most recent first) — UI-SPEC §Dashboard.
 */
export function partition(items: BookingDtoPublic[]): {
  upcoming: BookingDtoPublic[];
  past: BookingDtoPublic[];
} {
  const now = Date.now();
  const upcoming: BookingDtoPublic[] = [];
  const past: BookingDtoPublic[] = [];

  for (const item of items) {
    const dateStr = item.departureDate ?? item.createdAt;
    const ts = new Date(dateStr).getTime();
    if (Number.isFinite(ts) && ts > now) {
      upcoming.push(item);
    } else {
      past.push(item);
    }
  }

  upcoming.sort(
    (a, b) =>
      new Date(a.departureDate ?? a.createdAt).getTime() -
      new Date(b.departureDate ?? b.createdAt).getTime(),
  );
  past.sort(
    (a, b) =>
      new Date(b.departureDate ?? b.createdAt).getTime() -
      new Date(a.departureDate ?? a.createdAt).getTime(),
  );

  return { upcoming, past };
}
