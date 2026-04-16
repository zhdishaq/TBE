// Booking detail (D-17). Fetches a single booking from
// `GET /bookings/{id}` (BookingService), shows itinerary fields, and
// surfaces the `Download receipt` CTA wired to our pass-through route
// at `/api/bookings/{id}/receipt.pdf` (D-15).
//
// Pitfall 11: in Next.js 16, route `params` is a Promise. Always await
// it before destructuring — otherwise we trigger
// "A param property was accessed directly with `params.id`" warnings.

import Link from 'next/link';
import { notFound, redirect } from 'next/navigation';
import { Download } from 'lucide-react';
import { auth } from '@/lib/auth';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';
import { Button } from '@/components/ui/button';
import { formatDate, formatMoney } from '@/lib/formatters';
import type { BookingDtoPublic } from '@/types/api';

export const dynamic = 'force-dynamic';

interface PageProps {
  params: Promise<{ id: string }>;
}

export default async function BookingDetailPage({ params }: PageProps) {
  // Pitfall 11 — params is a Promise on Next.js 16.
  const { id } = await params;

  const session = await auth();
  if (!session) {
    redirect(`/login?callbackUrl=/bookings/${id}`);
  }

  let booking: BookingDtoPublic | null = null;
  try {
    const res = await gatewayFetch(`/bookings/${encodeURIComponent(id)}`);
    if (res.status === 404) notFound();
    if (res.ok) {
      booking = (await res.json()) as BookingDtoPublic;
    }
  } catch (err) {
    if (err instanceof UnauthenticatedError) {
      redirect(`/login?callbackUrl=/bookings/${id}`);
    }
    throw err;
  }

  if (!booking) {
    notFound();
  }

  const date = booking.departureDate ?? booking.createdAt;
  return (
    <main className="mx-auto flex max-w-2xl flex-col gap-8 p-8">
      <nav>
        <Link
          href="/bookings"
          className="text-sm text-muted-foreground underline hover:text-foreground"
        >
          ← Back to your bookings
        </Link>
      </nav>

      <header className="flex flex-col gap-2">
        <h1 className="text-display font-semibold">
          Booking {booking.bookingReference}
        </h1>
        <p className="text-muted-foreground">
          Confirmed on {formatDate(booking.createdAt)}
        </p>
      </header>

      <section className="flex flex-col gap-3 rounded-lg border border-border bg-card p-6">
        <Row label="Reference" value={booking.bookingReference} />
        {booking.pnr ? <Row label="PNR" value={booking.pnr} /> : null}
        {booking.ticketNumber ? (
          <Row label="Ticket number" value={booking.ticketNumber} />
        ) : null}
        <Row label="Date" value={formatDate(date)} />
        <Row
          label="Total"
          value={formatMoney(booking.totalAmount, booking.currency)}
        />
      </section>

      <a
        href={`/api/bookings/${encodeURIComponent(id)}/receipt.pdf`}
        download
      >
        <Button className="w-full">
          <Download className="me-2 size-4" aria-hidden />
          Download receipt
        </Button>
      </a>
    </main>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-center justify-between gap-4 text-sm">
      <span className="text-muted-foreground">{label}</span>
      <span className="font-semibold tabular-nums">{value}</span>
    </div>
  );
}
