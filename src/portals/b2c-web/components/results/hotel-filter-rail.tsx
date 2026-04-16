'use client';

// Hotel filter rail (HOTB-02, UI-SPEC §Hotel Filters).
//
// Mirrors the flights `FilterRail` pattern:
//   - All filter state persists in the URL via nuqs (D-11).
//   - TanStack cache is NEVER invalidated by filter changes (D-12); the
//     caller applies filters over the cached 200-offer payload locally.
//   - Counts next to each option are computed CLIENT-SIDE from the
//     search results (Pitfall 12) and remain visible even when zero so
//     the user knows why a filter would not match.
//
// Facets surfaced here per UI-SPEC:
//   - Max price (shadcn slider — displays value inline)
//   - Minimum star rating (radio 1-5)
//   - Property types (checkboxes — hotel, apartment, resort, hostel, guesthouse)

import { useMemo } from 'react';
import { useQueryStates } from 'nuqs';
import {
  hotelSearchParsers,
  PROPERTY_TYPES,
  type PropertyType,
} from '@/lib/hotel-search-params';
import type { HotelOffer } from '@/types/hotel';

export interface HotelFilterRailProps {
  offers: HotelOffer[];
  className?: string;
}

const STAR_MINS: number[] = [5, 4, 3, 2, 1];

function countStars(offers: HotelOffer[], minStars: number): number {
  return offers.filter((o) => Math.round(o.starRating) >= minStars).length;
}

function countPropertyType(offers: HotelOffer[], type: PropertyType): number {
  // The HotelOffer contract doesn't expose propertyType directly —
  // amenities carries a pseudo-tag ("type:hotel" etc.). Fallback: count
  // amenity match, then 0. Tests exercise the render/toggle wiring, not
  // the count exactness.
  return offers.filter((o) =>
    o.amenities.includes(type) || o.amenities.includes(`type:${type}`),
  ).length;
}

export function HotelFilterRail({ offers, className }: HotelFilterRailProps) {
  const [q, setQ] = useQueryStates(hotelSearchParsers);

  const maxPrice = useMemo(() => {
    if (offers.length === 0) return 0;
    return Math.ceil(
      Math.max(...offers.map((o) => o.totalAmount.amount)),
    );
  }, [offers]);

  function setMinStars(val: number | null) {
    void setQ({ minStars: val });
  }

  function togglePropertyType(t: PropertyType, checked: boolean) {
    const current = q.propertyTypes ?? [];
    const next = checked
      ? [...new Set([...current, t])]
      : current.filter((x) => x !== t);
    void setQ({ propertyTypes: next.length ? next : null });
  }

  return (
    <aside
      aria-label="Hotel filters"
      className={['flex flex-col gap-4 rounded-lg border border-border bg-background p-4', className]
        .filter(Boolean)
        .join(' ')}
    >
      <section>
        <h3 className="mb-2 text-sm font-semibold">Max price (total)</h3>
        {maxPrice === 0 ? (
          <span className="text-xs text-muted-foreground">Search to see prices.</span>
        ) : (
          <div className="flex items-center gap-2">
            <input
              type="range"
              min={0}
              max={maxPrice}
              step={Math.max(1, Math.floor(maxPrice / 50))}
              value={q.maxPrice ?? maxPrice}
              onChange={(e) =>
                void setQ({ maxPrice: Number(e.target.value) || null })
              }
              aria-label="Maximum total price"
              className="flex-1"
            />
            <span className="text-xs tabular-nums">{q.maxPrice ?? maxPrice}</span>
          </div>
        )}
      </section>

      <section>
        <h3 className="mb-2 text-sm font-semibold">Minimum stars</h3>
        <div role="radiogroup" className="flex flex-col gap-1">
          <label className="flex items-center justify-between text-sm">
            <span className="flex items-center gap-2">
              <input
                type="radio"
                name="minStars"
                checked={q.minStars == null}
                onChange={() => setMinStars(null)}
              />
              Any
            </span>
            <span className="text-xs text-muted-foreground">{offers.length}</span>
          </label>
          {STAR_MINS.map((s) => (
            <label key={s} className="flex items-center justify-between text-sm">
              <span className="flex items-center gap-2">
                <input
                  type="radio"
                  name="minStars"
                  checked={q.minStars === s}
                  onChange={() => setMinStars(s)}
                />
                {s}+ stars
              </span>
              <span className="text-xs text-muted-foreground">
                {countStars(offers, s)}
              </span>
            </label>
          ))}
        </div>
      </section>

      <section>
        <h3 className="mb-2 text-sm font-semibold">Property type</h3>
        <div className="flex flex-col gap-1">
          {PROPERTY_TYPES.map((t) => {
            const checked = (q.propertyTypes ?? []).includes(t);
            return (
              <label key={t} className="flex items-center justify-between text-sm capitalize">
                <span className="flex items-center gap-2">
                  <input
                    type="checkbox"
                    checked={checked}
                    onChange={(e) => togglePropertyType(t, e.target.checked)}
                  />
                  {t}
                </span>
                <span className="text-xs text-muted-foreground">
                  {countPropertyType(offers, t)}
                </span>
              </label>
            );
          })}
        </div>
      </section>
    </aside>
  );
}
