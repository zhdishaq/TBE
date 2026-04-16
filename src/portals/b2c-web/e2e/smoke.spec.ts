// Wave 0 E2E smoke (04-00 Task 2).
//
// Two tests:
//  1. "landing page renders" — always runs; confirms Playwright +
//     Next.js dev server + Chromium all agree. This is the per-task
//     <automated> gate.
//  2. "auth round-trip (live Keycloak)" — opt-in via TEST_KC_USER env
//     var. Satisfies RESEARCH Open Question 7 + VALIDATION Wave 0
//     requirement #4 (Auth.js v5 beta + Next 16 + nuqs mutual-compat).

import { test, expect } from '@playwright/test';
import { authedPage } from './fixtures/auth';

test('landing page renders', async ({ page }) => {
  await page.goto('/');
  await expect(
    page.getByRole('heading', { name: /book your trip/i }),
  ).toBeVisible();
});

test('auth round-trip (live Keycloak)', async ({ page }) => {
  test.skip(
    !process.env.TEST_KC_USER,
    'Set TEST_KC_USER + TEST_KC_PASSWORD to exercise the live Auth.js v5 + Keycloak round-trip (opt-in).',
  );
  await authedPage(page);
  // After the Keycloak flow, we should land on `/` with a session cookie.
  await page.goto('/');
  const cookies = await page.context().cookies();
  const sessionCookie = cookies.find(
    (c) =>
      c.name === 'next-auth.session-token' ||
      c.name === '__Secure-next-auth.session-token' ||
      c.name === 'authjs.session-token' ||
      c.name === '__Secure-authjs.session-token',
  );
  expect(sessionCookie, 'expected an Auth.js session cookie').toBeTruthy();
});
