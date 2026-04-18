/** @type {import('next').NextConfig} */
//
// Source: fork of ui/starterKit/next.config.mjs + CSP additions per
// .planning/phases/04-b2c-portal-customer-facing/04-RESEARCH.md Pitfall 16.
//
// The single Content-Security-Policy header below whitelists exactly the
// Stripe CDN + API endpoints required for Stripe Elements (Pattern 2).
// Everything else defaults to 'self'. If a new third-party is added, it
// MUST be added here explicitly — never relax to '*'.

const cspDirectives = [
  "default-src 'self'",
  "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://js.stripe.com",
  "style-src 'self' 'unsafe-inline'",
  "img-src 'self' data: https:",
  "font-src 'self' data:",
  "frame-src https://js.stripe.com https://hooks.stripe.com",
  "connect-src 'self' https://api.stripe.com https://js.stripe.com",
  "form-action 'self'",
].join('; ');

const nextConfig = {
  output: 'standalone',
  async headers() {
    return [
      {
        source: '/:path*',
        headers: [
          { key: 'Content-Security-Policy', value: cspDirectives },
          { key: 'X-Content-Type-Options', value: 'nosniff' },
          { key: 'Referrer-Policy', value: 'strict-origin-when-cross-origin' },
          { key: 'X-Frame-Options', value: 'DENY' },
        ],
      },
    ];
  },
};

export default nextConfig;
