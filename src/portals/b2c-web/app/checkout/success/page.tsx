// /checkout/success — booking-confirmed landing (B2C-05/06, UI-SPEC §Success).
//
// Reached ONLY via /checkout/processing's poll-driven redirect when the
// saga reports `Confirmed` (Pitfall 6 / D-12). This page never trusts
// Stripe's client-side return_url as a success signal — by the time we
// render, the booking's webhook-driven saga state is already terminal.
//
// Copy (UI-SPEC §Success messages):
//   "Flight booked. Booking reference: {PNR}. Your e-ticket will arrive
//    by email within 60 seconds."
//
// Primary CTA per UI-SPEC §Global CTAs: `Download receipt` — points at
// the streaming receipt proxy shipped in 04-01
// (/api/bookings/{id}/receipt.pdf, D-15/Pitfall 14).

import Link from 'next/link';
import { notFound, redirect } from 'next/navigation';
import { Download } from 'lucide-react';

import { auth } from '@/lib/auth';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';
import type { BookingDtoPublic } from '@/types/api';

export const dynamic = 'force-dynamic';
export const runtime = 'nodejs';

interface PageProps {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}

export default async function CheckoutSuccessPage({ searchParams }: PageProps) {
  // Pitfall 11 — searchParams is a Promise in Next.js 16.
  const sp = await searchParams;
  const bookingParam = Array.isArray(sp.booking) ? sp.booking[0] : sp.booking;
  if (!bookingParam) {
    redirect('/bookings');
  }

  const session = await auth();
  if (!session) {
    redirect(
      `/login?callbackUrl=/checkout/success?booking=${encodeURIComponent(
        bookingParam,
      )}`,
    );
  }

  let booking: BookingDtoPublic | null = null;
  try {
    const res = await gatewayFetch(
      `/bookings/${encodeURIComponent(bookingParam)}`,
    );
    if (res.status === 404) notFound();
    if (res.ok) {
      booking = (await res.json()) as BookingDtoPublic;
    }
  } catch (err) {
    if (err instanceof UnauthenticatedError) {
      redirect(
        `/login?callbackUrl=/checkout/success?booking=${encodeURIComponent(
          bookingParam,
        )}`,
      );
    }
    throw err;
  }

  if (!booking) {
    notFound();
  }

  // UI-SPEC §Success: PNR is the airline record locator; fall back to
  // bookingReference when the PNR hasn't landed yet (timing edge case —
  // saga terminal is `Confirmed` but PNR attribute can lag by a tick).
  const reference = booking.pnr ?? booking.bookingReference;
  const receiptHref = `/api/bookings/${encodeURIComponent(
    booking.bookingId,
  )}/receipt.pdf`;

  return (
    <section className="mx-auto flex w-full max-w-2xl flex-col items-center px-6 py-12 text-center md:px-10">
      <div
        aria-hidden="true"
        className="flex h-14 w-14 items-center justify-center rounded-full bg-green-100 text-green-700"
      >
        <svg
          xmlns="http://www.w3.org/2000/svg"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
          strokeLinecap="round"
          strokeLinejoin="round"
          className="h-7 w-7"
        >
          <path d="M20 6L9 17l-5-5" />
        </svg>
      </div>

      <h1 className="mt-6 text-2xl font-semibold">
        Flight booked. Booking reference:{' '}
        <span className="font-mono">{reference}</span>.
      </h1>
      <p className="mt-2 text-sm text-muted-foreground">
        Your e-ticket will arrive by email within 60 seconds.
      </p>

      <div className="mt-8 flex flex-col gap-3 sm:flex-row">
        <Link
          href={receiptHref}
          prefetch={false}
          className="inline-flex items-center justify-center gap-2 rounded-md bg-blue-600 px-5 py-2.5 text-sm font-medium text-white hover:bg-blue-700"
        >
          <Download className="h-4 w-4" aria-hidden="true" />
          Download receipt
        </Link>
        <Link
          href={`/bookings/${encodeURIComponent(booking.bookingId)}`}
          className="inline-flex items-center justify-center rounded-md border border-border px-5 py-2.5 text-sm font-medium hover:bg-muted"
        >
          View booking details
        </Link>
      </div>
    </section>
  );
}
