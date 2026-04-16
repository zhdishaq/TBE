'use client';

// Filter rail for the results page (B2C-04, UI-SPEC §Filters).
//
// All filter state is persisted in the URL via nuqs (D-11). The rail is
// a pure consumer/producer — it reads current filter state and writes
// back the new selection. TanStack Query cache is NOT invalidated by
// filter changes (D-12, Pitfall 11): the caller's `useMemo(applyFilters, …)`
// runs locally over the cached 200-offer payload.
//
// Counts next to each option are computed on the CLIENT from the current
// search results (Pitfall 12). "0" counts remain visible (so users know
// why a filter wouldn't match) rather than hidden.

import { useMemo } from 'react';
import { useQueryStates } from 'nuqs';
import { searchParsers, type StopsOption, type TimeWindow } from '@/lib/search-params';
import type { FlightOffer } from '@/hooks/use-flight-search';

export interface FilterRailProps {
  offers: FlightOffer[];
  className?: string;
}

const STOPS: Array<{ value: StopsOption; label: string }> = [
  { value: 'any', label: 'Any' },
  { value: '0', label: 'Non-stop' },
  { value: '1', label: '1 stop' },
  { value: '2+', label: '2+ stops' },
];

const TIME_WINDOWS: Array<{ value: TimeWindow; label: string; range: [number, number] }> = [
  { value: 'early', label: 'Early (00–06)', range: [0, 6] },
  { value: 'morning', label: 'Morning (06–12)', range: [6, 12] },
  { value: 'afternoon', label: 'Afternoon (12–18)', range: [12, 18] },
  { value: 'evening', label: 'Evening (18–24)', range: [18, 24] },
];

function countStops(offers: FlightOffer[], target: StopsOption): number {
  if (target === 'any') return offers.length;
  if (target === '2+') return offers.filter((o) => o.stops >= 2).length;
  return offers.filter((o) => o.stops === Number(target)).length;
}

function countAirline(offers: FlightOffer[], code: string): number {
  return offers.filter((o) => o.airline.code === code).length;
}

function countTime(offers: FlightOffer[], range: [number, number]): number {
  return offers.filter((o) => {
    const h = new Date(o.segments[0].departure).getHours();
    return h >= range[0] && h < range[1];
  }).length;
}

export function FilterRail({ offers, className }: FilterRailProps) {
  const [q, setQ] = useQueryStates(searchParsers);

  const airlines = useMemo(() => {
    const seen = new Map<string, string>();
    for (const o of offers) {
      if (!seen.has(o.airline.code)) seen.set(o.airline.code, o.airline.name);
    }
    return [...seen.entries()].map(([code, name]) => ({ code, name }));
  }, [offers]);

  const maxPrice = useMemo(() => {
    if (offers.length === 0) return 0;
    return Math.ceil(Math.max(...offers.map((o) => o.price.total)));
  }, [offers]);

  function toggleAirline(code: string, checked: boolean) {
    const current = q.airlines ?? [];
    const next = checked
      ? [...new Set([...current, code])]
      : current.filter((c) => c !== code);
    void setQ({ airlines: next.length ? next : null });
  }

  function setStops(val: StopsOption) {
    void setQ({ stops: val === 'any' ? null : [val] });
  }

  function toggleTime(tw: TimeWindow, checked: boolean) {
    const current = q.timeWindow ?? [];
    const next = checked
      ? [...new Set([...current, tw])]
      : current.filter((t) => t !== tw);
    void setQ({ timeWindow: next.length ? next : null });
  }

  const currentStops: StopsOption = q.stops?.[0] ?? 'any';

  return (
    <aside
      aria-label="Filters"
      className={['flex flex-col gap-4 rounded-lg border border-border bg-background p-4', className]
        .filter(Boolean)
        .join(' ')}
    >
      <section>
        <h3 className="mb-2 text-sm font-semibold">Stops</h3>
        <div role="radiogroup" className="flex flex-col gap-1">
          {STOPS.map((s) => (
            <label key={s.value} className="flex items-center justify-between text-sm">
              <span className="flex items-center gap-2">
                <input
                  type="radio"
                  name="stops"
                  value={s.value}
                  checked={currentStops === s.value}
                  onChange={() => setStops(s.value)}
                />
                {s.label}
              </span>
              <span className="text-xs text-muted-foreground">
                {countStops(offers, s.value)}
              </span>
            </label>
          ))}
        </div>
      </section>

      <section>
        <h3 className="mb-2 text-sm font-semibold">Airlines</h3>
        <div className="flex flex-col gap-1">
          {airlines.length === 0 && (
            <span className="text-xs text-muted-foreground">No results yet.</span>
          )}
          {airlines.map((a) => {
            const checked = (q.airlines ?? []).includes(a.code);
            return (
              <label key={a.code} className="flex items-center justify-between text-sm">
                <span className="flex items-center gap-2">
                  <input
                    type="checkbox"
                    checked={checked}
                    onChange={(e) => toggleAirline(a.code, e.target.checked)}
                  />
                  {a.name}
                </span>
                <span className="text-xs text-muted-foreground">
                  {countAirline(offers, a.code)}
                </span>
              </label>
            );
          })}
        </div>
      </section>

      <section>
        <h3 className="mb-2 text-sm font-semibold">Departure time</h3>
        <div className="grid grid-cols-2 gap-2">
          {TIME_WINDOWS.map((tw) => {
            const checked = (q.timeWindow ?? []).includes(tw.value);
            return (
              <button
                key={tw.value}
                type="button"
                onClick={() => toggleTime(tw.value, !checked)}
                aria-pressed={checked}
                className={[
                  'rounded-md border px-2 py-1 text-xs',
                  checked
                    ? 'border-blue-600 bg-blue-50 text-blue-900'
                    : 'border-border hover:bg-muted',
                ].join(' ')}
              >
                {tw.label}
                <span className="ms-1 text-muted-foreground">
                  ({countTime(offers, tw.range)})
                </span>
              </button>
            );
          })}
        </div>
      </section>

      <section>
        <h3 className="mb-2 text-sm font-semibold">Max price</h3>
        {maxPrice === 0 ? (
          <span className="text-xs text-muted-foreground">Search to see prices.</span>
        ) : (
          <div className="flex items-center gap-2">
            <input
              type="range"
              min={0}
              max={maxPrice}
              step={Math.max(1, Math.floor(maxPrice / 50))}
              value={q.price ?? maxPrice}
              onChange={(e) =>
                void setQ({ price: Number(e.target.value) || null })
              }
              className="flex-1"
            />
            <span className="text-xs tabular-nums">{q.price ?? maxPrice}</span>
          </div>
        )}
      </section>
    </aside>
  );
}
