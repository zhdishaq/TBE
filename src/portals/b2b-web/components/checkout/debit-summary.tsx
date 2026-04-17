// Plan 05-02 Task 3 — DebitSummary.
//
// Client component rendered by /checkout/confirm when
// `balance >= gross`. Shows the wallet-debit copy + a single CTA that POSTs
// the booking payload to the gateway. On 202 -> /checkout/success?booking=...
// On 409 (insufficient funds discovered at reserve-time — race condition) ->
// swap itself for <InsufficientFundsPanel /> in place; on 403 -> toast + redirect.
//
// UI-SPEC §Confirm Page §Debit Summary.
'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { formatMoney } from '@/lib/format-money';
import { InsufficientFundsPanel } from '@/components/checkout/insufficient-funds-panel';

export interface DebitSummaryProps {
  gross: number;
  currency: string;
  balance: number;
  onConfirm: string;
  payload: Record<string, unknown>;
  roles: string[];
  adminEmail?: string;
}

export function DebitSummary({
  gross,
  currency,
  balance,
  onConfirm,
  payload,
  roles,
  adminEmail,
}: DebitSummaryProps) {
  const router = useRouter();
  const [submitting, setSubmitting] = useState(false);
  const [raceFunds, setRaceFunds] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleConfirm() {
    setSubmitting(true);
    setError(null);
    try {
      const res = await fetch(onConfirm, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      });
      if (res.status === 202) {
        const body = (await res.json().catch(() => ({}))) as { bookingId?: string };
        router.push(`/checkout/success?booking=${encodeURIComponent(body.bookingId ?? '')}`);
        return;
      }
      if (res.status === 409) {
        setRaceFunds(true);
        return;
      }
      if (res.status === 403) {
        setError('You are not permitted to create bookings with this configuration.');
        return;
      }
      setError(`Booking failed (${res.status}). Please try again.`);
    } catch {
      setError('Network error. Please try again.');
    } finally {
      setSubmitting(false);
    }
  }

  if (raceFunds) {
    return (
      <InsufficientFundsPanel
        gross={gross}
        balance={balance}
        currency={currency}
        roles={roles}
        adminEmail={adminEmail}
      />
    );
  }

  return (
    <section className="rounded-lg border border-zinc-200 bg-background p-6 dark:border-zinc-700">
      <h2 className="text-2xl font-semibold tabular-nums">
        Debit from wallet: {formatMoney(gross, currency)}
      </h2>
      <p className="mt-2 text-sm text-muted-foreground">
        This booking will reserve {formatMoney(gross, currency)} from your agency
        wallet. The reservation commits automatically when the ticket is issued
        by the GDS.
      </p>
      {error && (
        <p role="status" className="mt-2 text-sm text-red-700">
          {error}
        </p>
      )}
      <button
        type="button"
        onClick={handleConfirm}
        disabled={submitting}
        className="mt-4 inline-flex h-10 items-center rounded-md bg-indigo-600 px-4 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50"
      >
        {submitting
          ? 'Confirming…'
          : `Confirm booking — debit ${formatMoney(gross, currency)}`}
      </button>
    </section>
  );
}
