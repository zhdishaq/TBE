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
//
// Phase 5 Plan 05-01 Task 2 additions (B2B-01 sub-agent CRUD):
//   - `createSubAgent({ agencyId, email, firstName, lastName, role })`
//     Creates a Keycloak user under the caller's agency with a role from
//     `'agent' | 'agent-readonly'` (T-05-01-06 — agent-admin is literally
//     unassignable through this helper), emailVerified=false, and the
//     D-33 single-valued `agency_id` attribute populated. Throws
//     `DuplicateUserError` on 409.
//   - `deactivateUser({ callerAgencyId, userId })`
//     Reads the target user, asserts target.attributes.agency_id[0]
//     matches the caller (T-05-01-05 cross-tenant guard — throws
//     `CrossTenantError` otherwise), then PUTs `enabled=false`.
//   - `reactivateUser({ callerAgencyId, userId })` — mirror of
//     deactivateUser with `enabled=true`.
//   - `listAgencyUsers({ agencyId })` — returns every Keycloak user
//     scoped to the agency plus their realm roles.
//
// Pitfall 28 (Phase 5 planning language — "server-side agency_id
// injection"): these helpers deliberately take `agencyId` / `callerAgencyId`
// as explicit parameters. Every caller in the route handler layer reads
// agency_id from the session and NEVER from the request body — the
// tampering test (T-05-01-03) runs in tests/route-agents.test.ts.
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
 * Shape returned by `listAgencyUsers` — a projection of Keycloak's user
 * representation plus resolved realm role names. Used by the RSC
 * `/admin/agents` page and the GET /api/agents handler.
 */
export interface AgencyUser {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  roles: string[];
  enabled: boolean;
  /** Keycloak `createdTimestamp` — ms epoch. */
  createdTimestamp: number;
}

/**
 * Raised when the Keycloak admin `POST /users` endpoint returns 409
 * (duplicate email/username). The /api/agents route handler translates
 * this into HTTP 409 for the Create dialog's inline form error.
 */
export class DuplicateUserError extends Error {
  constructor() {
    super('duplicate_user');
    this.name = 'DuplicateUserError';
  }
}

/**
 * Raised by `deactivateUser` / `reactivateUser` when the target user's
 * agency_id attribute does not match the caller's session agency_id
 * (T-05-01-05). The route handler catches this, logs a structured warn
 * line, and returns HTTP 403.
 */
export class CrossTenantError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'CrossTenantError';
  }
}

/**
 * Create a Keycloak user under the caller's agency, assign the selected
 * realm role, and trigger the initial verify-email action. Returns on
 * success; throws `DuplicateUserError` on 409. Every other non-2xx
 * raises a sanitised `Error` that does NOT echo the Keycloak response
 * body (T-05-01-04 — Keycloak error payloads sometimes reflect the
 * request back and we refuse to leak user input into the server log).
 *
 * `role` is typed `'agent' | 'agent-readonly'` — agent-admin is
 * literally unassignable through this helper (T-05-01-06). The zod
 * schema on the route handler is the first line of defence; this type
 * is the second.
 *
 * Pitfall 28: `agencyId` is always the caller's session agency_id.
 * The route handler must NEVER forward a body-supplied value.
 */
export async function createSubAgent(params: {
  agencyId: string;
  email: string;
  firstName: string;
  lastName: string;
  role: 'agent' | 'agent-readonly';
}): Promise<void> {
  const token = await getServiceAccountToken();
  const base = adminApiBase();
  // Step 1 — create the user with the D-33 single-valued agency_id
  // attribute. `enabled=true` so they can log in, `emailVerified=false`
  // so Keycloak's required action gates login until the verify-email
  // link (Step 3) has been clicked.
  const createResponse = await fetch(`${base}/users`, {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${token}`,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({
      username: params.email,
      email: params.email,
      firstName: params.firstName,
      lastName: params.lastName,
      enabled: true,
      emailVerified: false,
      attributes: { agency_id: [params.agencyId] },
    }),
    cache: 'no-store',
  });
  if (createResponse.status === 409) {
    throw new DuplicateUserError();
  }
  if (!createResponse.ok) {
    throw new Error(
      `createSubAgent(create) returned ${createResponse.status}`,
    );
  }
  // Keycloak returns 201 + `Location: /admin/realms/<realm>/users/<uuid>`
  // on successful create. Parse the new user id off the header (there
  // is no body — extracting from Location is the documented approach).
  const location = createResponse.headers.get('location');
  const userId = location?.split('/').pop();
  if (!userId) {
    throw new Error('createSubAgent: missing Location header on create');
  }
  // Step 2 — resolve the realm role representation so we can POST it
  // into the user's realm role-mappings. Keycloak's admin API requires
  // the full role representation (id + name + description), not just
  // the name.
  const roleLookup = await fetch(`${base}/roles/${params.role}`, {
    headers: { Authorization: `Bearer ${token}` },
    cache: 'no-store',
  });
  if (!roleLookup.ok) {
    throw new Error(
      `createSubAgent(role-lookup) returned ${roleLookup.status}`,
    );
  }
  const roleRep = (await roleLookup.json()) as unknown;
  const assignResponse = await fetch(
    `${base}/users/${userId}/role-mappings/realm`,
    {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${token}`,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify([roleRep]),
      cache: 'no-store',
    },
  );
  if (!assignResponse.ok) {
    throw new Error(
      `createSubAgent(role-assign) returned ${assignResponse.status}`,
    );
  }
  // Step 3 — trigger the verify-email action (Keycloak handles
  // idempotency + rate-limiting for us; a failure here is logged but
  // not surfaced since the user was successfully created above and the
  // admin can resend from the UI later).
  const verifyResp = await fetch(
    `${base}/users/${userId}/send-verify-email`,
    {
      method: 'PUT',
      headers: { Authorization: `Bearer ${token}` },
      cache: 'no-store',
    },
  );
  if (!verifyResp.ok) {
    // Fire-and-forget — log a sanitised line but do NOT throw. The
    // user record exists; the admin can re-trigger the email later.
    // eslint-disable-next-line no-console
    console.warn(
      `createSubAgent(send-verify-email) returned ${verifyResp.status}`,
    );
  }
}

/**
 * Flip `enabled=false` on a target Keycloak user after verifying their
 * `agency_id` attribute matches the caller's session agency_id
 * (T-05-01-05 cross-tenant guard). Throws `CrossTenantError` when the
 * guard fails so the route handler can log a structured warn line
 * without the helper having to know about HTTP status codes.
 */
export async function deactivateUser(params: {
  callerAgencyId: string;
  userId: string;
}): Promise<void> {
  await setUserEnabled({ ...params, enabled: false });
}

/**
 * Flip `enabled=true` on a target Keycloak user with the same
 * cross-tenant guard as `deactivateUser`. Exposed for the reactivate
 * row action on the sub-agent list.
 */
export async function reactivateUser(params: {
  callerAgencyId: string;
  userId: string;
}): Promise<void> {
  await setUserEnabled({ ...params, enabled: true });
}

/**
 * Return every Keycloak user whose `agency_id` attribute equals
 * `params.agencyId`, plus their realm role names. Used by the GET
 * /api/agents handler and the RSC /admin/agents page (initial data).
 *
 * Pagination is capped at 200 — that's well above the expected upper
 * bound for a single agency (Phase 5 is built around agencies with
 * 5–50 sub-agents, not enterprise scale). If we ever need paging, we
 * extend here and introduce cursor params on the route handler.
 */
export async function listAgencyUsers(params: {
  agencyId: string;
}): Promise<AgencyUser[]> {
  const token = await getServiceAccountToken();
  const base = adminApiBase();
  // Keycloak admin `GET /users` supports `q=<key>:<value>` for
  // attribute searches. This hits the `agency_id` attribute we set at
  // create-time; `max=200` caps pagination.
  const resp = await fetch(
    `${base}/users?q=agency_id:${encodeURIComponent(params.agencyId)}&max=200`,
    {
      headers: { Authorization: `Bearer ${token}` },
      cache: 'no-store',
    },
  );
  if (!resp.ok) {
    throw new Error(`listAgencyUsers returned ${resp.status}`);
  }
  const users = (await resp.json()) as Array<{
    id: string;
    email: string;
    firstName: string;
    lastName: string;
    enabled: boolean;
    createdTimestamp: number;
  }>;
  // Fan out one role-lookup per user. For < 200 users on a warm
  // service-account token this is fast enough; we revisit if the
  // number ever matters.
  return Promise.all(
    users.map(async (u) => {
      const rolesResp = await fetch(
        `${base}/users/${u.id}/role-mappings/realm`,
        {
          headers: { Authorization: `Bearer ${token}` },
          cache: 'no-store',
        },
      );
      const roles = rolesResp.ok
        ? ((await rolesResp.json()) as Array<{ name?: string }>)
            .map((r) => r.name)
            .filter((n): n is string => typeof n === 'string')
        : [];
      return {
        id: u.id,
        email: u.email,
        firstName: u.firstName,
        lastName: u.lastName,
        roles,
        enabled: u.enabled,
        createdTimestamp: u.createdTimestamp,
      };
    }),
  );
}

/**
 * Shared enable/disable path for `deactivateUser` / `reactivateUser`.
 * Reads the target user first, asserts cross-tenant match, then PUTs
 * the full representation back with the new `enabled` flag. Using the
 * full representation avoids clobbering attributes a partial PUT might
 * otherwise drop.
 */
async function setUserEnabled(params: {
  callerAgencyId: string;
  userId: string;
  enabled: boolean;
}): Promise<void> {
  const token = await getServiceAccountToken();
  const base = adminApiBase();
  const userResp = await fetch(`${base}/users/${params.userId}`, {
    headers: { Authorization: `Bearer ${token}` },
    cache: 'no-store',
  });
  if (userResp.status === 404) {
    throw new Error('setUserEnabled: target not found');
  }
  if (!userResp.ok) {
    throw new Error(`setUserEnabled(lookup) returned ${userResp.status}`);
  }
  const user = (await userResp.json()) as {
    id: string;
    attributes?: { agency_id?: string[] };
    [key: string]: unknown;
  };
  // T-05-01-05 — every write path asserts target.agency_id matches
  // the caller session. `targetAgency !== params.callerAgencyId` is
  // the grep-verifiable form from the plan acceptance criteria.
  const targetAgency = user.attributes?.agency_id?.[0];
  if (targetAgency !== params.callerAgencyId) {
    throw new CrossTenantError(
      `cross-tenant deactivation blocked caller=${params.callerAgencyId} target_owner=${targetAgency ?? 'unknown'} user=${params.userId}`,
    );
  }
  const updateResp = await fetch(`${base}/users/${params.userId}`, {
    method: 'PUT',
    headers: {
      Authorization: `Bearer ${token}`,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ ...user, enabled: params.enabled }),
    cache: 'no-store',
  });
  if (!updateResp.ok) {
    throw new Error(`setUserEnabled(put) returned ${updateResp.status}`);
  }
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
