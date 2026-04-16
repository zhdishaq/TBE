// Plan 04-01 Task 2 — receipt-download round-trip.
//
// Opt-in via TEST_KC_USER + TEST_KC_PASSWORD + TEST_BOOKING_ID. When set,
// asserts that GET /api/bookings/{id}/receipt.pdf returns application/pdf
// (200) for the authenticated owner. The b2c-web route is a stream-through
// proxy to BookingService.API's ReceiptsController (Pitfall 14 — pass the
// upstream Response body straight through; do not buffer).

import { test, expect } from '@playwright/test';
import { authedPage } from './fixtures/auth';

test.describe('receipt download round-trip', () => {
  test.skip(
    !process.env.TEST_KC_USER ||
      !process.env.TEST_KC_PASSWORD ||
      !process.env.TEST_BOOKING_ID,
    'Requires TEST_KC_USER + TEST_KC_PASSWORD + TEST_BOOKING_ID + live backend.',
  );

  test('GET /api/bookings/:id/receipt.pdf returns application/pdf', async ({
    page,
  }) => {
    await authedPage(page);

    const bookingId = process.env.TEST_BOOKING_ID!;
    const response = await page.request.get(
      `/api/bookings/${bookingId}/receipt.pdf`,
    );

    expect(response.status()).toBe(200);
    expect(response.headers()['content-type']).toContain('application/pdf');

    // Verify the body really is a PDF (magic bytes %PDF-).
    const body = await response.body();
    expect(body.length).toBeGreaterThan(500);
    expect(body[0]).toBe(0x25); // %
    expect(body[1]).toBe(0x50); // P
    expect(body[2]).toBe(0x44); // D
    expect(body[3]).toBe(0x46); // F
    expect(body[4]).toBe(0x2d); // -
  });
});
