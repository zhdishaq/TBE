'use client';

// Trip Builder hotel panel (Plan 04-04 / PKG-01 / D-07).
//
// Symmetric to FlightPanel — either shows the selected hotel line item
// or a CTA to /hotels for in-place search. PKG-04: cancellation policy
// is rendered here (card level) AND in the basket footer side-by-side
// with the flight policy, never merged.

import Link from 'next/link';
import { ArrowRight, X } from 'lucide-react';
import { formatMoney } from '@/lib/formatters';
import { useBasket } from '@/hooks/use-basket';

function cancellationLabel(policy: 'free' | 'nonRefundable' | 'flexible' | undefined): string {
  if (policy === 'free') return 'Free cancellation';
  if (policy === 'nonRefundable') return 'Non-refundable';
  if (policy === 'flexible') return 'Flexible';
  return 'Flexible';
}

export function HotelPanel() {
  const { hotel, removeHotel } = useBasket();

  return (
    <section className="flex flex-col gap-3 rounded-lg border border-border bg-background p-4">
      <header className="flex items-baseline justify-between">
        <h2 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">
          Hotel
        </h2>
        {hotel && (
          <button
            type="button"
            onClick={removeHotel}
            className="inline-flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground"
          >
            <X size={12} /> Remove
          </button>
        )}
      </header>
      {hotel ? (
        <div className="flex flex-col gap-1">
          <p className="text-base font-semibold">{hotel.summary}</p>
          <p className="text-sm text-muted-foreground tabular-nums">
            {formatMoney(hotel.amount.amount, hotel.amount.currency)}
          </p>
          <p className="text-xs text-muted-foreground">
            {cancellationLabel(hotel.cancellationPolicy)}
          </p>
        </div>
      ) : (
        <div className="flex flex-col gap-2 rounded-md border border-dashed border-border p-6 text-sm text-muted-foreground">
          <p>Add a hotel to your trip to unlock the combined checkout.</p>
          <Link
            href="/hotels"
            className="inline-flex items-center gap-1 self-start text-sm font-medium text-blue-600 hover:underline"
          >
            Search hotels <ArrowRight size={12} />
          </Link>
        </div>
      )}
    </section>
  );
}
