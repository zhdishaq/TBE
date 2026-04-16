// Passenger details form (B2C-05 step 4, FLTB-01).
//
// One sub-form per passenger (adults + children + infants from the URL
// state). RHF + zod validation. On submit POSTs /api/bookings with the
// offerId + passenger array; the API route forwards to BookingService
// and returns { bookingId } → navigate to
// /checkout/payment?ref=flight-{id} via the unified B5 contract.

'use client';

import { zodResolver } from '@hookform/resolvers/zod';
import { useRouter } from 'next/navigation';
import { useMemo, useState } from 'react';
import { useForm } from 'react-hook-form';
import { z } from 'zod';

import { buildCheckoutRef } from '@/lib/checkout-ref';

const TITLES = ['Mr', 'Ms', 'Mrs', 'Miss', 'Mx'] as const;
type Title = (typeof TITLES)[number];

const passengerSchema = z.object({
  title: z.enum(TITLES),
  firstName: z.string().min(1, 'First name is required.'),
  lastName: z.string().min(1, 'Last name is required.'),
  dob: z
    .string()
    .min(1, 'Date of birth is required.')
    .refine((s) => {
      const d = new Date(s);
      return !Number.isNaN(d.getTime()) && d.getTime() < Date.now();
    }, 'Date of birth cannot be in the future.'),
  passportNumber: z.string().min(5, 'Passport number looks too short.'),
  passportExpiry: z
    .string()
    .min(1, 'Passport expiry is required.')
    .refine((s) => {
      const d = new Date(s);
      const sixMonthsFromNow = new Date();
      sixMonthsFromNow.setMonth(sixMonthsFromNow.getMonth() + 6);
      return !Number.isNaN(d.getTime()) && d.getTime() > sixMonthsFromNow.getTime();
    }, 'Passport must be valid for at least 6 more months.'),
  nationality: z.string().length(2, 'Use the 2-letter country code (e.g. GB).'),
});

type Passenger = z.infer<typeof passengerSchema>;

const formSchema = z.object({
  passengers: z.array(passengerSchema).min(1),
});

type FormValues = z.infer<typeof formSchema>;

interface PassengerDetailsFormProps {
  offerId: string;
  adt: number;
  chd: number;
  infl: number;
  infs: number;
}

function paxTypes(adt: number, chd: number, infl: number, infs: number): string[] {
  const out: string[] = [];
  for (let i = 0; i < adt; i++) out.push(`Adult ${i + 1}`);
  for (let i = 0; i < chd; i++) out.push(`Child ${i + 1}`);
  for (let i = 0; i < infl; i++) out.push(`Infant on lap ${i + 1}`);
  for (let i = 0; i < infs; i++) out.push(`Infant in seat ${i + 1}`);
  return out;
}

function emptyPassenger(): Passenger {
  return {
    title: 'Mr',
    firstName: '',
    lastName: '',
    dob: '',
    passportNumber: '',
    passportExpiry: '',
    nationality: 'GB',
  };
}

export function PassengerDetailsForm({ offerId, adt, chd, infl, infs }: PassengerDetailsFormProps) {
  const router = useRouter();
  const labels = useMemo(() => paxTypes(adt, chd, infl, infs), [adt, chd, infl, infs]);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const { register, handleSubmit, formState } = useForm<FormValues>({
    resolver: zodResolver(formSchema),
    defaultValues: { passengers: labels.map(() => emptyPassenger()) },
  });

  const onSubmit = handleSubmit(async (values) => {
    setSubmitError(null);
    try {
      const resp = await fetch('/api/bookings', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          offerId,
          productType: 'flight',
          passengers: values.passengers,
        }),
      });
      if (!resp.ok) {
        setSubmitError('Could not start booking. Please try again.');
        return;
      }
      const body = (await resp.json()) as { bookingId?: string };
      if (!body.bookingId) {
        setSubmitError('Booking service returned no bookingId.');
        return;
      }
      router.push(
        `/checkout/payment?ref=${encodeURIComponent(buildCheckoutRef('flight', body.bookingId))}`,
      );
    } catch {
      setSubmitError('Network error. Please try again.');
    }
  });

  return (
    <form onSubmit={onSubmit} className="flex flex-col gap-6">
      {labels.map((label, i) => (
        <fieldset key={i} className="rounded-md border border-border p-4">
          <legend className="px-2 text-sm font-semibold">{label}</legend>

          <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
            <label className="flex flex-col gap-1 text-sm">
              <span>Title</span>
              <select
                {...register(`passengers.${i}.title` as const)}
                className="rounded-md border border-border bg-background px-3 py-2 text-sm"
              >
                {TITLES.map((t) => (
                  <option key={t} value={t}>
                    {t}
                  </option>
                ))}
              </select>
            </label>

            <label className="flex flex-col gap-1 text-sm">
              <span>First name</span>
              <input
                {...register(`passengers.${i}.firstName` as const)}
                type="text"
                autoComplete="given-name"
                className="rounded-md border border-border bg-background px-3 py-2 text-sm"
              />
              {formState.errors.passengers?.[i]?.firstName?.message && (
                <span className="text-xs text-red-600">
                  {formState.errors.passengers?.[i]?.firstName?.message}
                </span>
              )}
            </label>

            <label className="flex flex-col gap-1 text-sm">
              <span>Last name</span>
              <input
                {...register(`passengers.${i}.lastName` as const)}
                type="text"
                autoComplete="family-name"
                className="rounded-md border border-border bg-background px-3 py-2 text-sm"
              />
              {formState.errors.passengers?.[i]?.lastName?.message && (
                <span className="text-xs text-red-600">
                  {formState.errors.passengers?.[i]?.lastName?.message}
                </span>
              )}
            </label>

            <label className="flex flex-col gap-1 text-sm">
              <span>Date of birth</span>
              <input
                {...register(`passengers.${i}.dob` as const)}
                type="date"
                autoComplete="bday"
                className="rounded-md border border-border bg-background px-3 py-2 text-sm"
              />
              {formState.errors.passengers?.[i]?.dob?.message && (
                <span className="text-xs text-red-600">
                  {formState.errors.passengers?.[i]?.dob?.message}
                </span>
              )}
            </label>

            <label className="flex flex-col gap-1 text-sm">
              <span>Passport number</span>
              <input
                {...register(`passengers.${i}.passportNumber` as const)}
                type="text"
                className="rounded-md border border-border bg-background px-3 py-2 text-sm"
              />
              {formState.errors.passengers?.[i]?.passportNumber?.message && (
                <span className="text-xs text-red-600">
                  {formState.errors.passengers?.[i]?.passportNumber?.message}
                </span>
              )}
            </label>

            <label className="flex flex-col gap-1 text-sm">
              <span>Passport expiry</span>
              <input
                {...register(`passengers.${i}.passportExpiry` as const)}
                type="date"
                className="rounded-md border border-border bg-background px-3 py-2 text-sm"
              />
              {formState.errors.passengers?.[i]?.passportExpiry?.message && (
                <span className="text-xs text-red-600">
                  {formState.errors.passengers?.[i]?.passportExpiry?.message}
                </span>
              )}
            </label>

            <label className="flex flex-col gap-1 text-sm md:col-span-2">
              <span>Nationality (ISO code)</span>
              <input
                {...register(`passengers.${i}.nationality` as const)}
                type="text"
                maxLength={2}
                className="rounded-md border border-border bg-background px-3 py-2 text-sm uppercase"
              />
              {formState.errors.passengers?.[i]?.nationality?.message && (
                <span className="text-xs text-red-600">
                  {formState.errors.passengers?.[i]?.nationality?.message}
                </span>
              )}
            </label>
          </div>
        </fieldset>
      ))}

      {submitError && (
        <p role="alert" className="text-sm text-red-600">
          {submitError}
        </p>
      )}

      <div className="flex justify-end">
        <button
          type="submit"
          disabled={formState.isSubmitting}
          className="inline-flex items-center justify-center rounded-md bg-blue-600 px-5 py-2.5 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-60"
        >
          {formState.isSubmitting ? 'Starting booking…' : 'Continue to payment'}
        </button>
      </div>
    </form>
  );
}
