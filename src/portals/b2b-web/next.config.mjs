/** @type {import('next').NextConfig} */
//
// Phase 5 Plan 05-00 — B2B portal CSP, tightened vs b2c-web.
//
// Mitigation T-05-00-03 (05-00-PLAN.md threat register):
//   - `walletCsp` whitelists Stripe CDN + API endpoints ONLY for /admin/wallet/*
//     where the top-up PaymentIntent flow lives (Plan 05-03).
//   - `standardCsp` applies everywhere else and omits `js.stripe.com` entirely —
//     no other B2B route is allowed to load Stripe.js. This is tighter than the
//     b2c-web policy (which allows Stripe site-wide for the guest-checkout flow).
//
// Pitfall 5 (Stripe): `<Elements>` mount is module-structurally confined to the
// wallet page in Plan 05-03; the CSP is the defense-in-depth layer ensuring a
// stray import elsewhere would be blocked by the browser. Grep-verifiable per
// 05-00-PLAN acceptance criteria.
//
// Source: fork of src/portals/b2c-web/next.config.mjs +
// 05-PATTERNS.md §17 (per-route CSP) + 05-00-PLAN action step 9.

const standardSecurityHeaders = [
  { key: 'X-Content-Type-Options', value: 'nosniff' },
  { key: 'Referrer-Policy', value: 'strict-origin-when-cross-origin' },
  { key: 'X-Frame-Options', value: 'DENY' },
];

const walletCsp = [
  "default-src 'self'",
  "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://js.stripe.com",
  "style-src 'self' 'unsafe-inline'",
  "img-src 'self' data: https:",
  "font-src 'self' data:",
  "frame-src https://js.stripe.com https://hooks.stripe.com",
  "connect-src 'self' https://api.stripe.com https://js.stripe.com",
  "form-action 'self'",
].join('; ');

const standardCsp = [
  "default-src 'self'",
  "script-src 'self' 'unsafe-inline'",
  "style-src 'self' 'unsafe-inline'",
  "img-src 'self' data: https:",
  "font-src 'self' data:",
  "connect-src 'self'",
  "form-action 'self'",
].join('; ');

const nextConfig = {
  async headers() {
    return [
      {
        // Stripe-allowed CSP — wallet top-up only (Plan 05-03).
        // Order matters: Next.js applies the first matching `source` rule; the
        // more specific /admin/wallet/:path* rule must be listed BEFORE the
        // catch-all /:path* below.
        source: '/admin/wallet/:path*',
        headers: [
          { key: 'Content-Security-Policy', value: walletCsp },
          ...standardSecurityHeaders,
        ],
      },
      {
        // Default CSP for every other route — no Stripe, tighter than b2c.
        source: '/:path*',
        headers: [
          { key: 'Content-Security-Policy', value: standardCsp },
          ...standardSecurityHeaders,
        ],
      },
    ];
  },
};

export default nextConfig;
