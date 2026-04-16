// Edge-safe Auth.js v5 subset (Pitfall 3).
//
// This file is imported by middleware.ts and MUST NOT pull any Node-only
// modules (no `crypto`, no DB clients, no filesystem access). Only the
// provider metadata + session strategy + minimal `authorized` callback
// live here. The full `jwt`/`session` callbacks and `refreshAccessToken`
// implementation live in lib/auth.ts (Node runtime).
//
// Source: Auth.js v5 docs "Split Config" pattern + 04-RESEARCH Pitfall 3.

import type { NextAuthConfig } from 'next-auth';
import Keycloak from 'next-auth/providers/keycloak';
import NextAuth from 'next-auth';

export const authConfig = {
  providers: [
    Keycloak({
      clientId: process.env.KEYCLOAK_B2C_CLIENT_ID!,
      clientSecret: process.env.KEYCLOAK_B2C_CLIENT_SECRET!,
      issuer: process.env.KEYCLOAK_B2C_ISSUER!,
    }),
  ],
  session: { strategy: 'jwt' },
  pages: {
    signIn: '/login',
  },
  callbacks: {
    // Minimal, edge-safe gate. Full redirect logic lives in middleware.ts
    // which wraps this via the exported `auth` helper below.
    authorized({ auth, request }) {
      const { pathname } = request.nextUrl;
      const isProtected =
        pathname.startsWith('/bookings') || pathname.startsWith('/checkout');
      if (!isProtected) return true;
      return !!auth; // truthy when a session exists
    },
  },
} satisfies NextAuthConfig;

// Edge-safe re-export used exclusively by middleware.ts. Do NOT import
// from lib/auth.ts in edge code — it pulls Node crypto.
export const { auth } = NextAuth(authConfig);
