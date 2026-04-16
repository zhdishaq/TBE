'use client';

// Hotel search form (HOTB-01, UI-SPEC §Hotel Search).
//
// - Destination, date range, and occupancy are the four required inputs.
// - zod validates BEFORE nav (checkout > checkin, adults 1-9, children
//   0-4, destination ≥2 chars) so we don't pointlessly hit the gateway.
// - Submits by serializing state into URL query params via
//   `createSerializer(hotelSearchParsers)` so the results page reads a
//   consistent shape via `useQueryStates(hotelSearchParsers)` (D-11).
// - Reuses `DateRangePicker` from 04-02 verbatim.

import { useRouter } from 'next/navigation';
import { createSerializer } from 'nuqs';
import { useState } from 'react';
import { ArrowRight } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { DestinationCombobox, type DestinationOption } from '@/components/search/destination-combobox';
import { DateRangePicker } from '@/components/search/date-range-picker';
import { OccupancySelector } from '@/components/search/occupancy-selector';
import { hotelSearchParsers } from '@/lib/hotel-search-params';
import type { OccupancySpec } from '@/types/hotel';

const serialize = createSerializer(hotelSearchParsers);

export interface HotelSearchFormProps {
  mode?: 'full' | 'embedded';
  className?: string;
}

interface FormError {
  field: 'destination' | 'dates' | 'occupancy';
  message: string;
}

/**
 * Pure validation helper exported for the unit test — expects a value
 * object identical to the form's in-memory state. Keeping this as a
 * free function (not a zod schema call inside the component) means the
 * tests can call it directly without mounting RHF.
 */
export interface HotelSearchFormValues {
  destination: DestinationOption | null;
  range: { from: Date | null; to: Date | null };
  occupancy: OccupancySpec;
}

export function validateHotelSearch(v: HotelSearchFormValues): FormError[] {
  const out: FormError[] = [];
  if (!v.destination || v.destination.cityCode.length < 2) {
    out.push({ field: 'destination', message: 'Please pick a destination.' });
  }
  if (!v.range.from) {
    out.push({ field: 'dates', message: 'Please choose a check-in date.' });
  } else {
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    if (v.range.from < today) {
      out.push({ field: 'dates', message: 'Check-in date cannot be in the past.' });
    }
  }
  if (!v.range.to) {
    out.push({ field: 'dates', message: 'Please choose a check-out date.' });
  } else if (v.range.from && v.range.to <= v.range.from) {
    out.push({ field: 'dates', message: 'Check-out must be after check-in.' });
  }
  if (v.occupancy.adults < 1 || v.occupancy.adults > 9) {
    out.push({ field: 'occupancy', message: 'Adults must be between 1 and 9.' });
  }
  if (v.occupancy.children < 0 || v.occupancy.children > 4) {
    out.push({ field: 'occupancy', message: 'Children must be between 0 and 4.' });
  }
  if (v.occupancy.rooms < 1 || v.occupancy.rooms > 5) {
    out.push({ field: 'occupancy', message: 'Rooms must be between 1 and 5.' });
  }
  return out;
}

export function HotelSearchForm({ mode = 'full', className }: HotelSearchFormProps) {
  const router = useRouter();
  const [destination, setDestination] = useState<DestinationOption | null>(null);
  const [range, setRange] = useState<{ from: Date | null; to: Date | null }>({
    from: null,
    to: null,
  });
  const [occupancy, setOccupancy] = useState<OccupancySpec>({
    rooms: 1,
    adults: 2,
    children: 0,
  });
  const [errors, setErrors] = useState<FormError[]>([]);
  const [submitting, setSubmitting] = useState(false);

  function onSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    if (submitting) return;
    const errs = validateHotelSearch({ destination, range, occupancy });
    setErrors(errs);
    if (errs.length > 0) return;

    setSubmitting(true);
    const qs = serialize({
      destinationCityCode: destination!.cityCode,
      checkin: range.from,
      checkout: range.to,
      rooms: occupancy.rooms,
      adults: occupancy.adults,
      children: occupancy.children,
      sortKey: 'price-asc',
      maxPrice: null,
      minStars: null,
      propertyTypes: null,
    });
    router.push(`/hotels/results${qs}`);
  }

  return (
    <form
      onSubmit={onSubmit}
      aria-label="Hotel search"
      className={[
        'grid gap-3 rounded-lg border border-border bg-background p-4 shadow-sm',
        mode === 'embedded' ? 'md:grid-cols-6' : 'md:grid-cols-2 lg:grid-cols-3',
        className,
      ].filter(Boolean).join(' ')}
    >
      <DestinationCombobox
        label="Destination"
        value={destination}
        onChange={setDestination}
        className="md:col-span-2"
      />
      <DateRangePicker value={range} onChange={setRange} className="md:col-span-2" />
      <OccupancySelector value={occupancy} onChange={setOccupancy} className="md:col-span-2" />
      <div className="md:col-span-full flex flex-col items-stretch gap-2 md:flex-row md:items-center md:justify-between">
        {errors.length > 0 ? (
          <ul className="text-xs text-red-600" role="alert">
            {errors.map((e) => (
              <li key={`${e.field}:${e.message}`}>{e.message}</li>
            ))}
          </ul>
        ) : (
          <span className="text-xs text-muted-foreground">
            We search hundreds of properties and surface real-time availability.
          </span>
        )}
        <Button type="submit" disabled={submitting} className="md:min-w-40">
          Search hotels
          <ArrowRight size={16} className="ms-2" />
        </Button>
      </div>
    </form>
  );
}
