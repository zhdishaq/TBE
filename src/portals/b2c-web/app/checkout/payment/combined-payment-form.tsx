'use client';

// Combined Stripe PaymentElement for Trip Builder baskets (Plan 04-04 /
// PKG-02 / D-08 / D-10).
//
// D-08 single-PaymentIntent invariant: ONE `<Elements>` tree wrapping
// ONE `<PaymentElement>` driven by ONE `clientSecret`. This component
// MUST NOT accept, render, or reference a second client secret — a
// `flightClientSecret`/`hotelClientSecret` shape is the forbidden two-PI
// strategy.
//
// `confirmPayment` fires EXACTLY ONCE on click with `return_url =
// /checkout/processing?ref=basket-{id}`. That single call triggers the
// authorize; the sequential partial captures (flight final_capture=false
// → hotel final_capture=true) run server-side inside
// BasketPaymentOrchestrator. The copy explicitly tells the customer
// they'll see ONE charge on their statement (D-08 single-statement
// disclosure) — any split-statement language is forbidden.

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

export interface CombinedPaymentFormProps {
  basketId: string;
  clientSecret: string;
  amount: number;
  currency: string;
}

function buildBasketReturnUrl(basketId: string): string {
  const origin =
    typeof window !== 'undefined' && window.location?.origin
      ? window.location.origin
      : '';
  const safeId = encodeURIComponent(basketId);
  return `${origin}/checkout/processing?ref=basket-${safeId}`;
}

function PayForm({
  basketId,
  amount,
  currency,
}: Omit<CombinedPaymentFormProps, 'clientSecret'>) {
  const stripe = useStripe() as Stripe | null;
  const elements = useElements() as StripeElements | null;
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const onPay = useCallback(async () => {
    if (!stripe || !elements) return;
    setSubmitting(true);
    setError(null);
    try {
      // Validate fields before confirm (Stripe docs recommend this order).
      if (typeof elements.submit === 'function') {
        const submitRes = await elements.submit();
        const maybeError = (submitRes as { error?: { message?: string } } | undefined)?.error;
        if (maybeError?.message) {
          setError(maybeError.message);
          setSubmitting(false);
          return;
        }
      }

      // Exactly ONE confirmPayment — D-08. A second call for a hotel PI
      // would split the customer's statement into two charges, which the
      // product spec explicitly forbids.
      const result = await stripe.confirmPayment({
        elements,
        confirmParams: {
          return_url: buildBasketReturnUrl(basketId),
        },
      });

      const resultError = (result as { error?: { message?: string } } | undefined)?.error;
      if (resultError?.message) {
        setError(resultError.message);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Payment could not be confirmed.');
    } finally {
      setSubmitting(false);
    }
  }, [stripe, elements, basketId]);

  const label = `Pay ${formatMoney(amount, currency)}`;

  return (
    <div className="flex flex-col gap-4" data-testid="combined-payment-form">
      <PaymentElement />
      <p className="text-xs text-muted-foreground">
        You&apos;ll see ONE charge on your statement for the total trip amount.
      </p>
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

export function CombinedPaymentForm(props: CombinedPaymentFormProps) {
  const { clientSecret } = props;
  // getStripe() is memoised (Pitfall 5) — safe to call on every render.
  const stripePromise = getStripe();

  return (
    <Elements stripe={stripePromise} options={{ clientSecret }}>
      <PayForm basketId={props.basketId} amount={props.amount} currency={props.currency} />
    </Elements>
  );
}
