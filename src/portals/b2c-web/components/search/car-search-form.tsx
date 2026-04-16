'use client';

// Car-hire search form (Plan 04-04 / CARB-01, UI-SPEC §Car Search).
//
// - Pickup location (free text today; an airport-or-city combobox can slot
//   in later without breaking the URL contract), date range, and driver
//   age are the three required inputs (CARB-01 acceptance criteria).
// - A time picker is deliberately out-of-scope for the capstone; we
//   auto-fill 10:00 UTC for pickup and dropoff so the URL round-trips
//   a valid ISO-8601 datetime into the nuqs parsers.
// - zod-style validation runs BEFORE nav (dropoff > pickup, driverAge
//   18-99, pickupLocation ≥2 chars) so we don't pointlessly hit the
//   gateway.
// - Submits by serializing state into URL query params via
//   `createSerializer(carSearchParsers)` so the results page reads a
//   consistent shape via `useQueryStates(carSearchParsers)` (D-11).

import { useRouter } from 'next/navigation';
import { createSerializer } from 'nuqs';
import { useState } from 'react';
import { ArrowRight } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { DateRangePicker } from '@/components/search/date-range-picker';
import { carSearchParsers } from '@/lib/car-search-params';

const serialize = createSerializer(carSearchParsers);

export interface CarSearchFormProps {
  mode?: 'full' | 'embedded';
  className?: string;
}

interface FormError {
  field: 'pickup' | 'dates' | 'driverAge';
  message: string;
}

export interface CarSearchFormValues {
  pickupLocation: string;
  range: { from: Date | null; to: Date | null };
  driverAge: number;
}

/**
 * Pure validation helper exported for the unit test — expects a value
 * object identical to the form's in-memory state. Same pattern as
 * `validateHotelSearch`.
 */
export function validateCarSearch(v: CarSearchFormValues): FormError[] {
  const out: FormError[] = [];
  if (!v.pickupLocation || v.pickupLocation.trim().length < 2) {
    out.push({ field: 'pickup', message: 'Please enter a pickup location.' });
  }
  if (!v.range.from) {
    out.push({ field: 'dates', message: 'Please choose a pickup date.' });
  } else {
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    if (v.range.from < today) {
      out.push({ field: 'dates', message: 'Pickup date cannot be in the past.' });
    }
  }
  if (!v.range.to) {
    out.push({ field: 'dates', message: 'Please choose a drop-off date.' });
  } else if (v.range.from && v.range.to <= v.range.from) {
    out.push({ field: 'dates', message: 'Drop-off must be after pickup.' });
  }
  if (!Number.isFinite(v.driverAge) || v.driverAge < 18 || v.driverAge > 99) {
    out.push({ field: 'driverAge', message: 'Driver age must be between 18 and 99.' });
  }
  return out;
}

/**
 * Returns a new Date with the given UTC hour/minute applied. Kept tiny + pure so
 * we don't pull in a Temporal polyfill for a 10:00 default.
 */
function withUtcTime(d: Date, hour: number, minute: number): Date {
  const copy = new Date(d);
  copy.setUTCHours(hour, minute, 0, 0);
  return copy;
}

export function CarSearchForm({ mode = 'full', className }: CarSearchFormProps) {
  const router = useRouter();
  const [pickupLocation, setPickupLocation] = useState('');
  const [range, setRange] = useState<{ from: Date | null; to: Date | null }>({
    from: null,
    to: null,
  });
  const [driverAge, setDriverAge] = useState<number>(30);
  const [errors, setErrors] = useState<FormError[]>([]);
  const [submitting, setSubmitting] = useState(false);

  function onSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    if (submitting) return;
    const errs = validateCarSearch({ pickupLocation, range, driverAge });
    setErrors(errs);
    if (errs.length > 0) return;

    setSubmitting(true);
    const pickupAt = withUtcTime(range.from!, 10, 0);
    const dropoffAt = withUtcTime(range.to!, 10, 0);

    const qs = serialize({
      pickupLocation: pickupLocation.trim(),
      pickupAt,
      dropoffAt,
      driverAge,
      sortKey: 'price-asc',
      maxPrice: null,
      transmissions: null,
      categories: null,
    });
    router.push(`/cars/results${qs}`);
  }

  return (
    <form
      onSubmit={onSubmit}
      aria-label="Car hire search"
      className={[
        'grid gap-3 rounded-lg border border-border bg-background p-4 shadow-sm',
        mode === 'embedded' ? 'md:grid-cols-6' : 'md:grid-cols-2 lg:grid-cols-3',
        className,
      ].filter(Boolean).join(' ')}
    >
      <div className="flex flex-col gap-1 md:col-span-2">
        <label htmlFor="pickup-location" className="text-xs font-medium text-muted-foreground">
          Pickup location
        </label>
        <input
          id="pickup-location"
          name="pickupLocation"
          type="text"
          value={pickupLocation}
          onChange={(e) => setPickupLocation(e.target.value)}
          placeholder="e.g. LHR Terminal 5 or London"
          className="h-10 rounded-md border border-border bg-background px-3 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          autoComplete="off"
          spellCheck={false}
        />
      </div>
      <DateRangePicker value={range} onChange={setRange} className="md:col-span-2" />
      <div className="flex flex-col gap-1 md:col-span-2">
        <label htmlFor="driver-age" className="text-xs font-medium text-muted-foreground">
          Driver age
        </label>
        <input
          id="driver-age"
          name="driverAge"
          type="number"
          min={18}
          max={99}
          value={driverAge}
          onChange={(e) => setDriverAge(Number(e.target.value))}
          className="h-10 w-28 rounded-md border border-border bg-background px-3 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
      </div>
      <div className="md:col-span-full flex flex-col items-stretch gap-2 md:flex-row md:items-center md:justify-between">
        {errors.length > 0 ? (
          <ul className="text-xs text-red-600" role="alert">
            {errors.map((e) => (
              <li key={`${e.field}:${e.message}`}>{e.message}</li>
            ))}
          </ul>
        ) : (
          <span className="text-xs text-muted-foreground">
            Young-driver and airport-surcharge disclosures are shown on the detail page.
          </span>
        )}
        <Button type="submit" disabled={submitting} className="md:min-w-40">
          Search cars
          <ArrowRight size={16} className="ms-2" />
        </Button>
      </div>
    </form>
  );
}
