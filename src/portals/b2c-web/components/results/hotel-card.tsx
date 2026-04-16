'use client';

// Hotel result card (HOTB-02, UI-SPEC §Hotel Card).
//
// Layout (desktop):
//   ┌───────────────┬─────────────────────────────────────────────┐
//   │  photo (1/3)  │  Property name (h3) + stars                 │
//   │               │  Address line (muted)                       │
//   │               │  Amenity chips · cancellation badge         │
//   │               │                          nightly + /night   │
//   │               │                          total + currency   │
//   └───────────────┴─────────────────────────────────────────────┘
//
// Cancellation badge copy is the VERBATIM UI-SPEC text so a grep for the
// exact sentence always hits the rendered source:
//   "Free cancellation" | "Non-refundable" | "Flexible"
//
// "/night" suffix is REQUIRED by the acceptance test (Plan 04-03 ac-crit).

import Image from 'next/image';
import { Star } from 'lucide-react';
import { formatMoney } from '@/lib/formatters';
import { cn } from '@/lib/utils';
import type { CancellationPolicy, HotelOffer } from '@/types/hotel';

export interface HotelCardProps {
  offer: HotelOffer;
  onSelect?: (offer: HotelOffer) => void;
  className?: string;
}

function cancellationLabel(policy: CancellationPolicy): string {
  switch (policy) {
    case 'free':
      // UI-SPEC verbatim — do not reword.
      return 'Free cancellation';
    case 'nonRefundable':
      // UI-SPEC verbatim — do not reword.
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

function Stars({ rating }: { rating: number }) {
  // Render 1-5 stars; clamp gracefully so a stray 6 from an adapter
  // never blows the layout.
  const filled = Math.max(0, Math.min(5, Math.round(rating)));
  return (
    <span
      aria-label={`${filled}-star property`}
      className="inline-flex items-center gap-0.5 text-amber-500"
      data-testid="hotel-stars"
    >
      {Array.from({ length: filled }).map((_, i) => (
        <Star key={i} size={14} className="fill-current" />
      ))}
    </span>
  );
}

export function HotelCard({ offer, onSelect, className }: HotelCardProps) {
  const nightlyLabel = formatMoney(offer.nightlyRate.amount, offer.nightlyRate.currency);
  const totalLabel = formatMoney(offer.totalAmount.amount, offer.totalAmount.currency);
  const primaryPhoto = offer.photos[0];

  return (
    <article
      data-offer-id={offer.offerId}
      className={cn(
        'grid gap-4 rounded-lg border border-border bg-background p-4 transition-shadow hover:shadow-md md:grid-cols-[1fr_2fr]',
        className,
      )}
    >
      <div className="relative aspect-[4/3] w-full overflow-hidden rounded-md bg-muted">
        {primaryPhoto ? (
          <Image
            src={primaryPhoto}
            alt={offer.name}
            fill
            sizes="(max-width: 768px) 100vw, 33vw"
            className="object-cover"
            // next/image blur placeholder omitted — backend may or may not
            // supply a blurDataURL per offer; a subtle bg-muted during
            // load is the UI-SPEC-approved fallback.
          />
        ) : (
          <div
            role="img"
            aria-label={offer.name}
            className="flex h-full w-full items-center justify-center text-xs text-muted-foreground"
          >
            No photo available
          </div>
        )}
      </div>

      <div className="flex flex-col gap-2">
        <div className="flex items-start justify-between gap-3">
          <div className="flex flex-col">
            <h3 className="text-base font-semibold leading-tight">{offer.name}</h3>
            <p className="text-xs text-muted-foreground">{offer.address}</p>
            <div className="mt-1">
              <Stars rating={offer.starRating} />
            </div>
          </div>
          <div className="text-end">
            <p className="text-xl font-semibold tabular-nums">
              {nightlyLabel}
              <span className="ms-1 text-xs font-normal text-muted-foreground">/night</span>
            </p>
            <p className="text-xs text-muted-foreground tabular-nums">{totalLabel} total</p>
          </div>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          {offer.amenities.slice(0, 3).map((a) => (
            <span
              key={a}
              className="inline-flex items-center rounded-full border border-border bg-muted/40 px-2 py-0.5 text-xs text-muted-foreground"
            >
              {a}
            </span>
          ))}
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
              View rooms
            </button>
          </div>
        )}
      </div>
    </article>
  );
}
