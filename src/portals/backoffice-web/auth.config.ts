// Edge-safe Auth.js v5 subset for the Backoffice Portal (Pitfall 3 inherited).
//
// This file is imported by middleware.ts and MUST NOT pull any Node-only
// modules (no `crypto`, no DB clients, no filesystem access). Only the
// provider metadata + session strategy + minimal `authorized` callback
// live here. The full `jwt`/`session` callbacks and `refreshAccessToken`
// implementation live in lib/auth.ts (Node runtime).
//
// Phase 6 Plan 06-01 deltas vs b2b-web/auth.config.ts:
//   1. `KEYCLOAK_B2B_*` env vars → `KEYCLOAK_BACKOFFICE_*` (tbe-backoffice
//      realm per D-46).
//   2. `PROTECTED_PREFIXES = ['/']` — the entire portal is staff-only.
//      Role gating per-path happens in middleware.ts (Pattern M).
//   3. Session cookie renamed to `__Secure-tbe-backoffice.session-token`
//      (Pitfall 19 — no cookie collision with b2b-web or b2c-web when all
//      three portals share an apex domain in prod).
//
// Source: Auth.js v5 docs "Split Config" pattern + 06-PATTERNS.md Pattern M.

import type { NextAuthConfig } from 'next-auth';
import Keycloak from 'next-auth/providers/keycloak';
import NextAuth from 'next-auth';

// The backoffice portal is staff-only: every non-login path requires
// authentication. Role-level gating (ops-admin / ops-cs / ops-finance /
// ops-read) lives in middleware.ts, not here.
const PROTECTED_PREFIXES = ['/'] as const;

export const authConfig = {
  providers: [
    Keycloak({
      clientId: process.env.KEYCLOAK_BACKOFFICE_CLIENT_ID!,
      clientSecret: process.env.KEYCLOAK_BACKOFFICE_CLIENT_SECRET!,
      issuer: process.env.KEYCLOAK_BACKOFFICE_ISSUER!,
    }),
  ],
  session: { strategy: 'jwt' },
  // Pitfall 19: per-portal cookie. Name MUST contain `tbe-backoffice` and
  // MUST NOT contain `tbe-b2b` or `tbe-b2c` (grep-verifiable per
  // 06-01-PLAN acceptance criteria).
  cookies: {
    sessionToken: {
      name: '__Secure-tbe-backoffice.session-token',
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
      // Login page itself is the only public path.
      if (pathname.startsWith('/login')) return true;
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
