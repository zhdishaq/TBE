// Stripe Elements E2E helpers. Targets the private Stripe iframe via
// its internal name `__privateStripeFrame*` (Stripe does not expose a
// stable public selector).
//
// Source: Stripe docs + RESEARCH Validation Architecture.

import type { Page } from '@playwright/test';

export interface CardInput {
  number: string;
  exp: string; // "MM/YY"
  cvc: string;
  postalCode?: string;
}

/**
 * Fill the PaymentElement card iframe. Caller is responsible for
 * clicking the "Pay" button after this returns.
 */
export async function fillStripeCard(
  page: Page,
  card: CardInput = {
    number: '4242 4242 4242 4242',
    exp: '12/34',
    cvc: '123',
    postalCode: '12345',
  },
): Promise<void> {
  const stripeFrame = page.frameLocator(
    'iframe[name^="__privateStripeFrame"]',
  );
  await stripeFrame
    .getByPlaceholder(/card number|1234 1234/i)
    .fill(card.number);
  await stripeFrame.getByPlaceholder(/MM.*YY|expir/i).fill(card.exp);
  await stripeFrame.getByPlaceholder(/CVC|security code/i).fill(card.cvc);
  if (card.postalCode) {
    const postal = stripeFrame.getByPlaceholder(/ZIP|postal/i);
    if (await postal.isVisible().catch(() => false)) {
      await postal.fill(card.postalCode);
    }
  }
}

/**
 * 3DS-required test PAN — triggers Stripe's 3DS authentication frame.
 * Useful for exercising the SCA path in package bookings.
 */
export async function fillStripe3DSRequired(page: Page): Promise<void> {
  await fillStripeCard(page, {
    number: '4000 0027 6000 3184',
    exp: '12/34',
    cvc: '123',
    postalCode: '12345',
  });
}
