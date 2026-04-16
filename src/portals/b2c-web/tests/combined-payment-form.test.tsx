// CombinedPaymentForm unit tests — Plan 04-04 / Task 3b <behavior>.
//
// D-08 invariant: ONE `<Elements>` tree around ONE `<PaymentElement>`,
// driven by a SINGLE clientSecret. `confirmPayment` fires exactly ONCE on
// click with `return_url = /checkout/processing?ref=basket-{id}`.
//
// We stub @stripe/react-stripe-js so the test doesn't need Stripe.js at
// runtime — useStripe/useElements return controllable spies.

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';

const confirmPaymentSpy = vi.fn().mockResolvedValue({});
const submitSpy = vi.fn().mockResolvedValue({});

vi.mock('@stripe/react-stripe-js', () => ({
  Elements: ({ children }: { children: React.ReactNode }) => <div data-testid="stripe-elements">{children}</div>,
  PaymentElement: () => <div data-testid="payment-element" />,
  useStripe: () => ({ confirmPayment: confirmPaymentSpy }),
  useElements: () => ({ submit: submitSpy }),
}));

vi.mock('@/lib/stripe', () => ({
  getStripe: () => Promise.resolve(null),
}));

import { CombinedPaymentForm } from '@/app/checkout/payment/combined-payment-form';

beforeEach(() => {
  confirmPaymentSpy.mockClear();
  submitSpy.mockClear();
});

describe('<CombinedPaymentForm>', () => {
  it('renders ONE <Elements> tree containing ONE <PaymentElement>', () => {
    render(
      <CombinedPaymentForm
        basketId="B-123"
        clientSecret="pi_test_secret_1"
        amount={870}
        currency="GBP"
      />,
    );
    // Exactly one of each — if we ever shipped two (flightClientSecret +
    // hotelClientSecret shape), testing-library would surface both.
    expect(screen.getAllByTestId('stripe-elements')).toHaveLength(1);
    expect(screen.getAllByTestId('payment-element')).toHaveLength(1);
  });

  it('calls stripe.confirmPayment EXACTLY ONCE with return_url = /checkout/processing?ref=basket-{id}', async () => {
    // jsdom exposes window.location; we just need the origin to sanity-check the URL.
    render(
      <CombinedPaymentForm
        basketId="B-123"
        clientSecret="pi_test_secret_1"
        amount={870}
        currency="GBP"
      />,
    );

    const payBtn = screen.getByRole('button', { name: /pay/i });
    fireEvent.click(payBtn);

    await waitFor(() => {
      expect(confirmPaymentSpy).toHaveBeenCalledTimes(1);
    });

    const call = confirmPaymentSpy.mock.calls[0]?.[0] as {
      confirmParams?: { return_url?: string };
    };
    expect(call?.confirmParams?.return_url).toContain('/checkout/processing?ref=basket-B-123');
    // Never a second confirmPayment — D-08 single-PI.
    expect(confirmPaymentSpy).toHaveBeenCalledTimes(1);
  });

  it('renders the "ONE charge on your statement" disclosure (D-08)', () => {
    render(
      <CombinedPaymentForm
        basketId="B-123"
        clientSecret="pi_test_secret_1"
        amount={870}
        currency="GBP"
      />,
    );
    const body = screen.getByTestId('combined-payment-form');
    expect(body.textContent ?? '').toMatch(/ONE charge on your statement/);
  });
});
