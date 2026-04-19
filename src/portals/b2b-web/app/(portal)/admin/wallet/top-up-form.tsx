// Plan 05-05 Task 4 — TopUpForm (two-phase Stripe confirmPayment flow).
//
// Phase 1: submit amount → POST /api/wallet/top-up/intent → get clientSecret.
// Phase 2: render <WalletPaymentElementWrapper> with <PaymentElement/> inside,
// enable the dynamic "Pay £{amount} to top up" submit button, call
// `stripe.confirmPayment({ elements, confirmParams: { return_url: ... } })`.
// Backend problem+json (400 out-of-range) is parsed into the friendly inline
// message "Top-up must be between £{min} and £{max}. You requested £{requested}."
// per UI-SPEC §11 line 398. Client-side zod clamp is the UX hint; D-40 cap is
// enforced server-side (curl/Postman cannot bypass).

'use client';

import { useState } from 'react';
import { useForm, FormProvider } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useElements, useStripe, PaymentElement } from '@stripe/react-stripe-js';
import { WalletPaymentElementWrapper } from '@/components/wallet/wallet-payment-element-wrapper';

// D-40 top-up cap: £10 minimum, £50,000 maximum per admin-configured range.
const topUpSchema = z.object({
  amount: z.coerce
    .number({ message: 'Amount is required' })
    .min(10, 'Top-up must be between £10 and £50 000')
    .max(50000, 'Top-up must be between £10 and £50 000'),
});
// z.coerce.number() has input=unknown, output=number. Use z.input for
// useForm's TFieldValues so the resolver's input type aligns; z.output
// is what handleSubmit receives after coercion.
type TopUpFormInput = z.input<typeof topUpSchema>;
type TopUpFormValues = z.output<typeof topUpSchema>;

interface ProblemBody {
  type?: string;
  allowedRange?: { min: number; max: number; currency: string };
  requested?: number;
}

export function TopUpForm(): React.ReactElement {
  const methods = useForm<TopUpFormInput, unknown, TopUpFormValues>({
    resolver: zodResolver(topUpSchema),
    defaultValues: { amount: 0 },
  });
  const { register, handleSubmit, watch, formState: { errors } } = methods;
  const [clientSecret, setClientSecret] = useState<string | null>(null);
  const [apiError, setApiError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  // watch returns the pre-coercion input (unknown from z.coerce.number).
  // Normalise to number for downstream consumers that expect a numeric amount.
  const rawAmount = watch('amount');
  const coerced = Number(rawAmount);
  const watchedAmount = Number.isFinite(coerced) ? coerced : 0;

  const onSubmit = handleSubmit(async ({ amount }) => {
    setApiError(null);
    setSubmitting(true);
    try {
      const resp = await fetch('/api/wallet/top-up/intent', {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify({ amount }),
      });
      const ct = resp.headers.get('content-type') ?? '';
      if (!resp.ok) {
        if (ct.includes('application/problem+json')) {
          const prob = (await resp.json()) as ProblemBody;
          const min = prob.allowedRange?.min ?? 10;
          const max = prob.allowedRange?.max ?? 50000;
          const requested = prob.requested ?? amount;
          // Use non-breaking-space group separator "50 000" (NOT comma-separated
          // "50,000") so the UX wording matches the zod validation message.
          const fmt = (n: number) =>
            n.toLocaleString('en-GB', { useGrouping: true }).replace(/,/g, ' ');
          setApiError(
            `Top-up must be between £${fmt(min)} and £${fmt(max)}. You requested £${requested}.`,
          );
          return;
        }
        setApiError('Top-up failed. Please try again.');
        return;
      }
      const body = (await resp.json()) as { clientSecret: string };
      setClientSecret(body.clientSecret);
    } finally {
      setSubmitting(false);
    }
  });

  return (
    <FormProvider {...methods}>
      <form onSubmit={onSubmit} className="space-y-4">
        <div className="space-y-1">
          <label htmlFor="topup-amount" className="text-sm font-medium">
            Amount (£)
          </label>
          <input
            id="topup-amount"
            type="number"
            step="0.01"
            min={10}
            max={50000}
            {...register('amount')}
            className="w-full rounded-md border px-3 py-2 text-sm tabular-nums"
            aria-invalid={errors.amount ? 'true' : 'false'}
          />
          {errors.amount && (
            <p role="alert" className="text-sm text-red-600">
              {errors.amount.message}
            </p>
          )}
          {apiError && (
            <p role="alert" className="text-sm text-red-600">
              {apiError}
            </p>
          )}
        </div>

        {/* Phase-2 rendering: Stripe PaymentElement appears after clientSecret. */}
        <WalletPaymentElementWrapper clientSecret={clientSecret}>
          <StripeConfirmBlock amount={watchedAmount} submitting={submitting} />
        </WalletPaymentElementWrapper>

        {/* Phase-1 submit (only shown when no clientSecret yet). */}
        {!clientSecret && (
          <button
            type="submit"
            disabled={submitting}
            className="h-11 rounded-md bg-indigo-600 px-4 text-sm font-semibold text-white hover:bg-indigo-700 disabled:opacity-50"
          >
            {submitting
              ? 'Preparing...'
              : `Pay £${Number(watchedAmount || 0).toFixed(2)} to top up`}
          </button>
        )}
      </form>
    </FormProvider>
  );
}

// Phase-2 block: renders the Stripe PaymentElement + confirm button. Split
// out so it can safely call useStripe/useElements (which require being a
// descendant of <Elements>).
function StripeConfirmBlock({
  amount,
  submitting,
}: {
  amount: number;
  submitting: boolean;
}): React.ReactElement {
  const stripe = useStripe();
  const elements = useElements();

  const onConfirm = async () => {
    if (!stripe || !elements) return;
    // HI-02 — Stripe.js requires an absolute URL. A relative path throws
    // IntegrationError at confirmPayment time. basePath is '/b2b' per
    // next.config.mjs, so we prepend origin + basePath.
    const returnUrl = `${window.location.origin}/b2b/admin/wallet?success=1`;
    await stripe.confirmPayment({
      elements,
      confirmParams: { return_url: returnUrl },
    });
  };

  return (
    <div className="space-y-3">
      <PaymentElement />
      <button
        type="button"
        onClick={onConfirm}
        disabled={!stripe || !elements || submitting}
        className="h-11 rounded-md bg-indigo-600 px-4 text-sm font-semibold text-white hover:bg-indigo-700 disabled:opacity-50"
      >
        Pay £{Number(amount || 0).toFixed(2)} to top up
      </button>
    </div>
  );
}
