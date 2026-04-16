// /checkout/success — unified booking-confirmed landing (B5 / Plan 04-04).
//
// Reached ONLY via /checkout/processing's poll-driven redirect when the
// saga reports a terminal success state (Pitfall 6 / D-12). For baskets
// that terminal set includes `PartiallyConfirmed` (D-09), in which case
// we render the <PartialFailureBanner> above the success block.
//
// Reads the unified `?ref={kind}-{id}` contract (B5). For flights we
// display the PNR. For hotels/cars we display the vendor booking
// reference. For baskets we display BOTH the flight PNR and the hotel
// booking reference side-by-side (PKG-04 independent supplier refs).

import Link from 'next/link';
import { notFound, redirect } from 'next/navigation';
import { Download } from 'lucide-react';

import { auth } from '@/lib/auth';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';
import { buildCheckoutRef, parseCheckoutRef, type CheckoutRef } from '@/lib/checkout-ref';
import { PartialFailureBanner } from '@/components/trip-builder/partial-failure-banner';
import type { BookingDtoPublic } from '@/types/api';

export const dynamic = 'force-dynamic';
export const runtime = 'nodejs';

interface PageProps {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}

interface BasketDtoPublic {
  basketId: string;
  status: string;
  flightBookingId?: string | null;
  hotelBookingId?: string | null;
  carBookingId?: string | null;
  totalAmount: number;
  chargedAmount: number;
  refundedAmount: number;
  currency: string;
}

interface HotelBookingDto {
  id: string;
  status: string;
  bookingReference: string;
  supplierRef?: string | null;
}

interface CarBookingDto {
  id: string;
  status: string;
  bookingReference: string;
  supplierRef?: string | null;
  vendorName: string;
}

function firstParam(v: string | string[] | undefined): string | undefined {
  return Array.isArray(v) ? v[0] : v;
}

async function loadFlightBooking(id: string): Promise<BookingDtoPublic | null> {
  try {
    const res = await gatewayFetch(`/bookings/${encodeURIComponent(id)}`);
    if (res.status === 404) return null;
    if (!res.ok) return null;
    return (await res.json()) as BookingDtoPublic;
  } catch (err) {
    if (err instanceof UnauthenticatedError) throw err;
    return null;
  }
}

async function loadHotelBooking(id: string): Promise<HotelBookingDto | null> {
  try {
    const res = await gatewayFetch(`/hotel-bookings/${encodeURIComponent(id)}`);
    if (!res.ok) return null;
    return (await res.json()) as HotelBookingDto;
  } catch (err) {
    if (err instanceof UnauthenticatedError) throw err;
    return null;
  }
}

async function loadCarBooking(id: string): Promise<CarBookingDto | null> {
  try {
    const res = await gatewayFetch(`/car-bookings/${encodeURIComponent(id)}`);
    if (!res.ok) return null;
    return (await res.json()) as CarBookingDto;
  } catch (err) {
    if (err instanceof UnauthenticatedError) throw err;
    return null;
  }
}

async function loadBasket(id: string): Promise<BasketDtoPublic | null> {
  try {
    const res = await gatewayFetch(`/baskets/${encodeURIComponent(id)}`);
    if (!res.ok) return null;
    return (await res.json()) as BasketDtoPublic;
  } catch (err) {
    if (err instanceof UnauthenticatedError) throw err;
    return null;
  }
}

function loginCallback(ref: CheckoutRef): string {
  return `/login?callbackUrl=/checkout/success?ref=${encodeURIComponent(buildCheckoutRef(ref.kind, ref.id))}`;
}

export default async function CheckoutSuccessPage({ searchParams }: PageProps) {
  // Pitfall 11 — searchParams is a Promise in Next.js 16.
  const sp = await searchParams;
  const ref = parseCheckoutRef(firstParam(sp.ref));
  if (!ref) {
    redirect('/bookings');
  }

  const session = await auth();
  if (!session) {
    redirect(loginCallback(ref));
  }

  // Flight path — existing 04-02 shape.
  if (ref.kind === 'flight') {
    let booking: BookingDtoPublic | null = null;
    try {
      booking = await loadFlightBooking(ref.id);
    } catch (err) {
      if (err instanceof UnauthenticatedError) redirect(loginCallback(ref));
      throw err;
    }
    if (!booking) notFound();

    const reference = booking.pnr ?? booking.bookingReference;
    const receiptHref = `/api/bookings/${encodeURIComponent(booking.bookingId)}/receipt.pdf`;

    return (
      <section className="mx-auto flex w-full max-w-2xl flex-col items-center px-6 py-12 text-center md:px-10">
        <SuccessTick />
        <h1 className="mt-6 text-2xl font-semibold">
          Flight booked. Booking reference: <span className="font-mono">{reference}</span>.
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

  // Hotel path.
  if (ref.kind === 'hotel') {
    let hotel: HotelBookingDto | null = null;
    try {
      hotel = await loadHotelBooking(ref.id);
    } catch (err) {
      if (err instanceof UnauthenticatedError) redirect(loginCallback(ref));
      throw err;
    }
    if (!hotel) notFound();

    return (
      <section className="mx-auto flex w-full max-w-2xl flex-col items-center px-6 py-12 text-center md:px-10">
        <SuccessTick />
        <h1 className="mt-6 text-2xl font-semibold">
          Hotel booked. Booking reference: <span className="font-mono">{hotel.bookingReference}</span>.
        </h1>
        {hotel.supplierRef && (
          <p className="mt-2 text-sm text-muted-foreground">
            Supplier reference: <span className="font-mono">{hotel.supplierRef}</span>
          </p>
        )}
        <p className="mt-2 text-sm text-muted-foreground">
          Your hotel voucher will arrive by email within 60 seconds.
        </p>
      </section>
    );
  }

  // Car path.
  if (ref.kind === 'car') {
    let car: CarBookingDto | null = null;
    try {
      car = await loadCarBooking(ref.id);
    } catch (err) {
      if (err instanceof UnauthenticatedError) redirect(loginCallback(ref));
      throw err;
    }
    if (!car) notFound();

    return (
      <section className="mx-auto flex w-full max-w-2xl flex-col items-center px-6 py-12 text-center md:px-10">
        <SuccessTick />
        <h1 className="mt-6 text-2xl font-semibold">
          Car booked. Booking reference: <span className="font-mono">{car.bookingReference}</span>.
        </h1>
        {car.supplierRef && (
          <p className="mt-2 text-sm text-muted-foreground">
            Supplier reference ({car.vendorName}): <span className="font-mono">{car.supplierRef}</span>
          </p>
        )}
        <p className="mt-2 text-sm text-muted-foreground">
          Your car voucher will arrive by email within 60 seconds.
        </p>
      </section>
    );
  }

  // Basket path — PKG-04 independent refs + D-09 partial banner when needed.
  let basket: BasketDtoPublic | null = null;
  try {
    basket = await loadBasket(ref.id);
  } catch (err) {
    if (err instanceof UnauthenticatedError) redirect(loginCallback(ref));
    throw err;
  }
  if (!basket) notFound();

  const isPartial = basket.status === 'PartiallyConfirmed';

  // For a partial confirmation we still have the flight booking; its PNR
  // is the customer-visible reference to surface in the banner + success
  // block. We best-effort fetch the flight booking for the PNR string.
  let flightBooking: BookingDtoPublic | null = null;
  if (basket.flightBookingId) {
    try {
      flightBooking = await loadFlightBooking(basket.flightBookingId);
    } catch {
      // swallow — the success page still renders even if the flight
      // booking fetch fails (email has all details).
    }
  }
  const flightRef = flightBooking?.pnr ?? flightBooking?.bookingReference ?? basket.flightBookingId ?? '';

  let hotelBooking: HotelBookingDto | null = null;
  if (basket.hotelBookingId && !isPartial) {
    try {
      hotelBooking = await loadHotelBooking(basket.hotelBookingId);
    } catch {
      // best-effort
    }
  }

  return (
    <section className="mx-auto flex w-full max-w-2xl flex-col items-stretch gap-6 px-6 py-12 md:px-10">
      {isPartial && (
        <PartialFailureBanner flightReference={flightRef || '(pending)'} />
      )}
      <div className="flex flex-col items-center text-center">
        <SuccessTick />
        <h1 className="mt-6 text-2xl font-semibold">
          {isPartial ? 'Trip partially confirmed.' : 'Trip confirmed.'}
        </h1>
        <p className="mt-2 text-sm text-muted-foreground">
          Your confirmation email covers every component in one place.
        </p>
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <div className="rounded-md border border-border p-4">
          <p className="text-xs text-muted-foreground">Flight</p>
          <p className="mt-1 text-sm font-semibold">
            Reference: <span className="font-mono">{flightRef || '—'}</span>
          </p>
        </div>
        {!isPartial && hotelBooking ? (
          <div className="rounded-md border border-border p-4">
            <p className="text-xs text-muted-foreground">Hotel</p>
            <p className="mt-1 text-sm font-semibold">
              Reference: <span className="font-mono">{hotelBooking.bookingReference}</span>
            </p>
            {hotelBooking.supplierRef && (
              <p className="mt-1 text-xs text-muted-foreground">
                Supplier: <span className="font-mono">{hotelBooking.supplierRef}</span>
              </p>
            )}
          </div>
        ) : (
          <div className="rounded-md border border-dashed border-border p-4 text-sm text-muted-foreground">
            {isPartial ? 'Hotel was not confirmed.' : 'Hotel reference will appear here shortly.'}
          </div>
        )}
      </div>

      <div className="flex justify-center">
        <Link
          href="/bookings"
          className="inline-flex items-center justify-center rounded-md border border-border px-5 py-2.5 text-sm font-medium hover:bg-muted"
        >
          View my bookings
        </Link>
      </div>
    </section>
  );
}

function SuccessTick() {
  return (
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
  );
}
