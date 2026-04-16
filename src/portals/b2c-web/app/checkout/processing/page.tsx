// /checkout/processing — unified saga-driven status poll (B5 / Plan 04-04).
//
// CRITICAL: this page is the ONLY place we treat a booking as
// "confirmed". Stripe's client-side redirect may land here with
// ?payment_intent=... + ?redirect_status=succeeded — we IGNORE those
// query params as a success signal. Success is derived from polling the
// saga status endpoint until we see a terminal state.
//
// B5 unification: we read `?ref={kind}-{id}` only. Legacy booking= query
// param is removed.
// The poll target varies by kind:
//   - flight → /api/bookings/{id}/status
//   - hotel  → /api/hotel-bookings/{id}/status (parity; the existing
//              hotel controller already exposes GET /{id})
//   - car    → /api/car-bookings/{id}/status
//   - basket → /api/baskets/{id}/status (D-09 terminal set includes
//              `PartiallyConfirmed` which routes to /checkout/success
//              with the ref intact; the success page renders the
//              <PartialFailureBanner>).

'use client';

import { useRouter, useSearchParams } from 'next/navigation';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';

import { CheckoutStepper } from '@/components/checkout/stepper';
import { buildCheckoutRef, parseCheckoutRef, type CheckoutRef } from '@/lib/checkout-ref';

type Status =
  | 'Initiated'
  | 'Authorizing'
  | 'PriceReconfirmed'
  | 'TicketIssued'
  | 'Confirmed'
  | 'PartiallyConfirmed'
  | 'Failed'
  | 'Cancelled'
  | 'Pending';

const POLL_INTERVAL_MS = 2000;
const POLL_CAP_MS = 90_000;

const TERMINAL_SUCCESS: Status[] = ['Confirmed', 'PartiallyConfirmed'];
const TERMINAL_FAILURE: Status[] = ['Failed', 'Cancelled'];

interface StatusDto {
  status: Status;
  bookingReference?: string;
  pnr?: string;
}

type UiState =
  | { kind: 'polling'; status?: Status; elapsedMs: number }
  | { kind: 'failed'; reason: string }
  | { kind: 'timed-out' };

function statusEndpoint(ref: CheckoutRef): string {
  switch (ref.kind) {
    case 'flight':
      return `/api/bookings/${encodeURIComponent(ref.id)}/status`;
    case 'hotel':
      return `/api/hotel-bookings/${encodeURIComponent(ref.id)}`;
    case 'car':
      return `/api/car-bookings/${encodeURIComponent(ref.id)}`;
    case 'basket':
      return `/api/baskets/${encodeURIComponent(ref.id)}`;
  }
}

export default function CheckoutProcessingPage() {
  const router = useRouter();
  const search = useSearchParams();
  const ref = useMemo<CheckoutRef | null>(() => parseCheckoutRef(search.get('ref')), [search]);

  const [state, setState] = useState<UiState>({ kind: 'polling', elapsedMs: 0 });
  const startedAt = useRef<number>(Date.now());
  const mounted = useRef<boolean>(true);

  const poll = useCallback(async () => {
    if (!ref) return;
    try {
      const res = await fetch(statusEndpoint(ref), { cache: 'no-store' });
      if (!res.ok) {
        return; // transient — keep polling
      }
      const body = (await res.json()) as StatusDto;
      if (!mounted.current) return;

      if (TERMINAL_SUCCESS.includes(body.status)) {
        router.push(
          `/checkout/success?ref=${buildCheckoutRef(ref.kind, ref.id)}`,
        );
        return;
      }
      if (TERMINAL_FAILURE.includes(body.status)) {
        setState({
          kind: 'failed',
          reason:
            body.status === 'Cancelled'
              ? 'Booking was cancelled.'
              : 'The booking could not be completed.',
        });
        return;
      }
      setState({
        kind: 'polling',
        status: body.status,
        elapsedMs: Date.now() - startedAt.current,
      });
    } catch {
      // swallow transient errors; next tick will retry
    }
  }, [ref, router]);

  useEffect(() => {
    mounted.current = true;
    if (!ref) {
      setState({ kind: 'failed', reason: 'Missing checkout reference.' });
      return () => {
        mounted.current = false;
      };
    }

    // Kick off an immediate poll, then every POLL_INTERVAL_MS.
    poll();
    const iv = setInterval(() => {
      const elapsed = Date.now() - startedAt.current;
      if (elapsed >= POLL_CAP_MS) {
        setState({ kind: 'timed-out' });
        return;
      }
      poll();
    }, POLL_INTERVAL_MS);

    return () => {
      mounted.current = false;
      clearInterval(iv);
    };
  }, [ref, poll]);

  return (
    <>
      <CheckoutStepper currentStep="payment" />
      <section className="mx-auto flex w-full max-w-xl flex-col items-center px-6 py-16 text-center md:px-10">
        <div
          aria-hidden="true"
          className="h-12 w-12 animate-spin rounded-full border-2 border-blue-600 border-t-transparent"
        />
        <h1 className="mt-6 text-2xl font-semibold">Confirming your booking…</h1>

        {state.kind === 'polling' && (
          <p role="status" className="mt-2 text-sm text-muted-foreground">
            {state.status === 'PriceReconfirmed'
              ? 'Reconfirming fare with the airline…'
              : state.status === 'TicketIssued'
              ? 'Issuing tickets…'
              : state.status === 'Authorizing'
              ? 'Authorising payment…'
              : 'Processing your booking…'}
          </p>
        )}

        {state.kind === 'failed' && (
          <div className="mt-4 w-full rounded-md border border-red-200 bg-red-50 p-4 text-left">
            <p className="text-sm font-medium text-red-800">{state.reason}</p>
            <p className="mt-1 text-sm text-red-700">
              No charge was kept on your card. Please try again or contact support.
            </p>
            <button
              type="button"
              onClick={() => router.push('/flights')}
              className="mt-3 inline-flex items-center justify-center rounded-md bg-red-600 px-4 py-2 text-sm font-medium text-white hover:bg-red-700"
            >
              Back to search
            </button>
          </div>
        )}

        {state.kind === 'timed-out' && (
          <div className="mt-4 w-full rounded-md border border-amber-200 bg-amber-50 p-4 text-left">
            <p className="text-sm font-medium text-amber-900">
              Taking longer than expected — we&apos;ll email you
            </p>
            <p className="mt-1 text-sm text-amber-800">
              Your booking is still being confirmed. Check your inbox or your
              bookings dashboard for the confirmation.
            </p>
            <a
              href="/bookings"
              className="mt-3 inline-flex items-center justify-center rounded-md bg-amber-600 px-4 py-2 text-sm font-medium text-white hover:bg-amber-700"
            >
              View my bookings
            </a>
          </div>
        )}
      </section>
    </>
  );
}
