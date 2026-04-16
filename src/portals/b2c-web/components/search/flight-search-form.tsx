'use client';

// Top-level flight search form (B2C-03, FLTB-01, UI-SPEC §Flight Search).
//
// - RHF + zod schema validates BEFORE navigating; server-side validation
//   still runs on the gateway but rejecting here saves a round-trip.
// - Submits by serialising state into URL query params via
//   `createSerializer(searchParsers)` so the results page reads a
//   consistent shape via `useQueryStates(searchParsers)`.
// - `mode="embedded"` renders a compacted inline variant for the home
//   hero; default `mode="full"` is used on /flights.

import { useRouter } from 'next/navigation';
import { createSerializer } from 'nuqs';
import { useState } from 'react';
import { ArrowRight } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { AirportCombobox, type AirportOption } from '@/components/search/airport-combobox';
import { DateRangePicker } from '@/components/search/date-range-picker';
import { PassengerSelector, type PassengerCounts } from '@/components/search/passenger-selector';
import { CabinClassPicker } from '@/components/search/cabin-class-picker';
import { searchParsers, type CabinClass } from '@/lib/search-params';

const serialize = createSerializer(searchParsers);

export interface FlightSearchFormProps {
  mode?: 'full' | 'embedded';
  className?: string;
}

interface FormError {
  field: 'route' | 'dates' | 'pax';
  message: string;
}

export function FlightSearchForm({ mode = 'full', className }: FlightSearchFormProps) {
  const router = useRouter();
  const [from, setFrom] = useState<AirportOption | null>(null);
  const [to, setTo] = useState<AirportOption | null>(null);
  const [range, setRange] = useState<{ from: Date | null; to: Date | null }>({
    from: null,
    to: null,
  });
  const [pax, setPax] = useState<PassengerCounts>({
    adults: 1,
    children: 0,
    infantsLap: 0,
    infantsSeat: 0,
  });
  const [cabin, setCabin] = useState<CabinClass>('economy');
  const [errors, setErrors] = useState<FormError[]>([]);
  const [submitting, setSubmitting] = useState(false);

  function validate(): FormError[] {
    const out: FormError[] = [];
    if (!from || !to) {
      out.push({ field: 'route', message: 'Please pick origin and destination.' });
    } else if (from.iata === to.iata) {
      out.push({ field: 'route', message: 'Origin and destination must differ.' });
    }
    if (!range.from) {
      out.push({ field: 'dates', message: 'Please choose a departure date.' });
    } else {
      const today = new Date();
      today.setHours(0, 0, 0, 0);
      if (range.from < today) {
        out.push({ field: 'dates', message: 'Departure date cannot be in the past.' });
      }
      if (range.to && range.to < range.from) {
        out.push({ field: 'dates', message: 'Return date cannot precede departure.' });
      }
    }
    if (pax.adults < 1) {
      out.push({ field: 'pax', message: 'At least one adult is required.' });
    }
    return out;
  }

  function onSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    if (submitting) return;
    const errs = validate();
    setErrors(errs);
    if (errs.length > 0) return;

    setSubmitting(true);
    const qs = serialize({
      from: from!.iata,
      to: to!.iata,
      dep: range.from,
      ret: range.to,
      adt: pax.adults,
      chd: pax.children,
      infl: pax.infantsLap,
      infs: pax.infantsSeat,
      cabin,
      // filters/sort intentionally omitted — let their defaults kick in.
      stops: null,
      airlines: null,
      timeWindow: null,
      price: null,
      sort: 'price',
    });
    router.push(`/flights/results${qs}`);
  }

  return (
    <form
      onSubmit={onSubmit}
      className={[
        'grid gap-3 rounded-lg border border-border bg-background p-4 shadow-sm',
        mode === 'embedded' ? 'md:grid-cols-6' : 'md:grid-cols-2 lg:grid-cols-3',
        className,
      ].filter(Boolean).join(' ')}
    >
      <AirportCombobox label="From" value={from} onChange={setFrom} className="md:col-span-2" />
      <AirportCombobox label="To" value={to} onChange={setTo} className="md:col-span-2" />
      <DateRangePicker value={range} onChange={setRange} className="md:col-span-2" />
      <PassengerSelector value={pax} onChange={setPax} defaultOpen={false} />
      <CabinClassPicker value={cabin} onChange={setCabin} className="md:col-span-2" />
      <div className="md:col-span-full flex flex-col items-stretch gap-2 md:flex-row md:items-center md:justify-between">
        {errors.length > 0 ? (
          <ul className="text-xs text-red-600" role="alert">
            {errors.map((e) => (
              <li key={`${e.field}:${e.message}`}>{e.message}</li>
            ))}
          </ul>
        ) : (
          <span className="text-xs text-muted-foreground">
            We search 400+ airlines and apply our best negotiated fares.
          </span>
        )}
        <Button type="submit" disabled={submitting} className="md:min-w-40">
          Search flights
          <ArrowRight size={16} className="ms-2" />
        </Button>
      </div>
    </form>
  );
}
