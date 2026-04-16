// /hotels/[offerId] — hotel detail + room picker (HOTB-02/03).
//
// RSC loads the offer + available rooms from the gateway, renders a
// photo gallery + amenity list + room list. "Book room" CTA is a
// client island (`BookRoomButton`) which POSTs to `/api/hotel-bookings`
// and then router.push-es to the shared checkout flow from 04-02.
//
// Pitfall 11: `params` is a Promise in Next.js 16 dynamic routes; we
// await it before use.
// Pitfall 15: `dynamic = "force-dynamic"` so the detail doesn't stale
// serve across users (price tiers + availability vary per session).

import Image from 'next/image';
import { Star } from 'lucide-react';
import type { Metadata } from 'next';
import { BookRoomButton } from '@/components/hotel/book-room-button';
import { formatMoney } from '@/lib/formatters';
import type { HotelOffer } from '@/types/hotel';

export const metadata: Metadata = {
  title: 'Hotel details',
};

export const dynamic = 'force-dynamic';

async function loadHotelOffer(offerId: string): Promise<HotelOffer | null> {
  const gateway = process.env.GATEWAY_URL;
  if (!gateway) return null;
  try {
    const r = await fetch(
      `${gateway}/hotels/offers/${encodeURIComponent(offerId)}`,
      { cache: 'no-store' },
    );
    if (!r.ok) return null;
    return (await r.json()) as HotelOffer;
  } catch {
    return null;
  }
}

function cancellationText(policy: HotelOffer['cancellationPolicy']): string {
  if (policy === 'free') return 'Free cancellation';
  if (policy === 'nonRefundable') return 'Non-refundable';
  return 'Flexible';
}

export default async function HotelDetailPage({
  params,
  searchParams,
}: {
  params: Promise<{ offerId: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const { offerId } = await params;
  const sp = await searchParams;

  const checkin = Array.isArray(sp.checkin) ? sp.checkin[0] : sp.checkin;
  const checkout = Array.isArray(sp.checkout) ? sp.checkout[0] : sp.checkout;
  const roomsParam = Array.isArray(sp.rooms) ? sp.rooms[0] : sp.rooms;
  const adultsParam = Array.isArray(sp.adults) ? sp.adults[0] : sp.adults;
  const childrenParam = Array.isArray(sp.children) ? sp.children[0] : sp.children;

  const offer = await loadHotelOffer(offerId);

  if (!offer) {
    return (
      <main className="flex w-full flex-col px-6 py-8 md:px-10 lg:px-20">
        <div className="mx-auto w-full max-w-3xl">
          <h1 className="text-2xl font-semibold">Hotel details</h1>
          <p className="mt-2 text-sm text-muted-foreground">Offer ID: {offerId}</p>
          <div className="mt-6 rounded-md border border-dashed border-border p-6 text-sm text-muted-foreground">
            We couldn&apos;t load this hotel. It may have sold out, or the
            offer may have expired. Try searching again from{' '}
            <a className="underline" href="/hotels">/hotels</a>.
          </div>
        </div>
      </main>
    );
  }

  const primaryPhoto = offer.photos[0];
  const leadRoom = offer.rooms[0];

  return (
    <main className="flex w-full flex-col px-6 py-8 md:px-10 lg:px-20">
      <div className="mx-auto w-full max-w-5xl">
        <header className="flex flex-col gap-2 md:flex-row md:items-start md:justify-between">
          <div className="flex flex-col gap-1">
            <h1 className="text-2xl font-semibold leading-tight">{offer.name}</h1>
            <p className="text-sm text-muted-foreground">{offer.address}</p>
            <span
              aria-label={`${Math.round(offer.starRating)}-star property`}
              className="inline-flex items-center gap-0.5 text-amber-500"
            >
              {Array.from({ length: Math.max(0, Math.min(5, Math.round(offer.starRating))) }).map((_, i) => (
                <Star key={i} size={14} className="fill-current" />
              ))}
            </span>
          </div>
          <div className="text-end">
            <p className="text-xl font-semibold tabular-nums">
              {formatMoney(offer.totalAmount.amount, offer.totalAmount.currency)}
              <span className="ms-1 text-xs font-normal text-muted-foreground">total</span>
            </p>
            <p className="text-xs text-muted-foreground">
              {cancellationText(offer.cancellationPolicy)}
            </p>
          </div>
        </header>

        <div className="mt-6 grid gap-4 md:grid-cols-3">
          <div className="relative aspect-[4/3] overflow-hidden rounded-md bg-muted md:col-span-2">
            {primaryPhoto ? (
              <Image
                src={primaryPhoto}
                alt={offer.name}
                fill
                sizes="(max-width: 768px) 100vw, 66vw"
                className="object-cover"
              />
            ) : (
              <div className="flex h-full w-full items-center justify-center text-sm text-muted-foreground">
                No photo available
              </div>
            )}
          </div>
          <ul className="grid grid-cols-2 gap-2 md:grid-cols-1">
            {offer.photos.slice(1, 5).map((p, idx) => (
              <li key={`${p}-${idx}`} className="relative aspect-[4/3] overflow-hidden rounded-md bg-muted">
                <Image
                  src={p}
                  alt={`${offer.name} photo ${idx + 2}`}
                  fill
                  sizes="(max-width: 768px) 50vw, 33vw"
                  className="object-cover"
                />
              </li>
            ))}
          </ul>
        </div>

        <section className="mt-8">
          <h2 className="text-sm font-semibold">Amenities</h2>
          <ul className="mt-2 flex flex-wrap gap-2">
            {offer.amenities.map((a) => (
              <li
                key={a}
                className="rounded-full border border-border bg-muted/40 px-2 py-0.5 text-xs text-muted-foreground"
              >
                {a}
              </li>
            ))}
          </ul>
        </section>

        <section className="mt-8">
          <h2 className="text-lg font-semibold">Available rooms</h2>
          <ul className="mt-3 flex flex-col gap-3">
            {offer.rooms.map((room) => (
              <li
                key={room.roomId}
                className="flex flex-col gap-2 rounded-lg border border-border p-4 md:flex-row md:items-center md:justify-between"
                data-room-id={room.roomId}
              >
                <div className="flex flex-col">
                  <p className="text-sm font-semibold">{room.name}</p>
                  {room.bedType && (
                    <p className="text-xs text-muted-foreground">{room.bedType}</p>
                  )}
                  {room.description && (
                    <p className="text-xs text-muted-foreground">{room.description}</p>
                  )}
                  <p className="text-xs text-muted-foreground">
                    Sleeps up to {room.maxOccupancy} · {cancellationText(room.cancellationPolicy)}
                  </p>
                </div>
                <div className="flex items-center gap-4">
                  <div className="text-end">
                    <p className="text-sm font-semibold tabular-nums">
                      {formatMoney(room.nightlyRate.amount, room.nightlyRate.currency)}
                      <span className="ms-1 text-xs font-normal text-muted-foreground">/night</span>
                    </p>
                    <p className="text-xs text-muted-foreground tabular-nums">
                      {formatMoney(room.totalAmount.amount, room.totalAmount.currency)} total
                    </p>
                  </div>
                  <BookRoomButton
                    offerId={offer.offerId}
                    roomId={room.roomId}
                    checkinIso={checkin}
                    checkoutIso={checkout}
                    rooms={roomsParam ? Number(roomsParam) : 1}
                    adults={adultsParam ? Number(adultsParam) : 2}
                    children={childrenParam ? Number(childrenParam) : 0}
                  />
                </div>
              </li>
            ))}
            {offer.rooms.length === 0 && leadRoom === undefined && (
              <li className="rounded-md border border-dashed border-border p-4 text-sm text-muted-foreground">
                No rooms available for the selected dates. Try different dates.
              </li>
            )}
          </ul>
        </section>
      </div>
    </main>
  );
}
