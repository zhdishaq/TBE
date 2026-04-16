// Task 3 RED/E2E — desktop happy-path flight booking (B2C-04/05/06).
//
// Opt-in: the test is skipped unless the full infra (Keycloak + GDS
// connector + Stripe CLI webhook forwarding) is reachable, signalled
// by the TEST_FLIGHT_BOOKING_E2E=1 env var.
//
// Happy path covered:
//   /  →  search LHR→JFK
//       →  /flights/results  (pick an offer)
//       →  /flights/<offerId>  (Continue)
//       →  /checkout/details  (fill passenger form)
//       →  /checkout/payment  (Stripe PaymentElement with 4242…)
//       →  /checkout/processing  (polls; saga simulator emits Confirmed)
//       →  /checkout/success  (PNR visible)
//
// We assert that /checkout/success is reached via the POLL path, not
// the Stripe return_url landing — i.e. the URL must carry the
// `booking=` query from the polling redirect, NOT `payment_intent=`
// from Stripe's own redirect (Pitfall 6).

import { test, expect } from '@playwright/test';
import { authedPage } from './fixtures/auth';

const enabled = process.env.TEST_FLIGHT_BOOKING_E2E === '1';

test.describe('flight booking — desktop happy path', () => {
  test.skip(
    !enabled,
    'Set TEST_FLIGHT_BOOKING_E2E=1 (+ TEST_KC_USER/PASSWORD + stripe CLI) to run the full booking e2e.',
  );

  test('search → results → select → details → payment → processing → success', async ({ page }) => {
    await authedPage(page);
    await page.goto('/');

    // Search form
    await page.getByRole('combobox', { name: /from/i }).fill('LHR');
    await page.getByRole('option', { name: /LHR/ }).first().click();
    await page.getByRole('combobox', { name: /to/i }).fill('JFK');
    await page.getByRole('option', { name: /JFK/ }).first().click();
    await page.getByRole('button', { name: /date range/i }).click();
    await page.getByRole('button', { name: /apply/i }).click();
    await page.getByRole('button', { name: /search flights/i }).click();

    // Results page
    await expect(page).toHaveURL(/\/flights\/results/);
    const firstOffer = page.locator('[data-offer-id]').first();
    await firstOffer.waitFor({ state: 'visible', timeout: 15_000 });
    await firstOffer.getByRole('button', { name: /select/i }).click();

    // Fare rules drawer
    await expect(page).toHaveURL(/\/flights\//);
    await page.getByRole('link', { name: /continue/i }).click();

    // Details
    await expect(page).toHaveURL(/\/checkout\/details/);
    await page.getByLabel(/first name/i).fill('Alex');
    await page.getByLabel(/last name/i).fill('Turing');
    await page.getByLabel(/passport number/i).fill('TEST123456');
    await page.getByRole('button', { name: /continue to payment/i }).click();

    // Payment
    await expect(page).toHaveURL(/\/checkout\/payment/);
    // Stripe iframe — we fill the test PAN via the card-element helper.
    const cardFrame = page.frameLocator('iframe[name*="__privateStripeFrame"]');
    await cardFrame.getByPlaceholder(/card number/i).fill('4242 4242 4242 4242');
    await cardFrame.getByPlaceholder(/mm ?\/ ?yy/i).fill('12/34');
    await cardFrame.getByPlaceholder(/cvc/i).fill('123');
    await page.getByRole('button', { name: /^pay/i }).click();

    // Processing (saga polling; simulator confirms)
    await expect(page).toHaveURL(/\/checkout\/processing/);
    await expect(page).toHaveURL(/\/checkout\/success/, { timeout: 90_000 });

    // Success URL carries `booking=` (set by the polling redirect) —
    // NOT `payment_intent=` (which is what Stripe's own return_url flow
    // would append). This is the Pitfall 6 enforcement.
    const url = new URL(page.url());
    expect(url.searchParams.has('booking')).toBe(true);
    expect(url.searchParams.has('payment_intent')).toBe(false);

    // And the PNR is shown.
    await expect(page.getByText(/Booking reference/i)).toBeVisible();
  });
});
