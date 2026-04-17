// Wave 0 E2E smoke (05-00 Task 2).
//
// Two tests:
//  1. "landing page renders AgentPortalBadge" — always runs; confirms
//     Playwright + Next.js dev server + Chromium all agree. This is the
//     per-task <automated> gate.
//  2. "protected route redirects" — hits /dashboard unauthenticated and
//     asserts the middleware bounces to either Keycloak tbe-b2b login or
//     the local /api/auth/signin handler (both are acceptable; the exact
//     redirect target depends on whether the Keycloak env vars are set).
//
// Source: 05-00-PLAN action step 4 (Task 2).

import { test, expect } from '@playwright/test';

test('landing page renders AgentPortalBadge', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('main')).toBeVisible();
  await expect(page.getByText(/agent portal/i).first()).toBeVisible();
});

test('protected route redirects to Keycloak', async ({ page }) => {
  // Either Keycloak login page OR 302/303 redirect chain resolves to a
  // login URL. We accept either because the test environment may not
  // have Keycloak configured yet (Wave 0 only stages the realm delta).
  await page.goto('/dashboard', { waitUntil: 'networkidle' }).catch(() => {
    /* Network-idle can fail if Keycloak is unreachable — that's fine, we
       still assert on the URL below. */
  });
  expect(page.url()).toMatch(/realms\/tbe-b2b|\/api\/auth\/signin|\/login/);
});
