// Full Auth.js v5 config (Node runtime).
//
// Extends auth.config.ts with JWT callbacks that require Node APIs (fetch
// against Keycloak token endpoint, base64 handling). Exposes the set of
// helpers consumed by server components and route handlers.
//
// Source: 04-RESEARCH Pattern 1 + Pitfall 2 (refresh rotation) + Pitfall 3.

import NextAuth from 'next-auth';
import type { JWT } from 'next-auth/jwt';
import { authConfig } from '@/auth.config';

interface RefreshedToken extends JWT {
  access_token: string;
  refresh_token: string;
  expires_at: number;
  email_verified: boolean;
  error?: 'RefreshAccessTokenError';
}

async function refreshAccessToken(token: JWT): Promise<JWT> {
  try {
    const issuer = process.env.KEYCLOAK_B2C_ISSUER;
    if (!issuer) throw new Error('KEYCLOAK_B2C_ISSUER not set');
    const refresh = (token as RefreshedToken).refresh_token;
    if (!refresh) throw new Error('No refresh_token on JWT');

    const response = await fetch(`${issuer}/protocol/openid-connect/token`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams({
        grant_type: 'refresh_token',
        client_id: process.env.KEYCLOAK_B2C_CLIENT_ID!,
        client_secret: process.env.KEYCLOAK_B2C_CLIENT_SECRET!,
        refresh_token: refresh,
      }),
      cache: 'no-store',
    });

    const refreshed = (await response.json()) as {
      access_token?: string;
      refresh_token?: string;
      expires_in?: number;
      error?: string;
    };
    if (!response.ok || !refreshed.access_token) {
      throw new Error(refreshed.error ?? 'Refresh failed');
    }

    return {
      ...token,
      access_token: refreshed.access_token,
      refresh_token: refreshed.refresh_token ?? refresh,
      expires_at: Math.floor(Date.now() / 1000) + (refreshed.expires_in ?? 300),
      error: undefined,
    };
  } catch (err) {
    // Per Auth.js v5 docs — surface an error on the token so the client
    // can force re-auth. Do NOT throw — that would crash the handler.
    return { ...token, error: 'RefreshAccessTokenError' as const };
  }
}

export const { handlers, auth, signIn, signOut } = NextAuth({
  ...authConfig,
  callbacks: {
    ...authConfig.callbacks,
    async jwt({ token, account, profile }) {
      // Initial sign-in — copy Keycloak tokens onto our JWT.
      if (account) {
        token.access_token = account.access_token;
        token.refresh_token = account.refresh_token;
        token.expires_at = account.expires_at;
        // Keycloak puts `email_verified` on the ID token profile.
        token.email_verified =
          ((profile as { email_verified?: boolean } | null | undefined)
            ?.email_verified) ?? false;
        return token;
      }
      // Refresh-token rotation (Pitfall 2): if access token is within
      // 60 seconds of expiry, force a refresh now.
      const expiresAt = token.expires_at as number | undefined;
      if (expiresAt && Date.now() / 1000 > expiresAt - 60) {
        return await refreshAccessToken(token);
      }
      return token;
    },
    async session({ session, token }) {
      session.access_token = token.access_token as string | undefined;
      session.email_verified = Boolean(token.email_verified);
      session.expires_at = token.expires_at as number | undefined;
      return session;
    },
  },
});
