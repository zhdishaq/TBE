// Keycloak Admin API client for the B2B Agent Portal.
//
// Pitfall 8 (inherited from 04-RESEARCH): resending verify-email and creating
// sub-agents from a normal B2B access token fails with 403 — the Admin
// endpoints require the realm-management `manage-users` role. We provision a
// separate client (`tbe-b2b-admin`, see infra/keycloak/realm-tbe-b2b.json)
// and reach it here with a `client_credentials` grant. The service-account
// token is cached in-process with a 30-second expiry skew.
//
// Phase 5 Plan 05-00 Wave 0 surface:
//   - `getServiceAccountToken()` — fork of b2c-web/lib/keycloak-admin.ts
//   - `adminApiBase()` — fork of b2c-web/lib/keycloak-admin.ts
//   - NO sub-agent creation helper yet — Plan 05-01 Task 2 adds it. Keeping
//     the Wave 0 surface minimal lets Plan 01 add the B2B-10 create flow
//     without drift. (05-00-PLAN acceptance criterion asserts the sub-agent
//     helper export is absent in this Wave.)
//
// SECURITY (mitigation T-05-00-02):
//   - This module must NEVER be imported from a `"use client"` file.
//   - The service-account access token MUST NEVER be logged. It grants
//     manage-users on the realm, so leaking it leaks the whole user base.
//   - All fetches run server-side (Node runtime only — callers MUST set
//     `export const runtime = 'nodejs'` on their route handler).
//
// Source: fork of src/portals/b2c-web/lib/keycloak-admin.ts;
// 05-00-PLAN action step 8.

// Runtime guard: this module must never be pulled into a client bundle.
// `typeof window !== 'undefined'` is true in the browser runtime only —
// Node + Edge runtimes both leave `window` undefined. Grep-verifiable
// per 05-00-PLAN acceptance criterion T-05-00-02.
if (typeof window !== 'undefined') {
  throw new Error(
    'lib/keycloak-b2b-admin.ts imported into a Client Component — never do that',
  );
}

interface CachedToken {
  accessToken: string;
  /** Absolute ms epoch at which the cached token becomes unsafe to reuse. */
  expiresAtMs: number;
}

let cached: CachedToken | null = null;

/**
 * Acquire a service-account access token for `tbe-b2b-admin` via the
 * client_credentials grant. Cached in-process until 30 seconds before
 * expiry, then refreshed lazily on the next call.
 */
export async function getServiceAccountToken(): Promise<string> {
  const now = Date.now();
  if (cached && cached.expiresAtMs > now + 30_000) {
    return cached.accessToken;
  }

  const issuer = requireEnv('KEYCLOAK_B2B_ISSUER');
  const clientId = requireEnv('KEYCLOAK_B2B_ADMIN_CLIENT_ID');
  const clientSecret = requireEnv('KEYCLOAK_B2B_ADMIN_CLIENT_SECRET');

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
 * Derive the admin API base from `KEYCLOAK_B2B_ISSUER`.
 *
 * Issuer looks like `https://kc.example.com/realms/tbe-b2b`; the Keycloak
 * Admin API for the same realm lives at
 * `https://kc.example.com/admin/realms/tbe-b2b`. Strip the `/realms/…`
 * suffix and insert `/admin` to keep the host identical.
 */
export function adminApiBase(): string {
  const issuer = requireEnv('KEYCLOAK_B2B_ISSUER').replace(/\/+$/, '');
  // Regex capture = everything before `/realms/` (the origin + path prefix)
  // then the realm name itself.
  const match = issuer.match(/^(.+?)\/realms\/([^/]+)$/);
  if (!match) {
    throw new Error(
      'KEYCLOAK_B2B_ISSUER must be shaped "…/realms/<realm>"',
    );
  }
  const [, origin, realm] = match;
  return `${origin}/admin/realms/${realm}`;
}

// ---- internals ---------------------------------------------------------

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
