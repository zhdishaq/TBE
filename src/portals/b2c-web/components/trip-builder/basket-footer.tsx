'use client';

// Trip Builder sticky basket footer (Plan 04-04 / PKG-01..04 / D-07).
//
// Renders the two line items (flight + hotel), the total, TWO
// INDEPENDENT cancellation policy labels side-by-side (PKG-04 —
// test-asserted; never merge the two strings), and the "Continue to
// checkout" CTA that creates the server basket and pushes to
// /checkout/details?ref=basket-{id} via the unified B5 contract.
//
// Plain strings "Flight cancellation" and "Hotel cancellation" are
// required by the acceptance-criteria greps.

import { useRouter } from 'next/navigation';
import { useState } from 'react';
import { ArrowRight } from 'lucide-react';
import { useBasket } from '@/hooks/use-basket';
import { buildCheckoutRef } from '@/lib/checkout-ref';
import { formatMoney } from '@/lib/formatters';
import type { CancellationPolicy } from '@/types/hotel';

function cancellationLabel(policy: CancellationPolicy): string {
  if (policy === 'free') return 'Free cancellation';
  if (policy === 'nonRefundable') return 'Non-refundable';
  return 'Flexible';
}

export interface BasketFooterProps {
  /** Guest details to forward when materialising the server basket. */
  guest?: { fullName: string; email: string; phoneNumber?: string };
  className?: string;
}

export function BasketFooter({ guest, className }: BasketFooterProps) {
  const router = useRouter();
  const { flight, hotel, totalAmount, currency, createServerBasket } = useBasket();
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const canCheckout = Boolean(flight && hotel);

  async function onContinue() {
    if (!canCheckout || submitting) return;
    setSubmitting(true);
    setError(null);
    try {
      const basketId = await createServerBasket(
        guest ?? {
          fullName: 'Guest Traveller',
          email: 'guest@example.com',
        },
      );
      router.push(`/checkout/details?ref=${buildCheckoutRef('basket', basketId)}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not start checkout.');
      setSubmitting(false);
    }
  }

  const flightPolicyLabel = flight ? cancellationLabel(flight.cancellationPolicy) : '—';
  const hotelPolicyLabel = hotel ? cancellationLabel(hotel.cancellationPolicy) : '—';

  return (
    <aside
      className={[
        'sticky bottom-0 z-10 border-t border-border bg-background/95 px-4 py-3 shadow-lg backdrop-blur',
        className,
      ].filter(Boolean).join(' ')}
      aria-label="Trip basket"
    >
      <div className="mx-auto flex w-full max-w-6xl flex-col gap-3 md:flex-row md:items-center md:justify-between">
        <div className="flex flex-col gap-1 text-sm">
          {flight && (
            <p className="tabular-nums">
              <span className="text-muted-foreground">Flight:</span>{' '}
              <span>{flight.summary}</span>{' '}
              <span className="text-muted-foreground">
                · {formatMoney(flight.amount.amount, flight.amount.currency)}
              </span>
            </p>
          )}
          {hotel && (
            <p className="tabular-nums">
              <span className="text-muted-foreground">Hotel:</span>{' '}
              <span>{hotel.summary}</span>{' '}
              <span className="text-muted-foreground">
                · {formatMoney(hotel.amount.amount, hotel.amount.currency)}
              </span>
            </p>
          )}
          <div className="mt-1 flex flex-col gap-0.5 md:flex-row md:gap-4">
            <span
              data-testid="flight-cancellation"
              className="text-xs text-muted-foreground"
            >
              Flight cancellation: {flightPolicyLabel}
            </span>
            <span
              data-testid="hotel-cancellation"
              className="text-xs text-muted-foreground"
            >
              Hotel cancellation: {hotelPolicyLabel}
            </span>
          </div>
          {/* Single screen-reader announcement covering both PKG-04 policies. */}
          <span className="sr-only" data-testid="basket-cancellation-sr">
            Flight cancellation policy: {flightPolicyLabel}. Hotel cancellation policy: {hotelPolicyLabel}.
          </span>
        </div>

        <div className="flex items-center gap-4">
          <div className="text-end">
            <p className="text-xs text-muted-foreground">Total</p>
            <p data-testid="basket-total" className="text-lg font-semibold tabular-nums">
              {formatMoney(totalAmount, currency)}
            </p>
          </div>
          <button
            type="button"
            onClick={onContinue}
            disabled={!canCheckout || submitting}
            className="inline-flex items-center gap-1.5 rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
          >
            {submitting ? 'Starting checkout…' : 'Continue to checkout'}
            <ArrowRight size={14} className="ms-1" />
          </button>
        </div>
      </div>
      {error && (
        <p role="alert" className="mx-auto mt-2 max-w-6xl text-xs text-red-600">
          {error}
        </p>
      )}
    </aside>
  );
}
