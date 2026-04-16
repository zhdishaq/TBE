// Playwright fixture — drives the Auth.js v5 signIn → Keycloak form →
// callback round trip. Opt-in via TEST_KC_USER; the caller must guard
// with `test.skip(!process.env.TEST_KC_USER)` so CI agents without a
// live Keycloak still pass.
//
// Source: 04-RESEARCH Validation Architecture + RESEARCH Open Question 7.

import type { Page } from '@playwright/test';
import { expect } from '@playwright/test';

export async function authedPage(page: Page): Promise<void> {
  const user = process.env.TEST_KC_USER;
  const password = process.env.TEST_KC_PASSWORD;
  if (!user || !password) {
    throw new Error(
      'authedPage() requires TEST_KC_USER + TEST_KC_PASSWORD env vars',
    );
  }

  // Trigger sign-in through our /login route (which redirects to the
  // Keycloak-hosted form per D-04).
  await page.goto('/login');
  const signInButton = page.getByRole('button', { name: /sign in/i });
  if (await signInButton.isVisible().catch(() => false)) {
    await signInButton.click();
  }

  // Keycloak's default login form.
  await page.getByLabel(/username or email|username/i).fill(user);
  await page.getByLabel(/password/i).fill(password);
  await page.getByRole('button', { name: /sign in|log in/i }).click();

  // Callback redirects back to our app.
  await page.waitForURL((url) => !url.host.includes('8080'), {
    timeout: 15_000,
  });

  // A session cookie should now exist.
  const cookies = await page.context().cookies();
  expect(
    cookies.some((c) => /(authjs|next-auth)\.session-token/.test(c.name)),
    'Expected an Auth.js session cookie after Keycloak round trip',
  ).toBe(true);
}
