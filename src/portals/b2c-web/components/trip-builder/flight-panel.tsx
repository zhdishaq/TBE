'use client';

// Trip Builder flight panel (Plan 04-04 / PKG-01 / D-07).
//
// Minimal capstone scope: either shows the current basket's flight as a
// summary card with a "Remove" button, or a call-to-action linking out to
// /flights to pick one. A full in-place flight search would swap the CTA
// for an embedded <FlightSearchForm mode="embedded" /> + results list,
// but the capstone only requires that the Trip Builder lets the user
// ARRIVE with a selected flight and compose a hotel alongside it.

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

export function FlightPanel() {
  const { flight, removeFlight } = useBasket();

  return (
    <section className="flex flex-col gap-3 rounded-lg border border-border bg-background p-4">
      <header className="flex items-baseline justify-between">
        <h2 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">
          Flight
        </h2>
        {flight && (
          <button
            type="button"
            onClick={removeFlight}
            className="inline-flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground"
          >
            <X size={12} /> Remove
          </button>
        )}
      </header>
      {flight ? (
        <div className="flex flex-col gap-1">
          <p className="text-base font-semibold">{flight.summary}</p>
          <p className="text-sm text-muted-foreground tabular-nums">
            {formatMoney(flight.amount.amount, flight.amount.currency)}
          </p>
          <p className="text-xs text-muted-foreground">
            {cancellationLabel(flight.cancellationPolicy)}
          </p>
        </div>
      ) : (
        <div className="flex flex-col gap-2 rounded-md border border-dashed border-border p-6 text-sm text-muted-foreground">
          <p>No flight yet. Pick one to start your trip.</p>
          <Link
            href="/flights"
            className="inline-flex items-center gap-1 self-start text-sm font-medium text-blue-600 hover:underline"
          >
            Search flights <ArrowRight size={12} />
          </Link>
        </div>
      )}
    </section>
  );
}
