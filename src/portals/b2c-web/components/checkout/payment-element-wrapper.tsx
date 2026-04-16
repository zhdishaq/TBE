// Stripe PaymentElement wrapper (B2C-06, D-08, Pitfall 5/6).
//
// Wraps @stripe/react-stripe-js <Elements> around <PaymentElement>, then
// calls `stripe.confirmPayment({ confirmParams: { return_url } })` when
// the Pay button is clicked. The return_url lands on /checkout/processing
// with the bookingId — but success is ALWAYS driven by the saga-poll
// path, not by Stripe's redirect landing (Pitfall 6 / FLTB-D-12).
//
// COMP-01 / T-04-02-01: card data goes directly to the Stripe-hosted
// IFRAME; the portal code never touches PAN/CVC.

'use client';

import {
  Elements,
  PaymentElement,
  useElements,
  useStripe,
} from '@stripe/react-stripe-js';
import type { Stripe, StripeElements } from '@stripe/stripe-js';
import { useCallback, useState } from 'react';

import { getStripe } from '@/lib/stripe';
import { formatMoney } from '@/lib/formatters';
import { buildCheckoutRef, type CheckoutRefKind } from '@/lib/checkout-ref';

interface PaymentElementWrapperProps {
  amount: number;
  currency: string;
  bookingId: string;
  clientSecret: string;
  /**
   * Unified B5 ref kind used to build the return_url. Defaults to "flight"
   * for back-compat with the 04-02 flight-only callers. 04-04 hotel/car
   * flows pass "hotel" / "car" so the processing page polls the correct
   * status endpoint.
   */
  refKind?: CheckoutRefKind;
}

function buildReturnUrl(bookingId: string, kind: CheckoutRefKind): string {
  const origin =
    typeof window !== 'undefined' && window.location?.origin
      ? window.location.origin
      : '';
  const ref = encodeURIComponent(buildCheckoutRef(kind, bookingId));
  return `${origin}/checkout/processing?ref=${ref}`;
}

function PayForm({
  amount,
  currency,
  bookingId,
  refKind = 'flight',
}: Omit<PaymentElementWrapperProps, 'clientSecret'>) {
  const stripe = useStripe() as Stripe | null;
  const elements = useElements() as StripeElements | null;
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const onPay = useCallback(async () => {
    if (!stripe || !elements) return;
    setSubmitting(true);
    setError(null);
    try {
      // Validate fields first (Stripe docs recommend this before confirm).
      if (typeof elements.submit === 'function') {
        const submitRes = await elements.submit();
        const maybeError = (submitRes as { error?: { message?: string } } | undefined)?.error;
        if (maybeError?.message) {
          setError(maybeError.message);
          setSubmitting(false);
          return;
        }
      }

      const result = await stripe.confirmPayment({
        elements,
        confirmParams: {
          return_url: buildReturnUrl(bookingId, refKind),
        },
      });

      // On 3DS, confirmPayment redirects the browser away — any code
      // reaching this point means either immediate success OR a
      // non-redirect error (e.g. card declined).
      const resultError = (result as { error?: { message?: string } } | undefined)?.error;
      if (resultError?.message) {
        setError(resultError.message);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Payment could not be confirmed.');
    } finally {
      setSubmitting(false);
    }
  }, [stripe, elements, bookingId, refKind]);

  const label = `Pay ${formatMoney(amount, currency)}`;

  return (
    <div className="flex flex-col gap-4">
      <PaymentElement />
      <button
        type="button"
        onClick={onPay}
        disabled={submitting || !stripe || !elements}
        className="inline-flex items-center justify-center rounded-md bg-blue-600 px-4 py-3 text-base font-medium text-white hover:bg-blue-700 disabled:opacity-60"
      >
        {submitting ? 'Processing…' : label}
      </button>
      {error && (
        <p role="alert" className="text-sm text-red-600">
          {error}
        </p>
      )}
    </div>
  );
}

export function PaymentElementWrapper(props: PaymentElementWrapperProps) {
  const { clientSecret } = props;
  // getStripe() is memoised (Pitfall 5) — safe to call on every render.
  const stripePromise = getStripe();

  return (
    <Elements stripe={stripePromise} options={{ clientSecret }}>
      <PayForm
        amount={props.amount}
        currency={props.currency}
        bookingId={props.bookingId}
        refKind={props.refKind}
      />
    </Elements>
  );
}
