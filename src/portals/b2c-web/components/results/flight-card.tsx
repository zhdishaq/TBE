'use client';

// Flight result card (B2C-04, FLTB-03, UI-SPEC §Flight Card).
//
// Layout (stacked):
//   airline logo (24px) · name                    all-in price  (right-aligned)
//   HH:MM IATA ─ duration · stops ─ HH:MM IATA    "incl. taxes" label
//   baggage icon + allowance · expand-for-fare-breakdown button
//
// Expanded section: base / YQ-YR / taxes rows (FLTB-03) — we ALWAYS show
// these separately per UI-SPEC "taxes surfaced, not buried".
//
// Selected state (for the /flights/[offerId] drawer context) adds a
// 4px blue-600 left border + ring-1.

import { useState } from 'react';
import { ChevronDown, ChevronUp, Luggage, Plane } from 'lucide-react';
import { cn } from '@/lib/utils';
import type { FlightOffer } from '@/hooks/use-flight-search';

export interface FlightCardProps {
  offer: FlightOffer;
  selected?: boolean;
  onSelect?: (offer: FlightOffer) => void;
  className?: string;
}

function formatMoney(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat(undefined, {
      style: 'currency',
      currency,
      maximumFractionDigits: 0,
    }).format(amount);
  } catch {
    return `${currency} ${amount.toFixed(0)}`;
  }
}

function formatTime(iso: string): string {
  const d = new Date(iso);
  return d.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
}

function formatDuration(mins: number): string {
  const h = Math.floor(mins / 60);
  const m = mins % 60;
  return m === 0 ? `${h}h` : `${h}h ${m}m`;
}

export function FlightCard({ offer, selected = false, onSelect, className }: FlightCardProps) {
  const [expanded, setExpanded] = useState(false);

  const first = offer.segments[0];
  const last = offer.segments[offer.segments.length - 1];
  const stopsLabel =
    offer.stops === 0
      ? 'Non-stop'
      : offer.stops === 1
      ? '1 stop'
      : `${offer.stops} stops`;

  return (
    <article
      data-offer-id={offer.offerId}
      className={cn(
        'flex flex-col gap-3 rounded-lg border bg-background p-4 transition-shadow',
        selected
          ? 'border-border border-l-4 border-l-blue-600 ring-1 ring-blue-600/30'
          : 'border-border hover:shadow-md',
        className,
      )}
    >
      <div className="flex items-start justify-between gap-4">
        <div className="flex items-start gap-3">
          <div className="flex h-6 w-6 items-center justify-center rounded-full bg-muted text-xs font-semibold uppercase">
            {offer.airline.code}
          </div>
          <div>
            <p className="text-sm font-medium leading-tight">{offer.airline.name}</p>
            <p className="text-xs text-muted-foreground">{first.flightNumber}</p>
          </div>
        </div>
        <div className="text-end">
          <p className="text-xl font-semibold tabular-nums">
            {formatMoney(offer.price.total, offer.price.currency)}
          </p>
          <p className="text-xs text-muted-foreground">incl. taxes</p>
        </div>
      </div>

      <div className="flex flex-wrap items-center gap-4 text-sm">
        <div className="flex flex-col">
          <span className="text-lg font-semibold tabular-nums">{formatTime(first.departure)}</span>
          <span className="text-xs text-muted-foreground">{first.from}</span>
        </div>
        <div className="flex flex-1 items-center gap-2 text-xs text-muted-foreground">
          <Plane size={14} />
          <span className="flex-1 border-t border-dashed" />
          <span>{formatDuration(offer.durationMinutes)}</span>
          <span>·</span>
          <span>{stopsLabel}</span>
          <span className="flex-1 border-t border-dashed" />
        </div>
        <div className="flex flex-col text-end">
          <span className="text-lg font-semibold tabular-nums">{formatTime(last.arrival)}</span>
          <span className="text-xs text-muted-foreground">{last.to}</span>
        </div>
      </div>

      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2 text-xs text-muted-foreground">
          <Luggage size={14} />
          <span>
            {offer.baggage
              ? `${offer.baggage.included} ${offer.baggage.unit} checked`
              : 'Carry-on only'}
          </span>
        </div>
        <button
          type="button"
          className="inline-flex items-center gap-1 text-xs font-medium text-blue-600 hover:underline"
          aria-expanded={expanded}
          onClick={() => setExpanded((e) => !e)}
        >
          {expanded ? (
            <>
              Hide fare breakdown <ChevronUp size={14} />
            </>
          ) : (
            <>
              Show fare breakdown <ChevronDown size={14} />
            </>
          )}
        </button>
      </div>

      {expanded && (
        <dl className="grid grid-cols-2 gap-y-1 border-t border-border pt-3 text-xs">
          <dt className="text-muted-foreground">Base fare</dt>
          <dd className="text-end tabular-nums">
            {formatMoney(offer.price.base, offer.price.currency)}
          </dd>
          <dt className="text-muted-foreground">Airline surcharges (YQ/YR)</dt>
          <dd className="text-end tabular-nums">
            {formatMoney(offer.price.yqYr, offer.price.currency)}
          </dd>
          <dt className="text-muted-foreground">Taxes</dt>
          <dd className="text-end tabular-nums">
            {formatMoney(offer.price.taxes, offer.price.currency)}
          </dd>
          <dt className="pt-1 font-medium">Total</dt>
          <dd className="pt-1 text-end font-semibold tabular-nums">
            {formatMoney(offer.price.total, offer.price.currency)}
          </dd>
        </dl>
      )}

      {onSelect && (
        <div className="flex justify-end">
          <button
            type="button"
            onClick={() => onSelect(offer)}
            className="inline-flex items-center gap-1.5 rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
          >
            Select
          </button>
        </div>
      )}
    </article>
  );
}
