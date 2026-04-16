// Keycloak Admin API client for the B2C portal.
//
// Pitfall 8 (RESEARCH): resending a verify-email from the user's own B2C
// access token fails with 403 — the Admin endpoint requires the
// realm-management `manage-users` role. We provision a separate client
// (`tbe-b2c-admin`, see infra/keycloak/realm-tbe-b2c.json) and reach it
// here with a `client_credentials` grant. The service-account token is
// cached in-process with a 30-second expiry skew so we do not hit the
// token endpoint on every call.
//
// SECURITY:
//   - This module must NEVER be imported from a `"use client"` file.
//   - The access token MUST NEVER be logged. It grants manage-users on
//     the realm, so leaking it leaks the whole user base.
//   - All fetches run server-side (Node runtime only — see
//     `app/api/auth/resend-verification/route.ts` for the runtime gate).
//
// Source: 04-RESEARCH §Pitfall 8 + 04-01-PLAN action step 5.

// Runtime guard: this module must never be pulled into a client bundle.
// `typeof window !== 'undefined'` is true in the browser runtime only —
// Node + Edge runtimes both leave `window` undefined.
if (typeof window !== 'undefined') {
  throw new Error(
    'lib/keycloak-admin.ts imported into a Client Component — never do that',
  );
}

interface CachedToken {
  accessToken: string;
  /** Absolute ms epoch at which the cached token becomes unsafe to reuse. */
  expiresAtMs: number;
}

let cached: CachedToken | null = null;

/**
 * Acquire a service-account access token for `tbe-b2c-admin` via the
 * client_credentials grant. Cached in-process until 30 seconds before
 * expiry, then refreshed lazily on the next call.
 */
export async function getServiceAccountToken(): Promise<string> {
  const now = Date.now();
  if (cached && cached.expiresAtMs > now + 30_000) {
    return cached.accessToken;
  }

  const issuer = requireEnv('KEYCLOAK_B2C_ISSUER');
  const clientId = requireEnv('KEYCLOAK_B2C_ADMIN_CLIENT_ID');
  const clientSecret = requireEnv('KEYCLOAK_B2C_ADMIN_CLIENT_SECRET');

  const response = await fetch(`${issuer}/protocol/openid-connect/token`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: new URLSearchParams({
      grant_type: 'client_credentials',
      client_id: clientId,
      client_secret: clientSecret,
    }),
    cache: 'no-store',
  });

  if (!response.ok) {
    // NEVER log the body — Keycloak error payloads sometimes echo the
    // client_id / secret back. Bubble a generic error instead.
    throw new Error(
      `Keycloak admin token endpoint returned ${response.status}`,
    );
  }

  const payload = (await response.json()) as {
    access_token?: string;
    expires_in?: number;
  };
  if (!payload.access_token) {
    throw new Error('Keycloak admin token response missing access_token');
  }

  const expiresInSec = payload.expires_in ?? 60;
  cached = {
    accessToken: payload.access_token,
    expiresAtMs: now + expiresInSec * 1000,
  };
  return payload.access_token;
}

/**
 * Call `PUT /admin/realms/tbe-b2c/users/{userSub}/send-verify-email`,
 * which asks Keycloak to re-send the "verify your email" link to the
 * user. Returns on 2xx; throws with a sanitised message otherwise.
 */
export async function sendVerifyEmail(userSub: string): Promise<void> {
  if (!userSub) throw new Error('userSub is required');

  const adminBase = adminApiBase();
  const token = await getServiceAccountToken();

  const response = await fetch(
    `${adminBase}/users/${encodeURIComponent(userSub)}/send-verify-email`,
    {
      method: 'PUT',
      headers: {
        Authorization: `Bearer ${token}`,
        'Content-Type': 'application/json',
      },
      cache: 'no-store',
    },
  );

  if (!response.ok) {
    throw new Error(`send-verify-email returned ${response.status}`);
  }
}

// ---- internals ---------------------------------------------------------

/**
 * Derive the admin API base from `KEYCLOAK_B2C_ISSUER`.
 *
 * Issuer looks like `https://kc.example.com/realms/tbe-b2c`; the Keycloak
 * Admin API for the same realm lives at
 * `https://kc.example.com/admin/realms/tbe-b2c`. Strip the `/realms/…`
 * suffix and insert `/admin` to keep the host identical.
 */
function adminApiBase(): string {
  const issuer = requireEnv('KEYCLOAK_B2C_ISSUER').replace(/\/+$/, '');
  // Regex capture = everything before `/realms/` (the origin + path prefix)
  // then the realm name itself.
  const match = issuer.match(/^(.+?)\/realms\/([^/]+)$/);
  if (!match) {
    throw new Error(
      'KEYCLOAK_B2C_ISSUER must be shaped "…/realms/<realm>"',
    );
  }
  const [, origin, realm] = match;
  return `${origin}/admin/realms/${realm}`;
}

function requireEnv(name: string): string {
  const value = process.env[name];
  if (!value) {
    throw new Error(`${name} is not set`);
  }
  return value;
}

/**
 * Test-only hook: clears the in-memory token cache. Exported for unit
 * tests that want to exercise the refresh path without waiting for the
 * 30-second skew. Not imported by production code.
 */
export function __resetServiceAccountTokenCacheForTests(): void {
  cached = null;
}
