// /checkout/details — unified passenger/guest info entry (B5 / Plan 04-04).
//
// Reads the unified `?ref={kind}-{id}` query via `parseCheckoutRef` and
// branches: `flight` → offerId-driven PassengerDetailsForm (existing
// 04-02 flight flow), `hotel`/`basket`/`car` → a lightweight summary
// that jumps straight to /checkout/payment with the same ref (those
// flows collected guest info at booking-creation time already, so no
// per-passenger form is needed here). No page reads `?booking`,
// `?hotelBookingId`, or `?basketId` — those legacy params are removed.
//
// Pitfall 11: `searchParams` is a Promise in Next.js 16.

import Link from 'next/link';
import { redirect } from 'next/navigation';

import { PassengerDetailsForm } from '@/components/checkout/passenger-details-form';
import { CheckoutStepper } from '@/components/checkout/stepper';
import { buildCheckoutRef, parseCheckoutRef } from '@/lib/checkout-ref';

export const dynamic = 'force-dynamic';

interface PageProps {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}

function numParam(v: string | string[] | undefined, fallback: number): number {
  const s = Array.isArray(v) ? v[0] : v;
  if (!s) return fallback;
  const n = Number(s);
  return Number.isFinite(n) ? Math.max(0, Math.floor(n)) : fallback;
}

function firstParam(v: string | string[] | undefined): string | undefined {
  return Array.isArray(v) ? v[0] : v;
}

export default async function CheckoutDetailsPage({ searchParams }: PageProps) {
  const sp = await searchParams;
  const ref = parseCheckoutRef(firstParam(sp.ref));

  if (!ref) {
    // No valid ref → send the user back to search. We don't surface an
    // error toast here because a bare /checkout/details hit is almost
    // always a typo or an aged share-link.
    redirect('/flights');
  }

  // Flight path keeps the existing passenger-details form; the "id" in
  // the ref is the offerId (flights collect pax at the checkout step,
  // unlike hotel/car which collect guest info at book-time).
  if (ref.kind === 'flight') {
    const adt = numParam(sp.adt, 1);
    const chd = numParam(sp.chd, 0);
    const infl = numParam(sp.infl, 0);
    const infs = numParam(sp.infs, 0);

    return (
      <>
        <CheckoutStepper currentStep="details" />
        <section className="mx-auto w-full max-w-3xl px-6 py-8 md:px-10">
          <h1 className="text-2xl font-semibold">Passenger details</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Enter details exactly as they appear on each traveller&apos;s passport.
          </p>

          <div className="mt-8">
            <PassengerDetailsForm
              offerId={ref.id}
              adt={adt}
              chd={chd}
              infl={infl}
              infs={infs}
            />
          </div>
        </section>
      </>
    );
  }

  // hotel / car / basket all collected guest info at book-time. The
  // details step for them is a confirmation summary that forwards to
  // payment with the SAME ref.
  const kindLabel =
    ref.kind === 'hotel' ? 'Hotel booking' : ref.kind === 'car' ? 'Car hire' : 'Trip basket';

  const nextUrl = `/checkout/payment?ref=${buildCheckoutRef(ref.kind, ref.id)}`;

  return (
    <>
      <CheckoutStepper currentStep="details" />
      <section className="mx-auto w-full max-w-3xl px-6 py-8 md:px-10">
        <h1 className="text-2xl font-semibold">{kindLabel} details</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          We&apos;ve got the details from your booking. Ready to pay?
        </p>

        <div className="mt-8 flex flex-col gap-4 rounded-md border border-border p-6">
          <p className="text-sm text-muted-foreground">
            Reference: <code className="rounded bg-muted px-1.5 py-0.5 text-xs">{ref.kind}-{ref.id}</code>
          </p>
          <Link
            href={nextUrl}
            className="inline-flex self-start items-center rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
          >
            Continue to payment
          </Link>
        </div>
      </section>
    </>
  );
}
