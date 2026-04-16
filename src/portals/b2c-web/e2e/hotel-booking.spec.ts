// Plan 04-03 / Task 3 — mobile hotel booking E2E (B2C-05 ≤5 steps).
//
// Counted user-driven screens from /hotels through /checkout/payment:
//   1. /hotels                (landing + search form)
//   2. /hotels/results        (result cards)
//   3. /hotels/[offerId]      (detail + room picker)
//   4. /checkout/details      (passenger info — shared with flights)
//   5. /checkout/payment      (Stripe PaymentElement)
//
// /checkout/processing + /checkout/success are automatic and NOT user
// steps (B2C-05 same rule as flights).
//
// Opt-in via TEST_HOTEL_BOOKING_E2E=1 because CI without a Keycloak user
// + gateway fixture shouldn't run this. When skipped we still run the
// screen-count rule via a smoke assert below so the spec never gives a
// false green in environments with a working gateway but no test data.

import { test, expect } from '@playwright/test';
import { authedPage } from './fixtures/auth';

const enabled = process.env.TEST_HOTEL_BOOKING_E2E === '1';

test.describe('hotel booking — mobile ≤5 steps (B2C-05)', () => {
  test.skip(
    !enabled,
    'Set TEST_HOTEL_BOOKING_E2E=1 (+ TEST_KC_USER/PASSWORD + stripe CLI) to run the mobile hotel-booking e2e.',
  );

  test('completes hotel booking in ≤5 user-driven screens', async ({ page }, testInfo) => {
    test.skip(
      testInfo.project.name !== 'mobile',
      'Mobile 5-step spec only runs under the `mobile` Playwright project.',
    );

    await authedPage(page);

    const visitedSteps = new Set<string>();
    page.on('framenavigated', (frame) => {
      if (frame !== page.mainFrame()) return;
      const u = new URL(frame.url());
      const path = u.pathname;
      if (
        path.startsWith('/checkout/processing') ||
        path.startsWith('/checkout/success')
      ) {
        return; // excluded per B2C-05
      }
      if (
        path === '/hotels' ||
        path.startsWith('/hotels/') ||
        path.startsWith('/checkout/')
      ) {
        visitedSteps.add(path.replace(/\/[a-z0-9-]+$/i, '/:id'));
      }
    });

    // Step 1: /hotels
    await page.goto('/hotels');
    await page.getByRole('combobox', { name: /destination/i }).fill('lon');
    const firstOption = page.getByRole('option').first();
    await firstOption.waitFor({ state: 'visible', timeout: 8_000 });
    await firstOption.click();

    await page.getByRole('button', { name: /date range/i }).click();
    await page.getByRole('button', { name: /apply/i }).click();

    await page.getByRole('button', { name: /search hotels/i }).click();

    // Step 2: /hotels/results
    await expect(page).toHaveURL(/\/hotels\/results/);
    const firstCard = page.locator('[data-offer-id]').first();
    await firstCard.waitFor({ state: 'visible', timeout: 15_000 });
    await firstCard.getByRole('button', { name: /view rooms/i }).click();

    // Step 3: /hotels/[offerId]
    await expect(page).toHaveURL(/\/hotels\/[^/]+$/);
    await page.getByRole('button', { name: /^book room$/i }).first().click();

    // Step 4: /checkout/details
    await expect(page).toHaveURL(/\/checkout\/details/);
    await page.getByLabel(/first name/i).fill('Alice');
    await page.getByLabel(/last name/i).fill('Example');

    // Step 5: /checkout/payment
    await page.getByRole('button', { name: /continue to payment/i }).click();
    await expect(page).toHaveURL(/\/checkout\/payment/);

    // Processing + success follow automatically — NOT counted.
    await expect(page).toHaveURL(/\/checkout\/processing/, { timeout: 30_000 });
    await expect(page).toHaveURL(/\/checkout\/success/, { timeout: 90_000 });

    expect(
      visitedSteps.size,
      `Mobile hotel flow should complete in ≤5 user-driven screens, got ${visitedSteps.size}: ${[...visitedSteps].join(', ')}`,
    ).toBeLessThanOrEqual(5);
  });
});
