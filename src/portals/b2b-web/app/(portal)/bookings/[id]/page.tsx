// Plan 05-04 Task 3 — /bookings/[id] detail.
//
// RSC page for a single booking. Composes:
//   - BookingStatusCard (reference / PNR / status / TTL countdown)
//   - DocumentsPanel    (invoice + e-ticket links)
//   - VoidBookingButton (agent-admin only)
//
// Pitfall 14 — `params` is a Promise and MUST be awaited.
// Pitfall 10 — a 404 from the backend (booking missing OR cross-tenant)
//   redirects to /forbidden rather than surfacing the booking's existence.

import { notFound, redirect } from 'next/navigation';
import { auth } from '@/lib/auth';
import { gatewayFetch } from '@/lib/api-client';
import { BookingStatusCard } from '@/components/bookings/status-card';
import { DocumentsPanel } from '@/components/bookings/documents-panel';
import { VoidBookingButton } from '@/components/bookings/void-booking-button';

export const dynamic = 'force-dynamic';
export const runtime = 'nodejs';

interface BookingDetail {
  id: string;
  reference: string;
  pnr: string;
  status: string;
  clientName?: string;
  agentName?: string;
  ticketNumber?: string | null;
  ticketingDeadlineUtc?: string | null;
  grossAmount?: number | null;
  currency?: string | null;
}

async function fetchBooking(id: string): Promise<BookingDetail | null> {
  try {
    const r = await gatewayFetch(`/api/b2b/agent/bookings/${id}`);
    if (r.status === 404) return null;
    if (!r.ok) return null;
    return (await r.json()) as BookingDetail;
  } catch {
    return null;
  }
}

export default async function BookingDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  // Pitfall 14 — always await.
  const { id } = await params;
  const session = await auth();
  const roles = session?.roles ?? [];

  const booking = await fetchBooking(id);
  if (!booking) {
    // Pitfall 10 — redirect to a generic /forbidden page rather than
    // rendering a 404 shape that confirms the id exists for some OTHER
    // agency. notFound() would be fine too; we choose /forbidden for a
    // user-friendly "You do not have access" copy.
    redirect('/forbidden');
  }

  // Unreachable (redirect throws), but TypeScript wants the narrowing.
  if (!booking) return notFound();

  return (
    <main className="mx-auto flex max-w-5xl flex-col gap-6 px-6 py-8">
      <header className="flex items-center justify-between gap-4">
        <h1 className="text-2xl font-semibold text-foreground">Booking</h1>
        <VoidBookingButton bookingId={id} roles={roles} />
      </header>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        <div className="lg:col-span-2">
          <BookingStatusCard
            reference={booking.reference}
            pnr={booking.pnr}
            status={booking.status}
            clientName={booking.clientName}
            agentName={booking.agentName}
            ticketingDeadlineUtc={booking.ticketingDeadlineUtc ?? undefined}
            ticketNumber={booking.ticketNumber ?? undefined}
          />
        </div>
        <DocumentsPanel
          bookingId={id}
          ticketNumber={booking.ticketNumber ?? null}
          hasInvoice={booking.grossAmount !== null && booking.grossAmount !== undefined}
        />
      </div>
    </main>
  );
}
