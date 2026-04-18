// Memoized loadStripe (B2C-06, Pitfall 5) — module-scope singleton.
//
// Plan 05-05 Task 1 (05-PATTERNS.md §Stripe Bootstrap):
//   loadStripe MUST be called exactly once per page lifetime. Re-mounting
//   <Elements> with a new promise re-downloads Stripe.js and re-creates
//   the IFRAME session, breaking PaymentElement tokenisation mid-flow.
//
// Pitfall 5 preservation: importing this module triggers loadStripe —
// that is INTENDED. `lib/stripe.ts` is imported ONLY by
// `components/wallet/wallet-payment-element-wrapper.tsx`, which in
// turn is imported ONLY by the /admin/wallet top-up form. Every other
// portal page (search, checkout, dashboard, bookings) is structurally
// prevented from pulling this module by the grep guard in
// 05-05-PLAN.md §verification block (and the route-scoped CSP in
// next.config.mjs is the browser-side defence-in-depth layer).
//
// Both `getStripe()` (legacy B2C call site) and `stripePromise`
// (new Plan 05-05 wallet wrapper) resolve to the SAME Promise object
// — importing from two places still yields one loadStripe call.
//
// Source: 04-RESEARCH §Code Examples — "Memoised loadStripe";
// 05-PATTERNS.md §Stripe Bootstrap.

import type { Stripe } from '@stripe/stripe-js';
import { loadStripe } from '@stripe/stripe-js';

function resolveKey(): string {
  // Support both env var names during the 05-05 rollout: legacy
  // `NEXT_PUBLIC_STRIPE_PK` (used by B2C Plan 04-02 and checkout specs)
  // and the 05-PATTERNS-canonical `NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY`.
  // Either is fine — pick whichever is set first.
  return (
    process.env.NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY ??
    process.env.NEXT_PUBLIC_STRIPE_PK ??
    ''
  );
}

/**
 * Module-scope memoised Stripe promise. Created once at module-load
 * time — every component importing `stripePromise` receives the SAME
 * Promise object, so `<Elements stripe={stripePromise}>` never triggers
 * a re-download of Stripe.js on re-render.
 */
export const stripePromise: Promise<Stripe | null> = loadStripe(resolveKey());

/**
 * Returns the (single) Stripe instance promise for this page lifetime.
 * Kept for backward-compat with Plan 04-02 B2C checkout. Always returns
 * the same `stripePromise` object.
 */
export const getStripe = (): Promise<Stripe | null> => stripePromise;
