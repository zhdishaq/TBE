'use client';

// Hotel "Book room" CTA (HOTB-03).
//
// Client component fired from the RSC detail page. Posts to
// `/api/hotel-bookings` (thin pass-through to BookingService) which
// returns `{ bookingId }`; we then router.push to the shared checkout
// pipeline that 04-02 already built.
//
// The checkout/details page from 04-02 keys off `offerId` for flights.
// For hotels we pass `hotelBookingId` so the details page can branch
// (04-04 will unify both under a single `basketId` for Trip Builder).
//
// IMPORTANT: "Book room" is the exact button label asserted by the E2E
// spec. Do not reword.

import { useRouter } from 'next/navigation';
import { useState } from 'react';

export interface BookRoomButtonProps {
  offerId: string;
  roomId?: string;
  checkinIso?: string;
  checkoutIso?: string;
  rooms?: number;
  adults?: number;
  children?: number;
  guest?: { fullName: string; email: string; phoneNumber?: string };
  className?: string;
}

export function BookRoomButton({
  offerId,
  roomId,
  checkinIso,
  checkoutIso,
  rooms = 1,
  adults = 2,
  children = 0,
  guest,
  className,
}: BookRoomButtonProps) {
  const router = useRouter();
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleClick() {
    if (pending) return;
    setPending(true);
    setError(null);
    try {
      const resp = await fetch('/api/hotel-bookings', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          offerId,
          roomId,
          checkInDate: checkinIso,
          checkOutDate: checkoutIso,
          rooms,
          adults,
          children,
          guest,
        }),
      });
      if (!resp.ok) {
        if (resp.status === 401) {
          router.push(`/login?next=${encodeURIComponent(`/hotels/${offerId}`)}`);
          return;
        }
        throw new Error(`Booking failed: ${resp.status}`);
      }
      const body = (await resp.json()) as { bookingId?: string };
      if (!body.bookingId) throw new Error('Booking response missing bookingId');
      router.push(`/checkout/details?hotelBookingId=${encodeURIComponent(body.bookingId)}`);
    } catch (err) {
      setError((err as Error).message);
      setPending(false);
    }
  }

  return (
    <div className={['flex flex-col gap-1', className].filter(Boolean).join(' ')}>
      <button
        type="button"
        onClick={handleClick}
        disabled={pending}
        className="inline-flex items-center gap-1.5 rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-60"
      >
        {pending ? 'Reserving…' : 'Book room'}
      </button>
      {error && (
        <p role="alert" className="text-xs text-red-600">
          {error}
        </p>
      )}
    </div>
  );
}
