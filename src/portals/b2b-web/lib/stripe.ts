// Memoized loadStripe (B2C-06, Pitfall 5).
//
// The `loadStripe` Promise MUST be created once per page lifetime —
// never inside a React render. Re-mounting <Elements> with a new
// promise re-downloads Stripe.js and re-creates the IFRAME session,
// breaking PaymentElement tokenisation mid-flow.
//
// Source: 04-RESEARCH §Code Examples — "Memoised loadStripe".
// Pitfall 5: Stripe Elements only on the payment step; loadStripe
// MUST NOT run in any other page's tree.

import type { Stripe } from '@stripe/stripe-js';
import { loadStripe } from '@stripe/stripe-js';

let _p: Promise<Stripe | null> | undefined;

/**
 * Returns the (single) Stripe instance promise for this page lifetime.
 * On the server this returns a resolved-null promise if the key is
 * missing; callers should pass the result to <Elements>, which
 * gracefully handles `null`.
 */
export const getStripe = (): Promise<Stripe | null> => {
  return (_p ??= loadStripe(process.env.NEXT_PUBLIC_STRIPE_PK!));
};
