// Plan 05-02 Task 3 — DualPricingGrid.
//
// Results grid for the B2B flight search. Renders FOUR price columns in
// priority order: NET / Markup / GROSS / Commission. Commission is the only
// green-coloured column (UI-SPEC §Dual-pricing Grid, §Colour Rules).
//
// - Every price cell applies `font-variant-numeric: tabular-nums` via
//   Tailwind `tabular-nums` so digit widths align across rows.
// - Every price cell carries an `aria-label` that spells out the role
//   (e.g. `"Net fare £234.50"`) for screen-reader consumers.
// - Selected row gets a 4px indigo-600 left border + 1px ring (indigo-200).
// - Sort keys include price, duration, departure, AND commission (B2B-03).
//
// Pitfall 21 — the NET column is agent-only data: it MUST never surface on a
// traveller-facing artefact. Keeping the grid under the /b2b-web portal scope
// (JWT-gated via B2BPolicy) is the structural enforcement.
'use client';

import { useMemo, useState } from 'react';
import { formatMoney } from '@/lib/format-money';

export interface PricedOffer {
  offerId: string;
  airline: string;
  flightNumber: string;
  departAt: string;
  arriveAt: string;
  durationMinutes: number;
  stops: number;
  net: number;
  markup: number;
  gross: number;
  commission: number;
  currency: string;
}

export type SortKey =
  | 'gross-asc'
  | 'gross-desc'
  | 'duration-asc'
  | 'depart-asc'
  | 'commission-asc'
  | 'commission-desc';

export interface DualPricingGridProps {
  offers: PricedOffer[];
  initialSort?: SortKey;
  initialSelected?: string;
  onSelect?: (offerId: string) => void;
}

function sortOffers(offers: PricedOffer[], key: SortKey): PricedOffer[] {
  const copy = [...offers];
  switch (key) {
    case 'gross-asc':
      return copy.sort((a, b) => a.gross - b.gross);
    case 'gross-desc':
      return copy.sort((a, b) => b.gross - a.gross);
    case 'duration-asc':
      return copy.sort((a, b) => a.durationMinutes - b.durationMinutes);
    case 'depart-asc':
      return copy.sort(
        (a, b) => new Date(a.departAt).getTime() - new Date(b.departAt).getTime(),
      );
    case 'commission-asc':
      return copy.sort((a, b) => a.commission - b.commission);
    case 'commission-desc':
      return copy.sort((a, b) => b.commission - a.commission);
  }
}

export function DualPricingGrid({
  offers,
  initialSort = 'gross-asc',
  initialSelected,
  onSelect,
}: DualPricingGridProps) {
  const [sortKey, setSortKey] = useState<SortKey>(initialSort);
  const [selectedOfferId, setSelectedOfferId] = useState<string | undefined>(
    initialSelected,
  );

  // Client-only sort; MUST NOT refetch on sort change (Phase 4 D-12 cache
  // contract — the grid uses useMemo over the TanStack cache).
  const sorted = useMemo(() => sortOffers(offers, sortKey), [offers, sortKey]);

  function handleSelect(offerId: string) {
    setSelectedOfferId(offerId);
    onSelect?.(offerId);
  }

  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center justify-between">
        <h2 className="text-base font-semibold">Flights</h2>
        <label className="flex items-center gap-2 text-sm">
          <span>Sort</span>
          <select
            aria-label="Sort offers"
            value={sortKey}
            onChange={(e) => setSortKey(e.target.value as SortKey)}
            className="h-9 rounded-md border border-zinc-300 px-2 text-sm"
          >
            <option value="gross-asc">Price — low to high</option>
            <option value="gross-desc">Price — high to low</option>
            <option value="duration-asc">Duration — shortest</option>
            <option value="depart-asc">Departure — earliest</option>
            <option value="commission-asc">Commission — lowest</option>
            <option value="commission-desc">Commission — highest</option>
          </select>
        </label>
      </div>

      <table className="w-full border-collapse text-sm">
        <thead>
          <tr className="border-b border-zinc-200 text-left">
            <th scope="col" className="py-2 pr-3 font-medium">Flight</th>
            <th scope="col" className="py-2 pr-3 font-medium">Depart</th>
            <th scope="col" className="py-2 pr-3 font-medium">Duration</th>
            <th scope="col" className="py-2 pr-3 text-right font-medium">NET</th>
            <th scope="col" className="py-2 pr-3 text-right font-medium">Markup</th>
            <th scope="col" className="py-2 pr-3 text-right font-medium">GROSS</th>
            <th scope="col" className="py-2 pr-3 text-right font-medium">Commission</th>
            <th scope="col" className="py-2 font-medium"><span className="sr-only">Select</span></th>
          </tr>
        </thead>
        <tbody>
          {sorted.map((offer) => {
            const isSelected = offer.offerId === selectedOfferId;
            return (
              <tr
                key={offer.offerId}
                data-testid={`offer-row-${offer.offerId}`}
                className={[
                  'border-b border-zinc-100',
                  isSelected
                    ? 'border-l-4 border-l-indigo-600 ring-1 ring-indigo-200'
                    : '',
                ].join(' ')}
              >
                <td className="py-2 pr-3">
                  {offer.airline} {offer.flightNumber}
                </td>
                <td className="py-2 pr-3">{offer.departAt}</td>
                <td className="py-2 pr-3">{offer.durationMinutes}m</td>
                <td
                  className="py-2 pr-3 text-right tabular-nums bg-zinc-50"
                  aria-label={`Net fare ${formatMoney(offer.net, offer.currency)}`}
                >
                  {formatMoney(offer.net, offer.currency)}
                </td>
                <td
                  className="py-2 pr-3 text-right tabular-nums"
                  aria-label={`Markup ${formatMoney(offer.markup, offer.currency)}`}
                >
                  {formatMoney(offer.markup, offer.currency)}
                </td>
                <td
                  className="py-2 pr-3 text-right tabular-nums font-semibold"
                  aria-label={`Gross fare ${formatMoney(offer.gross, offer.currency)}`}
                >
                  {formatMoney(offer.gross, offer.currency)}
                </td>
                <td
                  className="py-2 pr-3 text-right tabular-nums text-green-700 dark:text-green-300"
                  aria-label={`Agency commission ${formatMoney(offer.commission, offer.currency)}`}
                >
                  {formatMoney(offer.commission, offer.currency)}
                </td>
                <td className="py-2">
                  <button
                    type="button"
                    onClick={() => handleSelect(offer.offerId)}
                    className="h-8 rounded-md border border-zinc-300 px-2 text-xs font-medium hover:bg-zinc-50"
                  >
                    {isSelected ? 'Selected' : 'Select'}
                  </button>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
