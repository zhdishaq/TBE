// Module augmentation for Auth.js v5 Session and JWT — B2B Agent Portal.
//
// Phase 5 Plan 05-01 Task 1: extends the Wave 0 B2B session type with
// `user.agency_id` + `session.roles` so RSC layout + admin guards + the
// /api/agents route handler can read the Keycloak `realm_access.roles`
// array and the single-valued agency_id attribute (D-33) without casting
// to `any`.
//
// Source: fork of b2c-web/types/auth.d.ts + 05-01-PLAN.md <interfaces>.

import 'next-auth';
import 'next-auth/jwt';

declare module 'next-auth' {
  interface Session {
    access_token?: string;
    email_verified: boolean;
    expires_at?: number;
    error?: 'RefreshAccessTokenError';
    /**
     * Keycloak realm-role array (`realm_access.roles`) projected onto the
     * session by the jwt() callback in lib/auth.ts. Expected values for
     * the tbe-b2b realm: one or more of 'agent' / 'agent-admin' /
     * 'agent-readonly'. Empty array for an authenticated user with no
     * realm roles assigned (should not happen in normal flow; treat as
     * unauthorised for every B2B surface).
     */
    roles: string[];
    user?: {
      /**
       * Keycloak `sub` claim. Required by the /api/agents route handler
       * (Pitfall 28) so logging events can attribute the caller.
       */
      id?: string;
      name?: string | null;
      email?: string | null;
      image?: string | null;
      /**
       * D-33 (05-CONTEXT.md) — single-valued agency_id user attribute
       * projected onto the session. Every B2B API route MUST read this
       * from `session.user.agency_id`, NEVER from the request body
       * (Pitfall 28 — T-05-01-03 tampering guard).
       */
      agency_id?: string;
    };
  }
}

declare module 'next-auth/jwt' {
  interface JWT {
    access_token?: string;
    refresh_token?: string;
    expires_at?: number;
    email_verified?: boolean;
    /** Keycloak `sub` claim, copied on initial sign-in. */
    sub?: string;
    /** Keycloak realm-role array, copied from profile.realm_access.roles. */
    roles?: string[];
    /** Keycloak agency_id user attribute (D-33 single-valued). */
    agency_id?: string;
    error?: 'RefreshAccessTokenError';
  }
}
