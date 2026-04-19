// Module augmentation for Auth.js v5 Session and JWT — Backoffice Portal.
//
// Phase 6 Plan 06-01 Task 3: extends the Session type with
// `session.roles: string[]` (flat Keycloak realm roles for ops-*) +
// `session.user.ops_role` (derived single strongest role for UI chip).
//
// Source: fork of b2b-web/types/auth.d.ts + 06-01-PLAN.md action step 3.E.

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
     * the tbe-backoffice realm: a subset of 'ops-admin' / 'ops-cs' /
     * 'ops-finance' / 'ops-read'. Empty array for an authenticated user
     * with no ops role (treat as unauthorised on every mutation endpoint
     * per 06-01-PLAN Pitfall 28 fail-closed mitigation).
     */
    roles: string[];
    user?: {
      /**
       * Keycloak `sub` claim. Required by the approval + DLQ route
       * handlers for audit-actor attribution (Pitfall 28).
       */
      id?: string;
      name?: string | null;
      email?: string | null;
      image?: string | null;
      /**
       * D-46 — single strongest ops role, derived from `session.roles`
       * by lib/auth.ts. Used exclusively for the portal badge / role
       * chip; authorisation decisions MUST go through `session.roles`
       * (multi-value; a user may hold more than one role).
       */
      ops_role?: 'ops-admin' | 'ops-cs' | 'ops-finance' | 'ops-read';
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
    /** Derived strongest ops role (ops-admin > finance > cs > read). */
    ops_role?: 'ops-admin' | 'ops-cs' | 'ops-finance' | 'ops-read';
    error?: 'RefreshAccessTokenError';
  }
}
