// BasketFooter unit tests — Plan 04-04 / Task 3b <behavior>.
//
// Asserts PKG-04: independent cancellation policies rendered SIDE-BY-SIDE
// (never merged), total = sum of both amounts, and "Continue to checkout"
// disabled when either item is missing. Also verifies the screen-reader
// label shape: "Flight cancellation policy: X. Hotel cancellation
// policy: Y."

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { BasketFooter } from '@/components/trip-builder/basket-footer';

// next/navigation + zustand persistence are both touched by the
// component; stub them so the test runs offline.
const pushSpy = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: pushSpy, replace: vi.fn(), back: vi.fn() }),
}));

import { useBasket } from '@/hooks/use-basket';

beforeEach(() => {
  useBasket.getState().clear();
  pushSpy.mockReset();
});

describe('<BasketFooter>', () => {
  it('disables "Continue to checkout" when the basket is empty', () => {
    render(<BasketFooter />);
    const btn = screen.getByRole('button', { name: /continue to checkout/i });
    expect(btn).toBeDisabled();
  });

  it('disables "Continue to checkout" when only a flight is present', () => {
    useBasket.getState().addFlight({
      offerId: 'F-1',
      summary: 'LHR → JFK, 04 May',
      amount: { amount: 420, currency: 'GBP' },
      cancellationPolicy: 'nonRefundable',
    });
    render(<BasketFooter />);
    const btn = screen.getByRole('button', { name: /continue to checkout/i });
    expect(btn).toBeDisabled();
  });

  it('renders two INDEPENDENT cancellation policies side-by-side (PKG-04)', () => {
    useBasket.getState().addFlight({
      offerId: 'F-1',
      summary: 'LHR → JFK, 04 May',
      amount: { amount: 420, currency: 'GBP' },
      cancellationPolicy: 'nonRefundable',
    });
    useBasket.getState().addHotel({
      offerId: 'H-1',
      summary: 'Hotel Alpha · 3 nights',
      amount: { amount: 450, currency: 'GBP' },
      cancellationPolicy: 'free',
    });
    render(<BasketFooter />);

    // Plain strings required by acceptance grep.
    const flightCx = screen.getByTestId('flight-cancellation');
    const hotelCx = screen.getByTestId('hotel-cancellation');
    expect(flightCx).toHaveTextContent(/Flight cancellation/);
    expect(flightCx).toHaveTextContent(/Non-refundable/);
    expect(hotelCx).toHaveTextContent(/Hotel cancellation/);
    expect(hotelCx).toHaveTextContent(/Free cancellation/);

    // Policies are rendered in distinct containers — never merged.
    expect(flightCx).not.toBe(hotelCx);

    // Screen-reader label string (single announcement) covers both.
    const srLabel = screen.getByTestId('basket-cancellation-sr');
    expect(srLabel).toHaveTextContent(
      /Flight cancellation policy: Non-refundable\. Hotel cancellation policy: Free cancellation\./,
    );
  });

  it('shows a total equal to the sum of line items', () => {
    useBasket.getState().addFlight({
      offerId: 'F-1',
      summary: 'LHR → JFK, 04 May',
      amount: { amount: 420, currency: 'GBP' },
      cancellationPolicy: 'nonRefundable',
    });
    useBasket.getState().addHotel({
      offerId: 'H-1',
      summary: 'Hotel Alpha · 3 nights',
      amount: { amount: 450, currency: 'GBP' },
      cancellationPolicy: 'free',
    });
    render(<BasketFooter />);
    const total = screen.getByTestId('basket-total');
    // GBP format via Intl is £870.00 (en-GB).
    expect(total).toHaveTextContent('£870.00');
  });

  it('enables "Continue to checkout" once both items are present', () => {
    useBasket.getState().addFlight({
      offerId: 'F-1',
      summary: 'LHR → JFK, 04 May',
      amount: { amount: 420, currency: 'GBP' },
      cancellationPolicy: 'nonRefundable',
    });
    useBasket.getState().addHotel({
      offerId: 'H-1',
      summary: 'Hotel Alpha · 3 nights',
      amount: { amount: 450, currency: 'GBP' },
      cancellationPolicy: 'free',
    });
    render(<BasketFooter />);
    const btn = screen.getByRole('button', { name: /continue to checkout/i });
    expect(btn).toBeEnabled();
  });
});
