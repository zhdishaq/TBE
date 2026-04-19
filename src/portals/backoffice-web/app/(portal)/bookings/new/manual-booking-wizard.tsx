'use client';

// Plan 06-02 Task 1 (BO-02) — 3-step manual booking wizard.
//
// Step 1: Source & product — ProductType (Flight | Hotel | Car |
//   Transfer), SupplierReference (mono), BookingReference (mono).
// Step 2: Itinerary & passengers — dynamic passenger list + segments
//   serialized to ItineraryJson on submit.
// Step 3: Pricing & customer — fare breakdown, customer contact,
//   optional AgencyId.
//
// On submit POSTs to /api/bookings/manual (proxy → BookingService).
// On 201 redirects to /bookings/{bookingId}. On 409 shows the
// duplicate-supplier-reference banner with a link to the existing row.

import { useRouter } from 'next/navigation';
import { useMemo, useState } from 'react';

type ProductType = 'Flight' | 'Hotel' | 'Car' | 'Transfer';
type PassengerType = 'Adult' | 'Child' | 'Infant';

type Passenger = {
  type: PassengerType;
  firstName: string;
  lastName: string;
  dateOfBirth: string;
  passport: string;
};

type Segment = {
  origin: string;
  destination: string;
  departure: string;
  arrival: string;
  carrier: string;
};

type Step = 1 | 2 | 3;

type ErrorBanner =
  | { kind: 'duplicate'; existingBookingId: string; supplierReference: string }
  | { kind: 'validation'; message: string }
  | { kind: 'generic'; message: string }
  | null;

const EMPTY_PASSENGER: Passenger = {
  type: 'Adult',
  firstName: '',
  lastName: '',
  dateOfBirth: '',
  passport: '',
};

const EMPTY_SEGMENT: Segment = {
  origin: '',
  destination: '',
  departure: '',
  arrival: '',
  carrier: '',
};

export function ManualBookingWizard() {
  const router = useRouter();
  const [step, setStep] = useState<Step>(1);

  // Step 1 state
  const [productType, setProductType] = useState<ProductType>('Flight');
  const [supplierReference, setSupplierReference] = useState('');
  const [bookingReference, setBookingReference] = useState('');

  // Step 2 state
  const [passengers, setPassengers] = useState<Passenger[]>([
    { ...EMPTY_PASSENGER },
  ]);
  const [segments, setSegments] = useState<Segment[]>([{ ...EMPTY_SEGMENT }]);

  // Step 3 state
  const [baseFareAmount, setBaseFareAmount] = useState('');
  const [surchargeAmount, setSurchargeAmount] = useState('0');
  const [taxAmount, setTaxAmount] = useState('0');
  const [currency, setCurrency] = useState('GBP');
  const [customerId, setCustomerId] = useState('');
  const [customerName, setCustomerName] = useState('');
  const [customerEmail, setCustomerEmail] = useState('');
  const [customerPhone, setCustomerPhone] = useState('');
  const [agencyId, setAgencyId] = useState('');
  const [notes, setNotes] = useState('');

  // Submit state
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<ErrorBanner>(null);

  const step1Valid = useMemo(
    () =>
      supplierReference.trim().length > 0 &&
      bookingReference.trim().length > 0,
    [supplierReference, bookingReference],
  );

  const step2Valid = useMemo(
    () =>
      passengers.length >= 1 &&
      passengers.every(
        (p) => p.firstName.trim() && p.lastName.trim() && p.dateOfBirth,
      ) &&
      segments.length >= 1 &&
      segments.every(
        (s) =>
          s.origin.trim().length === 3 &&
          s.destination.trim().length === 3 &&
          s.departure &&
          s.arrival &&
          s.carrier.trim(),
      ),
    [passengers, segments],
  );

  const step3Valid = useMemo(() => {
    const base = Number(baseFareAmount);
    const sur = Number(surchargeAmount);
    const tax = Number(taxAmount);
    return (
      Number.isFinite(base) &&
      base >= 0 &&
      Number.isFinite(sur) &&
      sur >= 0 &&
      Number.isFinite(tax) &&
      tax >= 0 &&
      customerName.trim() &&
      customerEmail.trim() &&
      currency.trim().length === 3
    );
  }, [
    baseFareAmount,
    surchargeAmount,
    taxAmount,
    currency,
    customerName,
    customerEmail,
  ]);

  async function onSubmit() {
    setSubmitting(true);
    setError(null);
    try {
      const itineraryJson = JSON.stringify({ passengers, segments });
      const res = await fetch('/api/bookings/manual', {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify({
          bookingReference: bookingReference.trim(),
          pnr: bookingReference.trim(),
          productType,
          baseFareAmount: Number(baseFareAmount),
          surchargeAmount: Number(surchargeAmount || 0),
          taxAmount: Number(taxAmount || 0),
          currency: currency.trim().toUpperCase(),
          customerId: customerId.trim() || null,
          customerName: customerName.trim(),
          customerEmail: customerEmail.trim(),
          customerPhone: customerPhone.trim() || null,
          agencyId: agencyId.trim() || null,
          itineraryJson,
          supplierReference: supplierReference.trim(),
          notes: notes.trim() || null,
        }),
      });

      if (res.status === 201) {
        const body = (await res.json()) as { bookingId: string };
        router.push(`/bookings/${body.bookingId}`);
        return;
      }
      if (res.status === 409) {
        const body = (await res.json()) as {
          type: string;
          existingBookingId?: string;
          supplierReference?: string;
        };
        setError({
          kind: 'duplicate',
          existingBookingId: body.existingBookingId ?? '',
          supplierReference: body.supplierReference ?? supplierReference,
        });
        return;
      }
      if (res.status === 400) {
        const body = (await res.json()) as { title?: string };
        setError({
          kind: 'validation',
          message: body.title ?? 'Validation failed',
        });
        return;
      }
      setError({
        kind: 'generic',
        message: `Unexpected status ${res.status}`,
      });
    } catch (err) {
      setError({ kind: 'generic', message: (err as Error).message });
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="flex flex-col gap-6 rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
      <Stepper step={step} />

      {error && <ErrorBanner error={error} />}

      {step === 1 && (
        <Step1
          productType={productType}
          onProductType={setProductType}
          supplierReference={supplierReference}
          onSupplierReference={setSupplierReference}
          bookingReference={bookingReference}
          onBookingReference={setBookingReference}
        />
      )}

      {step === 2 && (
        <Step2
          passengers={passengers}
          setPassengers={setPassengers}
          segments={segments}
          setSegments={setSegments}
        />
      )}

      {step === 3 && (
        <Step3
          baseFareAmount={baseFareAmount}
          setBaseFareAmount={setBaseFareAmount}
          surchargeAmount={surchargeAmount}
          setSurchargeAmount={setSurchargeAmount}
          taxAmount={taxAmount}
          setTaxAmount={setTaxAmount}
          currency={currency}
          setCurrency={setCurrency}
          customerId={customerId}
          setCustomerId={setCustomerId}
          customerName={customerName}
          setCustomerName={setCustomerName}
          customerEmail={customerEmail}
          setCustomerEmail={setCustomerEmail}
          customerPhone={customerPhone}
          setCustomerPhone={setCustomerPhone}
          agencyId={agencyId}
          setAgencyId={setAgencyId}
          notes={notes}
          setNotes={setNotes}
        />
      )}

      <nav className="flex items-center justify-between border-t border-slate-200 pt-4">
        <button
          type="button"
          className="rounded border border-slate-300 px-4 py-2 text-sm text-slate-700 disabled:opacity-50"
          disabled={step === 1 || submitting}
          onClick={() => setStep((s) => (s > 1 ? ((s - 1) as Step) : s))}
        >
          Back
        </button>
        {step < 3 ? (
          <button
            type="button"
            className="rounded bg-slate-900 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50"
            disabled={
              (step === 1 && !step1Valid) || (step === 2 && !step2Valid)
            }
            onClick={() => setStep((s) => (s < 3 ? ((s + 1) as Step) : s))}
          >
            Continue
          </button>
        ) : (
          <button
            type="button"
            className="rounded bg-emerald-600 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50"
            disabled={!step3Valid || submitting}
            onClick={onSubmit}
          >
            {submitting ? 'Submitting…' : 'Confirm booking'}
          </button>
        )}
      </nav>
    </div>
  );
}

function Stepper({ step }: { step: Step }) {
  const labels: Record<Step, string> = {
    1: 'Source & product',
    2: 'Itinerary & passengers',
    3: 'Pricing & customer',
  };
  return (
    <ol className="flex gap-2 text-xs">
      {([1, 2, 3] as Step[]).map((n) => {
        const active = n === step;
        const done = n < step;
        return (
          <li
            key={n}
            aria-current={active ? 'step' : undefined}
            className={[
              'flex-1 rounded px-3 py-2 font-medium',
              active
                ? 'bg-slate-900 text-white'
                : done
                  ? 'bg-emerald-100 text-emerald-900'
                  : 'bg-slate-100 text-slate-500',
            ].join(' ')}
          >
            <span className="mr-2 inline-flex h-5 w-5 items-center justify-center rounded-full bg-white/20 text-[10px]">
              {n}
            </span>
            {labels[n]}
          </li>
        );
      })}
    </ol>
  );
}

function ErrorBanner({ error }: { error: NonNullable<ErrorBanner> }) {
  if (error.kind === 'duplicate') {
    return (
      <div className="rounded border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900">
        Duplicate supplier reference{' '}
        <span className="font-mono font-semibold">
          {error.supplierReference}
        </span>{' '}
        already exists within the last 24 hours.{' '}
        {error.existingBookingId && (
          <a
            href={`/bookings/${error.existingBookingId}`}
            className="underline"
          >
            View existing booking
          </a>
        )}
      </div>
    );
  }
  return (
    <div className="rounded border border-rose-300 bg-rose-50 p-3 text-sm text-rose-900">
      {error.message}
    </div>
  );
}

function Step1({
  productType,
  onProductType,
  supplierReference,
  onSupplierReference,
  bookingReference,
  onBookingReference,
}: {
  productType: ProductType;
  onProductType: (p: ProductType) => void;
  supplierReference: string;
  onSupplierReference: (v: string) => void;
  bookingReference: string;
  onBookingReference: (v: string) => void;
}) {
  const opts: ProductType[] = ['Flight', 'Hotel', 'Car', 'Transfer'];
  return (
    <fieldset className="flex flex-col gap-4">
      <legend className="text-sm font-semibold text-slate-900">
        Step 1 — Source & product
      </legend>
      <div>
        <label className="mb-1 block text-xs font-medium text-slate-700">
          Product type
        </label>
        <div className="flex gap-2">
          {opts.map((o) => (
            <label key={o} className="flex items-center gap-1 text-sm">
              <input
                type="radio"
                name="productType"
                value={o}
                checked={productType === o}
                onChange={() => onProductType(o)}
              />
              {o}
            </label>
          ))}
        </div>
      </div>
      <label className="flex flex-col gap-1 text-xs">
        <span className="font-medium text-slate-700">Supplier reference</span>
        <input
          type="text"
          className="rounded border border-slate-300 px-2 py-1 font-mono text-sm"
          value={supplierReference}
          onChange={(e) => onSupplierReference(e.target.value)}
          placeholder="e.g. AMD-12345"
        />
      </label>
      <label className="flex flex-col gap-1 text-xs">
        <span className="font-medium text-slate-700">Booking reference</span>
        <input
          type="text"
          className="rounded border border-slate-300 px-2 py-1 font-mono text-sm"
          value={bookingReference}
          onChange={(e) => onBookingReference(e.target.value)}
          placeholder="e.g. TBE-MAN-0001"
        />
      </label>
      <div className="rounded bg-slate-50 p-3 text-xs text-slate-500">
        Channel stamp on submit:{' '}
        <span className="inline-flex rounded bg-amber-100 px-2 py-0.5 font-semibold text-amber-900">
          Manual
        </span>
      </div>
    </fieldset>
  );
}

function Step2({
  passengers,
  setPassengers,
  segments,
  setSegments,
}: {
  passengers: Passenger[];
  setPassengers: (p: Passenger[]) => void;
  segments: Segment[];
  setSegments: (s: Segment[]) => void;
}) {
  return (
    <fieldset className="flex flex-col gap-4">
      <legend className="text-sm font-semibold text-slate-900">
        Step 2 — Itinerary & passengers
      </legend>

      <section>
        <header className="mb-2 flex items-center justify-between">
          <h3 className="text-xs font-semibold uppercase tracking-wide text-slate-500">
            Passengers ({passengers.length}/9)
          </h3>
          <button
            type="button"
            className="text-xs text-slate-700 underline disabled:opacity-40"
            disabled={passengers.length >= 9}
            onClick={() =>
              setPassengers([...passengers, { ...EMPTY_PASSENGER }])
            }
          >
            + Add
          </button>
        </header>
        <ul className="flex flex-col gap-2">
          {passengers.map((p, i) => (
            <li
              key={i}
              className="grid grid-cols-6 gap-2 rounded border border-slate-200 p-2 text-xs"
            >
              <select
                className="rounded border border-slate-300 px-1 py-1"
                value={p.type}
                onChange={(e) =>
                  setPassengers(
                    passengers.map((q, j) =>
                      j === i
                        ? { ...q, type: e.target.value as PassengerType }
                        : q,
                    ),
                  )
                }
              >
                <option>Adult</option>
                <option>Child</option>
                <option>Infant</option>
              </select>
              <input
                className="rounded border border-slate-300 px-1 py-1"
                placeholder="First name"
                value={p.firstName}
                onChange={(e) =>
                  setPassengers(
                    passengers.map((q, j) =>
                      j === i ? { ...q, firstName: e.target.value } : q,
                    ),
                  )
                }
              />
              <input
                className="rounded border border-slate-300 px-1 py-1"
                placeholder="Last name"
                value={p.lastName}
                onChange={(e) =>
                  setPassengers(
                    passengers.map((q, j) =>
                      j === i ? { ...q, lastName: e.target.value } : q,
                    ),
                  )
                }
              />
              <input
                type="date"
                className="rounded border border-slate-300 px-1 py-1"
                value={p.dateOfBirth}
                onChange={(e) =>
                  setPassengers(
                    passengers.map((q, j) =>
                      j === i ? { ...q, dateOfBirth: e.target.value } : q,
                    ),
                  )
                }
              />
              <input
                className="col-span-1 rounded border border-slate-300 px-1 py-1"
                placeholder="Passport (optional)"
                value={p.passport}
                onChange={(e) =>
                  setPassengers(
                    passengers.map((q, j) =>
                      j === i ? { ...q, passport: e.target.value } : q,
                    ),
                  )
                }
              />
              <button
                type="button"
                className="justify-self-end text-xs text-rose-600 underline disabled:opacity-40"
                disabled={passengers.length <= 1}
                onClick={() =>
                  setPassengers(passengers.filter((_, j) => j !== i))
                }
              >
                Remove
              </button>
            </li>
          ))}
        </ul>
      </section>

      <section>
        <header className="mb-2 flex items-center justify-between">
          <h3 className="text-xs font-semibold uppercase tracking-wide text-slate-500">
            Segments
          </h3>
          <button
            type="button"
            className="text-xs text-slate-700 underline"
            onClick={() => setSegments([...segments, { ...EMPTY_SEGMENT }])}
          >
            + Add
          </button>
        </header>
        <ul className="flex flex-col gap-2">
          {segments.map((s, i) => (
            <li
              key={i}
              className="grid grid-cols-6 gap-2 rounded border border-slate-200 p-2 text-xs"
            >
              <input
                className="rounded border border-slate-300 px-1 py-1 font-mono uppercase"
                maxLength={3}
                placeholder="LHR"
                value={s.origin}
                onChange={(e) =>
                  setSegments(
                    segments.map((t, j) =>
                      j === i ? { ...t, origin: e.target.value } : t,
                    ),
                  )
                }
              />
              <input
                className="rounded border border-slate-300 px-1 py-1 font-mono uppercase"
                maxLength={3}
                placeholder="JFK"
                value={s.destination}
                onChange={(e) =>
                  setSegments(
                    segments.map((t, j) =>
                      j === i ? { ...t, destination: e.target.value } : t,
                    ),
                  )
                }
              />
              <input
                type="datetime-local"
                className="rounded border border-slate-300 px-1 py-1"
                value={s.departure}
                onChange={(e) =>
                  setSegments(
                    segments.map((t, j) =>
                      j === i ? { ...t, departure: e.target.value } : t,
                    ),
                  )
                }
              />
              <input
                type="datetime-local"
                className="rounded border border-slate-300 px-1 py-1"
                value={s.arrival}
                onChange={(e) =>
                  setSegments(
                    segments.map((t, j) =>
                      j === i ? { ...t, arrival: e.target.value } : t,
                    ),
                  )
                }
              />
              <input
                className="rounded border border-slate-300 px-1 py-1"
                placeholder="Carrier / supplier"
                value={s.carrier}
                onChange={(e) =>
                  setSegments(
                    segments.map((t, j) =>
                      j === i ? { ...t, carrier: e.target.value } : t,
                    ),
                  )
                }
              />
              <button
                type="button"
                className="justify-self-end text-xs text-rose-600 underline disabled:opacity-40"
                disabled={segments.length <= 1}
                onClick={() =>
                  setSegments(segments.filter((_, j) => j !== i))
                }
              >
                Remove
              </button>
            </li>
          ))}
        </ul>
      </section>
    </fieldset>
  );
}

function Step3(props: {
  baseFareAmount: string;
  setBaseFareAmount: (v: string) => void;
  surchargeAmount: string;
  setSurchargeAmount: (v: string) => void;
  taxAmount: string;
  setTaxAmount: (v: string) => void;
  currency: string;
  setCurrency: (v: string) => void;
  customerId: string;
  setCustomerId: (v: string) => void;
  customerName: string;
  setCustomerName: (v: string) => void;
  customerEmail: string;
  setCustomerEmail: (v: string) => void;
  customerPhone: string;
  setCustomerPhone: (v: string) => void;
  agencyId: string;
  setAgencyId: (v: string) => void;
  notes: string;
  setNotes: (v: string) => void;
}) {
  const gross =
    Number(props.baseFareAmount || 0) +
    Number(props.surchargeAmount || 0) +
    Number(props.taxAmount || 0);

  return (
    <fieldset className="flex flex-col gap-4">
      <legend className="text-sm font-semibold text-slate-900">
        Step 3 — Pricing & customer
      </legend>

      <section className="grid grid-cols-4 gap-3 text-xs">
        <NumField
          label="Base fare"
          value={props.baseFareAmount}
          onChange={props.setBaseFareAmount}
        />
        <NumField
          label="Surcharge"
          value={props.surchargeAmount}
          onChange={props.setSurchargeAmount}
        />
        <NumField
          label="Tax"
          value={props.taxAmount}
          onChange={props.setTaxAmount}
        />
        <label className="flex flex-col gap-1">
          <span className="font-medium text-slate-700">Currency</span>
          <input
            className="rounded border border-slate-300 px-2 py-1 font-mono uppercase"
            maxLength={3}
            value={props.currency}
            onChange={(e) => props.setCurrency(e.target.value)}
          />
        </label>
      </section>
      <div className="rounded bg-slate-50 p-3 text-sm text-slate-700">
        Gross total (auto):{' '}
        <span className="font-mono font-semibold tabular-nums">
          {props.currency.toUpperCase()} {gross.toFixed(2)}
        </span>
      </div>

      <section className="grid grid-cols-2 gap-3 text-xs">
        <label className="flex flex-col gap-1">
          <span className="font-medium text-slate-700">Customer name</span>
          <input
            className="rounded border border-slate-300 px-2 py-1"
            value={props.customerName}
            onChange={(e) => props.setCustomerName(e.target.value)}
          />
        </label>
        <label className="flex flex-col gap-1">
          <span className="font-medium text-slate-700">Customer email</span>
          <input
            type="email"
            className="rounded border border-slate-300 px-2 py-1"
            value={props.customerEmail}
            onChange={(e) => props.setCustomerEmail(e.target.value)}
          />
        </label>
        <label className="flex flex-col gap-1">
          <span className="font-medium text-slate-700">
            Customer phone (optional)
          </span>
          <input
            className="rounded border border-slate-300 px-2 py-1"
            value={props.customerPhone}
            onChange={(e) => props.setCustomerPhone(e.target.value)}
          />
        </label>
        <label className="flex flex-col gap-1">
          <span className="font-medium text-slate-700">
            Customer id (optional)
          </span>
          <input
            className="rounded border border-slate-300 px-2 py-1 font-mono"
            value={props.customerId}
            onChange={(e) => props.setCustomerId(e.target.value)}
          />
        </label>
        <label className="col-span-2 flex flex-col gap-1">
          <span className="font-medium text-slate-700">
            Agency id (optional)
          </span>
          <input
            className="rounded border border-slate-300 px-2 py-1 font-mono"
            value={props.agencyId}
            onChange={(e) => props.setAgencyId(e.target.value)}
          />
        </label>
        <label className="col-span-2 flex flex-col gap-1">
          <span className="font-medium text-slate-700">Notes (optional)</span>
          <textarea
            className="rounded border border-slate-300 px-2 py-1"
            rows={2}
            value={props.notes}
            onChange={(e) => props.setNotes(e.target.value)}
          />
        </label>
      </section>

      <div className="rounded bg-emerald-50 p-3 text-xs text-emerald-900">
        On submit the booking will be stamped{' '}
        <span className="font-semibold">Confirmed</span> immediately.
      </div>
    </fieldset>
  );
}

function NumField({
  label,
  value,
  onChange,
}: {
  label: string;
  value: string;
  onChange: (v: string) => void;
}) {
  return (
    <label className="flex flex-col gap-1">
      <span className="font-medium text-slate-700">{label}</span>
      <input
        type="number"
        step="0.01"
        min="0"
        className="rounded border border-slate-300 px-2 py-1 text-right tabular-nums"
        value={value}
        onChange={(e) => onChange(e.target.value)}
      />
    </label>
  );
}
