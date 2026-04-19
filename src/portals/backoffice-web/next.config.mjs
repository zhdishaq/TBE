/** @type {import('next').NextConfig} */
//
// Phase 6 Plan 06-01 — backoffice portal CSP, tighter than b2b-web.
//
// No Stripe anywhere: the backoffice does not run the top-up PaymentIntent
// flow. Ops-finance reads reconciliation summaries that are already captured
// by PaymentService; no card data ever touches this portal (SAQ-A scope
// boundary preserved — Pitfall 5 fork decision).
//
// Grep-check per 06-01-PLAN acceptance criteria: `js.stripe.com` MUST NOT
// appear anywhere in this file. If it does, the SAQ-A scope analysis in
// 06-VALIDATION.md is invalidated.
//
// Source: fork of src/portals/b2b-web/next.config.mjs minus walletCsp,
// basePath changed to `/backoffice` per PATTERNS.md Pattern M.

const standardSecurityHeaders = [
  { key: 'X-Content-Type-Options', value: 'nosniff' },
  { key: 'Referrer-Policy', value: 'strict-origin-when-cross-origin' },
  { key: 'X-Frame-Options', value: 'DENY' },
];

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
  basePath: '/backoffice',
  output: 'standalone',
  async headers() {
    return [
      {
        // Single catch-all — no per-route variation. Backoffice has no
        // Stripe surface so the standardCsp applies everywhere.
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
