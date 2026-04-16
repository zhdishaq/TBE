// /cars/[offerId] — car hire offer detail page (Plan 04-04 / CARB-02).
//
// RSC loads the offer from the gateway and renders a summary + "Book car"
// CTA (client island). Mirrors /hotels/[offerId] structurally so the three
// product detail pages feel symmetrical.
//
// Pitfall 11: `params` is a Promise in Next.js 16 dynamic routes; we
// await it before use.
// Pitfall 15: `dynamic = "force-dynamic"` so the detail doesn't
// stale-serve across users (price tiers + availability vary per
// session).

import Image from 'next/image';
import type { Metadata } from 'next';
import { Users, Luggage, Cog } from 'lucide-react';
import { BookCarButton } from '@/components/car/book-car-button';
import { formatMoney } from '@/lib/formatters';
import type { CarOffer } from '@/types/car';

export const metadata: Metadata = {
  title: 'Car hire details',
};

export const dynamic = 'force-dynamic';

async function loadCarOffer(offerId: string): Promise<CarOffer | null> {
  const gateway = process.env.GATEWAY_URL;
  if (!gateway) return null;
  try {
    const r = await fetch(
      `${gateway}/cars/offers/${encodeURIComponent(offerId)}`,
      { cache: 'no-store' },
    );
    if (!r.ok) return null;
    return (await r.json()) as CarOffer;
  } catch {
    return null;
  }
}

function cancellationText(policy: CarOffer['cancellationPolicy']): string {
  if (policy === 'free') return 'Free cancellation';
  if (policy === 'nonRefundable') return 'Non-refundable';
  return 'Flexible';
}

export default async function CarDetailPage({
  params,
  searchParams,
}: {
  params: Promise<{ offerId: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const { offerId } = await params;
  const sp = await searchParams;
  const driverAgeParam = Array.isArray(sp.driverAge) ? sp.driverAge[0] : sp.driverAge;

  const offer = await loadCarOffer(offerId);

  if (!offer) {
    return (
      <main className="flex w-full flex-col px-6 py-8 md:px-10 lg:px-20">
        <div className="mx-auto w-full max-w-3xl">
          <h1 className="text-2xl font-semibold">Car hire details</h1>
          <p className="mt-2 text-sm text-muted-foreground">Offer ID: {offerId}</p>
          <div className="mt-6 rounded-md border border-dashed border-border p-6 text-sm text-muted-foreground">
            We couldn&apos;t load this car offer. It may have sold out, or the
            offer may have expired. Try searching again from{' '}
            <a className="underline" href="/cars">/cars</a>.
          </div>
        </div>
      </main>
    );
  }

  const transmissionLabel = offer.transmission === 'automatic' ? 'Automatic' : 'Manual';

  return (
    <main className="flex w-full flex-col px-6 py-8 md:px-10 lg:px-20">
      <div className="mx-auto w-full max-w-5xl">
        <header className="flex flex-col gap-2 md:flex-row md:items-start md:justify-between">
          <div className="flex flex-col gap-1">
            <h1 className="text-2xl font-semibold leading-tight">
              {offer.vendorName}
              <span className="ms-2 text-base font-normal text-muted-foreground">· {offer.category}</span>
            </h1>
            <p className="text-sm text-muted-foreground">Pick-up: {offer.pickupLocation}</p>
            <p className="text-sm text-muted-foreground">Drop-off: {offer.dropoffLocation}</p>
          </div>
          <div className="text-end">
            <p className="text-xl font-semibold tabular-nums">
              {formatMoney(offer.dailyRate.amount, offer.dailyRate.currency)}
              <span className="ms-1 text-xs font-normal text-muted-foreground">/day</span>
            </p>
            <p className="text-xs text-muted-foreground tabular-nums">
              {formatMoney(offer.totalAmount.amount, offer.totalAmount.currency)} total
            </p>
            <p className="text-xs text-muted-foreground">{cancellationText(offer.cancellationPolicy)}</p>
          </div>
        </header>

        <div className="mt-6 grid gap-4 md:grid-cols-3">
          <div className="relative aspect-[4/3] overflow-hidden rounded-md bg-muted md:col-span-2">
            {offer.vendorLogo ? (
              <Image
                src={offer.vendorLogo}
                alt={offer.vendorName}
                fill
                sizes="(max-width: 768px) 100vw, 66vw"
                className="object-contain p-8"
              />
            ) : (
              <div className="flex h-full w-full items-center justify-center text-sm text-muted-foreground">
                No image available
              </div>
            )}
          </div>

          <ul className="grid grid-cols-1 gap-2">
            <li className="flex items-center gap-2 rounded-md border border-border px-3 py-2 text-sm">
              <Cog size={14} className="text-muted-foreground" /> {transmissionLabel}
            </li>
            <li className="flex items-center gap-2 rounded-md border border-border px-3 py-2 text-sm">
              <Users size={14} className="text-muted-foreground" /> Seats {offer.seats}
            </li>
            <li className="flex items-center gap-2 rounded-md border border-border px-3 py-2 text-sm">
              <Luggage size={14} className="text-muted-foreground" /> Bags {offer.bags}
            </li>
          </ul>
        </div>

        <section className="mt-8 flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <div>
            <p className="text-sm text-muted-foreground">
              Pickup: {new Date(offer.pickupAt).toLocaleString()}
            </p>
            <p className="text-sm text-muted-foreground">
              Drop-off: {new Date(offer.dropoffAt).toLocaleString()}
            </p>
          </div>
          <BookCarButton
            offerId={offer.offerId}
            vendorName={offer.vendorName}
            pickupLocation={offer.pickupLocation}
            dropoffLocation={offer.dropoffLocation}
            pickupAtIso={offer.pickupAt}
            dropoffAtIso={offer.dropoffAt}
            driverAge={driverAgeParam ? Number(driverAgeParam) : 30}
          />
        </section>
      </div>
    </main>
  );
}
