// Task 3 RED/E2E — mobile 5-step rule (B2C-05).
//
// The mobile viewport (iPhone 12, driven by the `mobile` Playwright
// project) has a hard UX budget: the user must reach
// /checkout/success in ≤5 DOM-visible step changes. The confirmation
// screen itself does NOT count (per B2C-05; it is the outcome, not a
// step). So the 5 counted screens are:
//
//   1. /               (landing + search form)
//   2. /flights/results
//   3. /flights/<offerId>   (fare rules)
//   4. /checkout/details
//   5. /checkout/payment
//
// …then /checkout/processing + /checkout/success follow automatically
// and are NOT user-driven steps.
//
// Opt-in: TEST_FLIGHT_BOOKING_E2E=1 (same gate as desktop happy path)
// so CI without Keycloak + Stripe CLI doesn't run it.

import { test, expect } from '@playwright/test';
import { authedPage } from './fixtures/auth';

const enabled = process.env.TEST_FLIGHT_BOOKING_E2E === '1';

test.describe('flight booking — mobile ≤5 steps (B2C-05)', () => {
  test.skip(
    !enabled,
    'Set TEST_FLIGHT_BOOKING_E2E=1 (+ TEST_KC_USER/PASSWORD + stripe CLI) to run the mobile booking e2e.',
  );

  test('completes booking in ≤5 user-driven screens before success', async ({ page }, testInfo) => {
    // Only run on the `mobile` project (iPhone 12); the chromium
    // desktop project covers the full flow separately.
    test.skip(
      testInfo.project.name !== 'mobile',
      'Mobile 5-step spec only runs under the `mobile` Playwright project.',
    );

    await authedPage(page);

    // Track each distinct path the user lands on PRIOR to
    // /checkout/processing + /checkout/success. That set is the
    // "step count" for B2C-05.
    const visitedSteps = new Set<string>();
    page.on('framenavigated', (frame) => {
      if (frame !== page.mainFrame()) return;
      const u = new URL(frame.url());
      // Only count in-app paths; strip queries so /flights/results
      // and /flights/results?foo=bar collapse to one step.
      const path = u.pathname;
      if (
        path.startsWith('/checkout/processing') ||
        path.startsWith('/checkout/success')
      ) {
        return; // excluded per rule
      }
      if (
        path === '/' ||
        path.startsWith('/flights/') ||
        path.startsWith('/checkout/')
      ) {
        visitedSteps.add(path.replace(/\/[a-z0-9-]+$/i, '/:id'));
      }
    });

    await page.goto('/');

    // Step 1 → 2: search
    await page.getByRole('combobox', { name: /from/i }).fill('LHR');
    await page.getByRole('option', { name: /LHR/ }).first().click();
    await page.getByRole('combobox', { name: /to/i }).fill('JFK');
    await page.getByRole('option', { name: /JFK/ }).first().click();
    await page.getByRole('button', { name: /date range/i }).click();
    await page.getByRole('button', { name: /apply/i }).click();
    await page.getByRole('button', { name: /search flights/i }).click();
    await expect(page).toHaveURL(/\/flights\/results/);

    // Step 2 → 3: select offer
    const firstOffer = page.locator('[data-offer-id]').first();
    await firstOffer.waitFor({ state: 'visible', timeout: 15_000 });
    await firstOffer.getByRole('button', { name: /select/i }).click();
    await expect(page).toHaveURL(/\/flights\//);

    // Step 3 → 4: continue to details
    await page.getByRole('link', { name: /continue/i }).click();
    await expect(page).toHaveURL(/\/checkout\/details/);
    await page.getByLabel(/first name/i).fill('Alex');
    await page.getByLabel(/last name/i).fill('Turing');
    await page.getByLabel(/passport number/i).fill('TEST123456');

    // Step 4 → 5: payment
    await page.getByRole('button', { name: /continue to payment/i }).click();
    await expect(page).toHaveURL(/\/checkout\/payment/);

    // Fill Stripe test PAN.
    const cardFrame = page.frameLocator('iframe[name*="__privateStripeFrame"]');
    await cardFrame.getByPlaceholder(/card number/i).fill('4242 4242 4242 4242');
    await cardFrame.getByPlaceholder(/mm ?\/ ?yy/i).fill('12/34');
    await cardFrame.getByPlaceholder(/cvc/i).fill('123');
    await page.getByRole('button', { name: /^pay/i }).click();

    // Processing + Success happen automatically; they must NOT inflate
    // the step count.
    await expect(page).toHaveURL(/\/checkout\/processing/);
    await expect(page).toHaveURL(/\/checkout\/success/, { timeout: 90_000 });

    // User-driven screens counted: /, /flights/results, /flights/:id,
    // /checkout/details, /checkout/payment — i.e. exactly 5.
    expect(
      visitedSteps.size,
      `Mobile flow should complete in ≤5 user-driven screens, got ${visitedSteps.size}: ${[...visitedSteps].join(', ')}`,
    ).toBeLessThanOrEqual(5);

    // And the PNR is shown.
    await expect(page.getByText(/Booking reference/i)).toBeVisible();
  });
});
