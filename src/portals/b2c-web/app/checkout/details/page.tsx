// /checkout/details — passenger info entry (B2C-05 step 4).
//
// Reads offerId + passenger counts from searchParams, renders the RHF
// form. Submits to /api/bookings which creates a pending booking; on
// success the form pushes to /checkout/payment?booking={id}.
//
// Pitfall 11: `searchParams` is a Promise in Next.js 16 — await it.

import { redirect } from 'next/navigation';

import { PassengerDetailsForm } from '@/components/checkout/passenger-details-form';
import { CheckoutStepper } from '@/components/checkout/stepper';

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

export default async function CheckoutDetailsPage({ searchParams }: PageProps) {
  const sp = await searchParams;
  const offerId = Array.isArray(sp.offerId) ? sp.offerId[0] : sp.offerId;
  if (!offerId) {
    redirect('/flights');
  }

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
            offerId={offerId}
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
