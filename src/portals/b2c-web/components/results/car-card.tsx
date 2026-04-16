'use client';

// Car hire result card (CARB-01 / UI-SPEC §Car Card).
//
// Layout (desktop):
//   ┌───────────────┬─────────────────────────────────────────────┐
//   │  vendor logo  │  Vendor name · Category                      │
//   │               │  Transmission chip · seats · bags · pickup   │
//   │               │                          daily rate /day     │
//   │               │                          total + currency    │
//   └───────────────┴─────────────────────────────────────────────┘
//
// Cancellation badge copy reuses the UI-SPEC strings from hotel-card so a grep for the
// exact sentence hits the rendered source:
//   "Free cancellation" | "Non-refundable" | "Flexible"
//
// "/day" suffix mirrors hotel-card's "/night" convention and is REQUIRED by the
// acceptance test.

import Image from 'next/image';
import { Users, Luggage, Cog } from 'lucide-react';
import { formatMoney } from '@/lib/formatters';
import { cn } from '@/lib/utils';
import type { CarOffer } from '@/types/car';
import type { CancellationPolicy } from '@/types/hotel';

export interface CarCardProps {
  offer: CarOffer;
  onSelect?: (offer: CarOffer) => void;
  className?: string;
}

function cancellationLabel(policy: CancellationPolicy): string {
  switch (policy) {
    case 'free':
      return 'Free cancellation';
    case 'nonRefundable':
      return 'Non-refundable';
    case 'flexible':
    default:
      return 'Flexible';
  }
}

function cancellationBadgeClass(policy: CancellationPolicy): string {
  if (policy === 'free') return 'bg-green-50 text-green-800 border-green-200';
  if (policy === 'nonRefundable') return 'bg-amber-50 text-amber-800 border-amber-200';
  return 'bg-zinc-50 text-zinc-800 border-zinc-200';
}

export function CarCard({ offer, onSelect, className }: CarCardProps) {
  const dailyLabel = formatMoney(offer.dailyRate.amount, offer.dailyRate.currency);
  const totalLabel = formatMoney(offer.totalAmount.amount, offer.totalAmount.currency);
  const transmissionLabel = offer.transmission === 'automatic' ? 'Automatic' : 'Manual';

  return (
    <article
      data-offer-id={offer.offerId}
      className={cn(
        'grid gap-4 rounded-lg border border-border bg-background p-4 transition-shadow hover:shadow-md md:grid-cols-[1fr_2fr]',
        className,
      )}
    >
      <div className="relative aspect-[4/3] w-full overflow-hidden rounded-md bg-muted">
        {offer.vendorLogo ? (
          <Image
            src={offer.vendorLogo}
            alt={offer.vendorName}
            fill
            sizes="(max-width: 768px) 100vw, 33vw"
            className="object-contain p-4"
          />
        ) : (
          <div
            role="img"
            aria-label={offer.vendorName}
            className="flex h-full w-full items-center justify-center text-xs text-muted-foreground"
          >
            {offer.vendorName}
          </div>
        )}
      </div>

      <div className="flex flex-col gap-2">
        <div className="flex items-start justify-between gap-3">
          <div className="flex flex-col">
            <h3 className="text-base font-semibold leading-tight">
              {offer.vendorName}
              <span className="ms-2 text-sm font-normal text-muted-foreground">· {offer.category}</span>
            </h3>
            <p className="text-xs text-muted-foreground">Pick-up: {offer.pickupLocation}</p>
          </div>
          <div className="text-end">
            <p className="text-xl font-semibold tabular-nums">
              {dailyLabel}
              <span className="ms-1 text-xs font-normal text-muted-foreground">/day</span>
            </p>
            <p className="text-xs text-muted-foreground tabular-nums">{totalLabel} total</p>
          </div>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <span
            className="inline-flex items-center gap-1 rounded-full border border-border bg-muted/40 px-2 py-0.5 text-xs text-muted-foreground"
            data-testid="transmission-chip"
          >
            <Cog size={12} /> {transmissionLabel}
          </span>
          <span
            className="inline-flex items-center gap-1 rounded-full border border-border bg-muted/40 px-2 py-0.5 text-xs text-muted-foreground"
            data-testid="seats-chip"
          >
            <Users size={12} /> {offer.seats} seats
          </span>
          <span
            className="inline-flex items-center gap-1 rounded-full border border-border bg-muted/40 px-2 py-0.5 text-xs text-muted-foreground"
            data-testid="bags-chip"
          >
            <Luggage size={12} /> {offer.bags} bags
          </span>
          <span
            className={cn(
              'inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-medium',
              cancellationBadgeClass(offer.cancellationPolicy),
            )}
            data-testid="cancellation-badge"
          >
            {cancellationLabel(offer.cancellationPolicy)}
          </span>
        </div>

        {onSelect && (
          <div className="mt-auto flex justify-end pt-2">
            <button
              type="button"
              onClick={() => onSelect(offer)}
              className="inline-flex items-center gap-1.5 rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
            >
              Book car
            </button>
          </div>
        )}
      </div>
    </article>
  );
}
