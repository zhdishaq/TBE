// Plan 05-02 Task 3 -- /checkout/success RSC.
//
// Reads ONLY `?booking={id}` (Pitfall 11 -- searchParams is a Promise in
// Next.js 16). Explicitly rejects any `?payment_intent=` param and returns
// a redirect -- that parameter shape belongs to Stripe (Pitfall 6) and must
// never surface on the B2B success page. The /checkout/confirm route never
// loads Stripe Elements, so `payment_intent` on the URL can only mean
// tampering or a misrouted B2C redirect -> either way, redirect to
// /dashboard.
//
// GDPR / T-05-02-05 -- no NET or commission figure is ever rendered here.
// This page only displays the booking reference so a traveller forwarded
// the URL (unlikely but possible) never sees agent-only pricing.

import Link from 'next/link';
import { notFound, redirect } from 'next/navigation';
import { auth } from '@/lib/auth';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

interface PageProps {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}

interface BookingDtoPublic {
  bookingId: string;
  pnr?: string | null;
  bookingReference?: string | null;
  status: string;
}

function firstParam(v: string | string[] | undefined): string | undefined {
  return Array.isArray(v) ? v[0] : v;
}

async function loadBooking(id: string): Promise<BookingDtoPublic | null> {
  try {
    const r = await gatewayFetch(`/agent/bookings/${encodeURIComponent(id)}`);
    if (!r.ok) return null;
    return (await r.json()) as BookingDtoPublic;
  } catch (err) {
    if (err instanceof UnauthenticatedError) throw err;
    return null;
  }
}

export default async function CheckoutSuccessPage({ searchParams }: PageProps) {
  const sp = await searchParams;

  // Pitfall 6 -- `payment_intent` is a Stripe redirect-query shape. The B2B
  // confirm route never mounts <Elements>, so its presence here is a signal
  // of misrouting or tampering. Send the user somewhere safe.
  if (firstParam(sp.payment_intent)) {
    redirect('/dashboard?error=unexpected_payment_intent');
  }

  const bookingId = firstParam(sp.booking);
  if (!bookingId) redirect('/bookings');

  const session = await auth();
  if (!session) {
    redirect(`/login?callbackUrl=/checkout/success?booking=${encodeURIComponent(bookingId)}`);
  }

  const booking = await loadBooking(bookingId);
  if (!booking) notFound();

  const reference = booking.pnr ?? booking.bookingReference ?? bookingId;

  return (
    <section className="mx-auto flex w-full max-w-2xl flex-col items-center gap-6 px-6 py-12 text-center md:px-10">
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
      <h1 className="text-2xl font-semibold">
        Booking confirmed. Reference{' '}
        <span className="font-mono">{reference}</span>.
      </h1>
      <p className="text-sm text-muted-foreground">
        The wallet debit is now a reservation and will commit when the ticket
        is issued. Your customer will receive their e-ticket by email.
      </p>
      <div className="flex flex-col gap-3 sm:flex-row">
        <Link
          href={`/api/bookings/${encodeURIComponent(booking.bookingId)}/invoice.pdf`}
          prefetch={false}
          className="inline-flex h-10 items-center justify-center rounded-md bg-indigo-600 px-4 text-sm font-medium text-white hover:bg-indigo-700"
        >
          Download invoice
        </Link>
        <Link
          href={`/api/bookings/${encodeURIComponent(booking.bookingId)}/e-ticket.pdf`}
          prefetch={false}
          className="inline-flex h-10 items-center justify-center rounded-md border border-border px-4 text-sm font-medium hover:bg-muted"
        >
          Download e-ticket
        </Link>
      </div>
      <Link
        href="/bookings"
        className="text-sm underline underline-offset-4 hover:text-foreground"
      >
        Back to bookings
      </Link>
    </section>
  );
}
