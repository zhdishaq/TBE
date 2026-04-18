// Plan 05-05 Task 1 — WalletPaymentElementWrapper.
//
// Pitfall 5 preservation: <Elements> is mounted ONLY here, and this
// wrapper is imported ONLY by the /admin/wallet top-up form. Route-scoped
// CSP in next.config.mjs allows js.stripe.com on /admin/wallet/:path*
// only — so a regression that pulls this wrapper into another route
// would fail at the browser CSP layer.
//
// Two-phase flow (05-05-PLAN Task 4):
//   1. The form first POSTs { amount } to /api/wallet/top-up/intent and
//      receives a PaymentIntent clientSecret.
//   2. ONLY THEN does the form render this wrapper, passing the
//      clientSecret. Before that, `clientSecret` is null and we render
//      nothing (no <Elements>, no IFRAME).
//
// Memoisation: `stripePromise` from `lib/stripe.ts` is a module-scope
// singleton — re-renders of this wrapper do NOT re-download Stripe.js.
'use client';

import type { ReactNode } from 'react';
import { Elements } from '@stripe/react-stripe-js';
import { stripePromise } from '@/lib/stripe';

export interface WalletPaymentElementWrapperProps {
  clientSecret: string | null;
  children?: ReactNode;
}

export function WalletPaymentElementWrapper({
  clientSecret,
  children,
}: WalletPaymentElementWrapperProps): React.ReactElement | null {
  if (!clientSecret) return null;
  return (
    <Elements
      stripe={stripePromise}
      options={{ clientSecret, appearance: { theme: 'stripe' } }}
    >
      {children}
    </Elements>
  );
}
