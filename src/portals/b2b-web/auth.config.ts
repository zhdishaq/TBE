// Edge-safe Auth.js v5 subset for the B2B Agent Portal (Pitfall 3 inherited).
//
// This file is imported by middleware.ts and MUST NOT pull any Node-only
// modules (no `crypto`, no DB clients, no filesystem access). Only the
// provider metadata + session strategy + minimal `authorized` callback
// live here. The full `jwt`/`session` callbacks and `refreshAccessToken`
// implementation live in lib/auth.ts (Node runtime).
//
// Phase 5 Plan 05-00 deltas vs b2c-web/auth.config.ts:
//   1. `KEYCLOAK_B2C_*` env vars → `KEYCLOAK_B2B_*` (tbe-b2b realm per D-32).
//   2. Protected-path list expanded: adds `/dashboard`, `/admin`, `/search`.
//   3. Session cookie renamed to `__Secure-tbe-b2b.session-token` (Pitfall 19 /
//      mitigation T-05-00-01) — prevents cookie collision with the b2c portal
//      when both are served from the same apex domain in prod.
//
// Source: Auth.js v5 docs "Split Config" pattern + 05-PATTERNS.md §15.

import type { NextAuthConfig } from 'next-auth';
import Keycloak from 'next-auth/providers/keycloak';
import NextAuth from 'next-auth';

const PROTECTED_PREFIXES = [
  '/dashboard',
  '/bookings',
  '/checkout',
  '/admin',
  '/search',
] as const;

export const authConfig = {
  providers: [
    Keycloak({
      clientId: process.env.KEYCLOAK_B2B_CLIENT_ID!,
      clientSecret: process.env.KEYCLOAK_B2B_CLIENT_SECRET!,
      issuer: process.env.KEYCLOAK_B2B_ISSUER!,
    }),
  ],
  session: { strategy: 'jwt' },
  // Pitfall 19 mitigation T-05-00-01: distinct cookie name per portal so the
  // B2C and B2B browser sessions cannot overwrite each other on a shared
  // apex domain. Name MUST contain `tbe-b2b` and MUST NOT contain `tbe-b2c`
  // (grep-verifiable in 05-00-PLAN acceptance criteria).
  cookies: {
    sessionToken: {
      name: '__Secure-tbe-b2b.session-token',
      options: {
        httpOnly: true,
        sameSite: 'lax',
        path: '/',
        secure: true,
      },
    },
  },
  pages: {
    signIn: '/login',
  },
  callbacks: {
    // Minimal, edge-safe gate. Full redirect logic lives in middleware.ts
    // which wraps this via the exported `auth` helper below.
    authorized({ auth, request }) {
      const { pathname } = request.nextUrl;
      const isProtected = PROTECTED_PREFIXES.some((p) =>
        pathname.startsWith(p),
      );
      if (!isProtected) return true;
      return !!auth; // truthy when a session exists
    },
  },
} satisfies NextAuthConfig;

// Edge-safe re-export used exclusively by middleware.ts. Do NOT import
// from lib/auth.ts in edge code — it pulls Node crypto.
export const { auth } = NextAuth(authConfig);
