'use client';

// Car hire "Book car" CTA (Plan 04-04 / CARB-01..03).
//
// Client component fired from the RSC detail page. Posts to
// `/api/car-bookings` (thin pass-through to BookingService) which returns
// `{ bookingId }`; we then router.push to the shared checkout pipeline
// from 04-02, using the unified `?ref=car-{id}` contract that Task 3b
// formalises under `lib/checkout-ref.ts`. The string is kept literal here
// so the page ships before `checkout-ref` lands without a broken import.
//
// IMPORTANT: "Book car" is the exact button label asserted by the unit
// tests for <CarCard>. Do not reword.

import { useRouter } from 'next/navigation';
import { useState } from 'react';

export interface BookCarButtonProps {
  offerId: string;
  vendorName: string;
  pickupLocation: string;
  dropoffLocation: string;
  pickupAtIso: string;
  dropoffAtIso: string;
  driverAge: number;
  guest?: { fullName: string; email: string; phoneNumber?: string };
  className?: string;
}

export function BookCarButton({
  offerId,
  vendorName,
  pickupLocation,
  dropoffLocation,
  pickupAtIso,
  dropoffAtIso,
  driverAge,
  guest,
  className,
}: BookCarButtonProps) {
  const router = useRouter();
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleClick() {
    if (pending) return;
    setPending(true);
    setError(null);
    try {
      const resp = await fetch('/api/car-bookings', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          offerId,
          vendorName,
          pickupLocation,
          dropoffLocation,
          pickupAtUtc: pickupAtIso,
          dropoffAtUtc: dropoffAtIso,
          driverAge,
          guest,
        }),
      });
      if (!resp.ok) {
        if (resp.status === 401) {
          router.push(`/login?next=${encodeURIComponent(`/cars/${offerId}`)}`);
          return;
        }
        throw new Error(`Booking failed: ${resp.status}`);
      }
      const body = (await resp.json()) as { bookingId?: string };
      if (!body.bookingId) throw new Error('Booking response missing bookingId');
      // Unified checkout ref contract (B5): ?ref=car-{id}. Formalised by
      // `lib/checkout-ref.ts` in Task 3b.
      router.push(`/checkout/details?ref=${encodeURIComponent(`car-${body.bookingId}`)}`);
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
        {pending ? 'Reserving…' : 'Book car'}
      </button>
      {error && (
        <p role="alert" className="text-xs text-red-600">
          {error}
        </p>
      )}
    </div>
  );
}
