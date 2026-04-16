// Plan 04-01 Task 2 — password-reset redirect to Keycloak.
//
// Self-skips when KEYCLOAK_ISSUER is not configured. When the env is set
// (Wave 0 smoke runners, local dev), clicking "Forgot password?" on /login
// must redirect to Keycloak's hosted reset-credentials flow per D-04
// (Keycloak owns registration, login, password-reset and email-verify
// pages — we never reimplement them).

import { test, expect } from '@playwright/test';

test.describe('password-reset redirect', () => {
  test.skip(
    !process.env.KEYCLOAK_ISSUER,
    'Requires KEYCLOAK_ISSUER to assert the Keycloak-hosted reset flow URL.',
  );

  test('clicking "Forgot password?" redirects to Keycloak reset-credentials', async ({
    page,
  }) => {
    await page.goto('/login');

    // D-04: password reset is owned by Keycloak. Our /login page exposes a
    // single "Forgot password?" link that hands off to the Keycloak-hosted
    // login-actions/reset-credentials URL for the tbe-b2c realm.
    const forgotLink = page.getByRole('link', { name: /forgot password/i });
    await expect(forgotLink).toBeVisible();

    await Promise.all([
      page.waitForURL(/login-actions\/reset-credentials/, { timeout: 15_000 }),
      forgotLink.click(),
    ]);

    const issuer = process.env.KEYCLOAK_ISSUER!.replace(/\/+$/, '');
    expect(page.url()).toContain(`${issuer}/login-actions/reset-credentials`);
  });
});
