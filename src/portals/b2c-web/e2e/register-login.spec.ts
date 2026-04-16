// Plan 04-01 Task 2 — Keycloak-backed register/login round-trip.
//
// Opt-in: self-skips when TEST_KC_USER / TEST_KC_PASSWORD are not set so
// CI agents without a live Keycloak still pass (matches the 04-00 smoke).
// When the creds are set the test completes a real sign-in and asserts
// the empty-state copy from UI-SPEC on /bookings.

import { test, expect } from '@playwright/test';
import { authedPage } from './fixtures/auth';

test.describe('register + login round-trip', () => {
  test.skip(
    !process.env.TEST_KC_USER || !process.env.TEST_KC_PASSWORD,
    'Requires TEST_KC_USER + TEST_KC_PASSWORD + live Keycloak.',
  );

  test('authenticated user reaches /bookings with empty-state copy', async ({
    page,
  }) => {
    await authedPage(page);

    await page.goto('/bookings');

    // UI-SPEC §Copywriting Contract — verbatim.
    await expect(
      page.getByRole('heading', { name: 'No upcoming trips' }),
    ).toBeVisible();
    await expect(
      page.getByText(
        'When you book a flight, hotel, or car, it will appear here. Search now to get started.',
      ),
    ).toBeVisible();
  });
});
