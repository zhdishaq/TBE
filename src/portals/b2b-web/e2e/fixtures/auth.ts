// Playwright fixtures for B2B Agent Portal auth.
//
// Wave 0 ships SKELETONS only — the actual Keycloak tbe-b2b round-trip
// flow is implemented in Plan 05-01 once the realm is imported by the
// human operator. Wave 0's purpose is to reserve the import paths so
// downstream plans can `import { signInAsAgent } from './fixtures/auth'`
// without errors.
//
// Source: 05-00-PLAN action step 4 + fork of b2c-web/e2e/fixtures/auth.ts.

import type { Page } from '@playwright/test';

/**
 * Sign in as a plain `agent` role user (can view agency-wide bookings per
 * D-34 but cannot enter /admin/*). Plan 05-01 Task 5 fills in the body.
 */
export async function signInAsAgent(_page: Page): Promise<void> {
  throw new Error(
    'signInAsAgent() is a Wave 0 skeleton — implemented by Plan 05-01 Task 5.',
  );
}

/**
 * Sign in as an `agent-admin` role user (D-32, D-33). Required to reach
 * /admin/agents (sub-agent create per B2B-10) and /admin/wallet
 * (top-up per B2B-07 / Plan 05-03). Plan 05-01 Task 5 fills in the body.
 */
export async function signInAsAgentAdmin(_page: Page): Promise<void> {
  throw new Error(
    'signInAsAgentAdmin() is a Wave 0 skeleton — implemented by Plan 05-01 Task 5.',
  );
}

/**
 * Sign in as an `agent-readonly` role user (D-35 — finance/compliance
 * agency-wide view). Mutations MUST 403 for this role. Plan 05-01 Task 5
 * fills in the body.
 */
export async function signInAsAgentReadonly(_page: Page): Promise<void> {
  throw new Error(
    'signInAsAgentReadonly() is a Wave 0 skeleton — implemented by Plan 05-01 Task 5.',
  );
}
