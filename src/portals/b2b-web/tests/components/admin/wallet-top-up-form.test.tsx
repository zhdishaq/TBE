// Plan 05-05 Task 4 — TopUpForm (two-phase Stripe confirmPayment flow).
//
// Facts cover (from 05-05-PLAN Task 4 behavior block):
//   1. zod clamp — submitting £5 (below £10 min) shows inline "Top-up must be
//      between £10 and £50 000" and does NOT call fetch.
//   2. Happy path — submitting a valid amount POSTs /api/wallet/top-up/intent,
//      receives `{ clientSecret }`, renders <WalletPaymentElementWrapper> and
//      the dynamic submit label `Pay £{amount} to top up` (UI-SPEC line 217
//      Global CTAs + §11 line 398).
//   3. Problem+JSON surfacing — backend 400 application/problem+json shaped
//      `{type,allowedRange,requested}` renders the friendly inline message
//      parsed from the allowedRange (NOT a raw toast).

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

// Stub the Stripe wrapper so the form's "phase 2" renders something we can
// assert on without mounting actual Stripe.js iframes.
vi.mock('@/components/wallet/wallet-payment-element-wrapper', () => ({
  WalletPaymentElementWrapper: ({ clientSecret, children }: {
    clientSecret: string;
    children?: React.ReactNode;
  }) =>
    clientSecret ? (
      <div data-testid="stripe-elements" data-client-secret={clientSecret}>
        {children}
      </div>
    ) : null,
}));

// Stub @stripe/react-stripe-js's useStripe/useElements so the confirm button
// is enabled in the test. Use vi.hoisted so the confirmPayment spy is
// captured at the same hoist level as the vi.mock factory (HI-02 assertions
// inspect the call args).
const { confirmPaymentSpy } = vi.hoisted(() => ({
  confirmPaymentSpy: vi.fn().mockResolvedValue({}),
}));
vi.mock('@stripe/react-stripe-js', () => ({
  useStripe: () => ({ confirmPayment: confirmPaymentSpy }),
  useElements: () => ({}),
  PaymentElement: () => <div data-testid="payment-element" />,
}));

import { TopUpForm } from '@/app/(portal)/admin/wallet/top-up-form';

function renderWithClient(ui: React.ReactElement) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, refetchOnMount: false } },
  });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe('TopUpForm', () => {
  beforeEach(() => {
    vi.unstubAllGlobals();
    confirmPaymentSpy.mockClear();
  });

  it('zod: submitting £5 shows inline error and does NOT fetch', async () => {
    const fetchSpy = vi.fn();
    vi.stubGlobal('fetch', fetchSpy);

    renderWithClient(<TopUpForm />);

    const amount = screen.getByLabelText(/amount/i) as HTMLInputElement;
    await userEvent.clear(amount);
    await userEvent.type(amount, '5');
    fireEvent.submit(amount.closest('form')!);

    await waitFor(() => {
      expect(
        screen.getByText(/must be between £10 and £50 ?000/i),
      ).toBeInTheDocument();
    });
    expect(fetchSpy).not.toHaveBeenCalled();
  });

  it('happy path: POSTs intent, renders Stripe wrapper + dynamic "Pay £N to top up" label', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        headers: new Headers({ 'content-type': 'application/json' }),
        json: async () => ({
          clientSecret: 'pi_123_secret_xyz',
          paymentIntentId: 'pi_123',
          amount: 250,
          currency: 'GBP',
        }),
      }),
    );

    renderWithClient(<TopUpForm />);

    const amount = screen.getByLabelText(/amount/i) as HTMLInputElement;
    await userEvent.clear(amount);
    await userEvent.type(amount, '250');
    fireEvent.submit(amount.closest('form')!);

    await waitFor(() => {
      expect(screen.getByTestId('stripe-elements')).toHaveAttribute(
        'data-client-secret',
        'pi_123_secret_xyz',
      );
    });
    expect(screen.getByRole('button', { name: /Pay £/i })).toBeInTheDocument();
  });

  it('problem+json: renders inline "Top-up must be between £{min} and £{max}. You requested £{requested}."', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: false,
        status: 400,
        headers: new Headers({ 'content-type': 'application/problem+json' }),
        json: async () => ({
          type: '/errors/wallet-topup-out-of-range',
          title: 'Top-up amount out of range',
          status: 400,
          allowedRange: { min: 10, max: 50000, currency: 'GBP' },
          requested: 5,
        }),
      }),
    );

    renderWithClient(<TopUpForm />);

    const amount = screen.getByLabelText(/amount/i) as HTMLInputElement;
    await userEvent.clear(amount);
    await userEvent.type(amount, '5');
    // Bypass client-side zod by typing a value that satisfies min(10) first
    // then programmatically overriding the backend response.
    // (The test above already asserts the zod guard; here we assert that
    // once a value slips past zod — e.g. £5 with client-side disabled — the
    // backend problem+json is surfaced verbatim.)
    // Use amount=10 then rely on backend to reject with requested:5 payload.
    await userEvent.clear(amount);
    await userEvent.type(amount, '10');
    fireEvent.submit(amount.closest('form')!);

    await waitFor(() => {
      expect(
        screen.getByText(/must be between £10 and £50 ?000/i),
      ).toBeInTheDocument();
    });
    expect(screen.getByText(/requested £5/i)).toBeInTheDocument();
  });

  // HI-02 — Stripe.js rejects relative return_url with IntegrationError.
  // Guard that the confirm flow always passes an absolute URL including the
  // /b2b basePath (see next.config.mjs).
  it('HI-02: confirmPayment is called with absolute return_url including /b2b basePath', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        headers: new Headers({ 'content-type': 'application/json' }),
        json: async () => ({
          clientSecret: 'pi_abs_secret',
          paymentIntentId: 'pi_abs',
          amount: 250,
          currency: 'GBP',
        }),
      }),
    );

    renderWithClient(<TopUpForm />);

    const amount = screen.getByLabelText(/amount/i) as HTMLInputElement;
    await userEvent.clear(amount);
    await userEvent.type(amount, '250');
    fireEvent.submit(amount.closest('form')!);

    // Wait for the Stripe wrapper (phase-2) to mount, then grab the confirm
    // button inside it. The phase-1 submit button is unmounted once
    // clientSecret is set (gated by !clientSecret), so scoping into the
    // wrapper avoids any ambiguity.
    const wrapper = await screen.findByTestId('stripe-elements');
    const payBtn = await waitFor(() => {
      const btn = wrapper.querySelector('button');
      if (!btn) throw new Error('confirm button not yet rendered');
      if (btn.hasAttribute('disabled')) throw new Error('confirm button disabled');
      return btn as HTMLButtonElement;
    });
    fireEvent.click(payBtn);

    await waitFor(() => {
      expect(confirmPaymentSpy).toHaveBeenCalledTimes(1);
    });
    const callArgs = confirmPaymentSpy.mock.calls[0][0] as {
      confirmParams: { return_url: string };
    };
    const returnUrl = callArgs.confirmParams.return_url;
    expect(returnUrl).toMatch(/^https?:\/\//);
    expect(returnUrl).toContain('/b2b/admin/wallet');
    expect(returnUrl).toContain('success=1');
  });
});
