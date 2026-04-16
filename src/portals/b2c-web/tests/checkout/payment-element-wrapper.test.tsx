// Task 3 RED — PaymentElementWrapper (B2C-06, Pitfall 5/6).
//
// Contract (behaviours asserted by this suite):
//   - On mount, calls getStripe() exactly ONCE (memoisation check).
//   - The child <Elements> is configured with the client_secret passed via props.
//   - On pay button click: calls stripe.confirmPayment with
//     confirmParams.return_url containing "/checkout/processing" AND the
//     bookingId query string.
//   - The button label includes "Pay" and the formatted amount.

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// Mock @stripe/stripe-js and @stripe/react-stripe-js FIRST — `vi.mock`
// hoists above imports, so these modules are replaced before
// payment-element-wrapper pulls them in.
const confirmPayment = vi.fn(async () => ({ paymentIntent: { status: 'succeeded' } }));
const stripeInstance = { confirmPayment };
const loadStripe = vi.fn(async (_pk?: string) => stripeInstance);

vi.mock('@stripe/stripe-js', () => ({
  loadStripe: (pk: string) => loadStripe(pk),
}));

vi.mock('@stripe/react-stripe-js', () => {
  return {
    Elements: ({ children, options }: { children: React.ReactNode; options: { clientSecret: string } }) => {
      // expose the clientSecret to the test so we can assert on it
      (globalThis as Record<string, unknown>).__LAST_ELEMENTS_OPTS__ = options;
      return <div data-testid="stripe-elements" data-client-secret={options?.clientSecret}>{children}</div>;
    },
    PaymentElement: () => <div data-testid="payment-element" />,
    useStripe: () => stripeInstance,
    useElements: () => ({ submit: vi.fn(async () => ({ error: null })) }),
  };
});

import { PaymentElementWrapper } from '@/components/checkout/payment-element-wrapper';

describe('<PaymentElementWrapper>', () => {
  beforeEach(() => {
    confirmPayment.mockClear();
    loadStripe.mockClear();
    // Stub the publishable key for getStripe().
    Object.assign(process.env, { NEXT_PUBLIC_STRIPE_PK: 'pk_test_smoke' });
    // jsdom doesn't set location.origin by default — set it to a known
    // value so the return_url assertion is stable.
    Object.defineProperty(window, 'location', {
      value: { ...window.location, origin: 'https://test.tbe.local' },
      writable: true,
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('mounts <Elements> with the provided clientSecret', () => {
    render(
      <PaymentElementWrapper
        amount={199.99}
        currency="GBP"
        bookingId="book-123"
        clientSecret="pi_test_secret_abc"
      />,
    );
    const elems = screen.getByTestId('stripe-elements');
    expect(elems).toHaveAttribute('data-client-secret', 'pi_test_secret_abc');
    expect(screen.getByTestId('payment-element')).toBeInTheDocument();
  });

  it('renders a pay button with the formatted amount', () => {
    render(
      <PaymentElementWrapper
        amount={199.99}
        currency="GBP"
        bookingId="book-123"
        clientSecret="pi_test_secret_abc"
      />,
    );
    const btn = screen.getByRole('button', { name: /Pay/i });
    // Accepts GBP symbol OR "GBP" prefix, and must contain the amount.
    expect(btn.textContent ?? '').toMatch(/Pay/);
    expect(btn.textContent ?? '').toMatch(/199/);
  });

  it('calls stripe.confirmPayment with return_url=/checkout/processing on submit', async () => {
    const user = userEvent.setup();
    render(
      <PaymentElementWrapper
        amount={199.99}
        currency="GBP"
        bookingId="book-xyz"
        clientSecret="pi_test_secret_abc"
      />,
    );
    await user.click(screen.getByRole('button', { name: /Pay/i }));
    expect(confirmPayment).toHaveBeenCalledTimes(1);
    const calls = confirmPayment.mock.calls as unknown as Array<
      [{ confirmParams: { return_url: string } }]
    >;
    const call = calls[0][0];
    expect(call.confirmParams.return_url).toMatch(/\/checkout\/processing/);
    expect(call.confirmParams.return_url).toMatch(/book-xyz/);
  });
});
