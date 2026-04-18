// Plan 05-05 Task 1 — WalletPaymentElementWrapper + memoised loadStripe.
//
// Contract (Pitfall 5 preservation):
//   - `<Elements>` from @stripe/react-stripe-js is mounted with the module-
//     scoped `stripePromise` singleton.
//   - When `clientSecret` is null/empty the component renders nothing (no
//     Elements, no IFRAMEs) so the wrapper can be imported conditionally on
//     the top-up form's two-phase flow without eagerly reaching Stripe.
//   - Across two renders of the wrapper, `loadStripe` must be called exactly
//     ONCE — the whole point of the memoised singleton in `lib/stripe.ts`.

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, cleanup } from '@testing-library/react';

// Stripe.js browser SDK mock — spy on loadStripe so we can count invocations.
const loadStripeSpy = vi.fn(() => Promise.resolve({} as unknown));
vi.mock('@stripe/stripe-js', () => ({
  loadStripe: loadStripeSpy,
}));

// Stub <Elements> so the jsdom render doesn't attempt to mount the real Stripe
// IFRAME. We just forward children and stash the received props on a module
// accessor for assertions.
const elementsMock = vi.fn(({ children }: { children?: React.ReactNode }) => (
  <div data-testid="elements-root">{children as React.ReactNode}</div>
));
vi.mock('@stripe/react-stripe-js', () => ({
  Elements: (props: Record<string, unknown>) => elementsMock(props),
}));

beforeEach(() => {
  loadStripeSpy.mockClear();
  elementsMock.mockClear();
  vi.resetModules();
  // Ensure the publishable key is set so `lib/stripe.ts` module-load doesn't
  // throw. Use a structurally-valid test key that never touches real Stripe.
  process.env.NEXT_PUBLIC_STRIPE_PK = 'pk_test_wallet_dummy';
  process.env.NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY = 'pk_test_wallet_dummy';
  cleanup();
});

async function importWrapper() {
  return await import(
    '@/components/wallet/wallet-payment-element-wrapper'
  );
}

describe('WalletPaymentElementWrapper', () => {
  it('renders children inside <Elements> when clientSecret is provided', async () => {
    const { WalletPaymentElementWrapper } = await importWrapper();
    const { getByTestId, getByText } = render(
      <WalletPaymentElementWrapper clientSecret="pi_123_secret_abc">
        <span>child-node</span>
      </WalletPaymentElementWrapper>,
    );
    expect(getByTestId('elements-root')).toBeInTheDocument();
    expect(getByText('child-node')).toBeInTheDocument();
    // Assert <Elements> was invoked with { stripe, options: { clientSecret, ... } }
    expect(elementsMock).toHaveBeenCalled();
    const props = elementsMock.mock.calls[0][0] as Record<string, unknown>;
    expect(props.stripe).toBeDefined();
    expect(
      (props.options as { clientSecret?: string } | undefined)?.clientSecret,
    ).toBe('pi_123_secret_abc');
  });

  it('renders nothing when clientSecret is null', async () => {
    const { WalletPaymentElementWrapper } = await importWrapper();
    const { container } = render(
      <WalletPaymentElementWrapper clientSecret={null}>
        <span>hidden</span>
      </WalletPaymentElementWrapper>,
    );
    expect(container.querySelector('[data-testid="elements-root"]')).toBeNull();
  });

  it('calls loadStripe exactly once across two renders (module-scope singleton — Pitfall 5)', async () => {
    const { WalletPaymentElementWrapper } = await importWrapper();
    const { unmount } = render(
      <WalletPaymentElementWrapper clientSecret="pi_a_secret_1">
        <span>one</span>
      </WalletPaymentElementWrapper>,
    );
    unmount();
    render(
      <WalletPaymentElementWrapper clientSecret="pi_b_secret_2">
        <span>two</span>
      </WalletPaymentElementWrapper>,
    );
    // Memoised lib/stripe.ts must NOT call loadStripe a second time.
    expect(loadStripeSpy).toHaveBeenCalledTimes(1);
  });
});
