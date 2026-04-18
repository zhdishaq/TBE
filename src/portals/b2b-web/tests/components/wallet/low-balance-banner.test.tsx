// Plan 05-05 Task 5 — LowBalanceBanner.
//
// Facts cover (from 05-05-PLAN Task 5 behavior block):
//   1. Renders nothing when balance >= threshold.
//   2. Renders `role="status"` + `aria-live="polite"` banner (informational,
//      NOT urgent — UI-SPEC §11 lines 418/559/628) with copy
//      "Your wallet balance (£{bal}) is below your alert threshold (£{thr})."
//      when balance < threshold.
//   3. Agent-admin → "Top up" link to /admin/wallet.
//   4. Non-admin → <RequestTopUpLink/> (mailto), NO /admin/wallet link.
//   5. Dismiss writes sessionStorage 'lowBalanceDismissed' and hides banner;
//      localStorage NEVER written.
//   6. On next mount with sessionStorage.getItem('lowBalanceDismissed')==='1',
//      banner stays hidden.
//   7. Shares TanStack query key ['wallet','balance'] with WalletChip — only
//      ONE fetch per render (cache hit when mounted twice in the same client).

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { LowBalanceBanner } from '@/components/wallet/low-balance-banner';
import { WalletChip } from '@/components/wallet/wallet-chip';

function renderWithClient(
  ui: React.ReactElement,
  seed?: (qc: QueryClient) => void,
) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, refetchOnMount: false } },
  });
  if (seed) seed(client);
  return {
    client,
    ...render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>),
  };
}

describe('LowBalanceBanner', () => {
  beforeEach(() => {
    sessionStorage.clear();
    // localStorage should NEVER be touched by the banner; we assert that
    // explicitly via a spy.
    vi.spyOn(Storage.prototype, 'setItem');
  });
  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
    sessionStorage.clear();
  });

  it('renders nothing when balance >= threshold', () => {
    const { container } = renderWithClient(
      <LowBalanceBanner roles={['agent-admin']} />,
      (qc) => {
        qc.setQueryData(['wallet', 'balance'], { amount: 1000, currency: 'GBP' });
        qc.setQueryData(['wallet', 'threshold'], { threshold: 500, currency: 'GBP' });
      },
    );
    expect(container.firstChild).toBeNull();
  });

  it('renders role="status" + aria-live="polite" banner with copy when balance < threshold', () => {
    renderWithClient(
      <LowBalanceBanner roles={['agent-admin']} />,
      (qc) => {
        qc.setQueryData(['wallet', 'balance'], { amount: 200, currency: 'GBP' });
        qc.setQueryData(['wallet', 'threshold'], { threshold: 500, currency: 'GBP' });
      },
    );
    const banner = screen.getByRole('status');
    expect(banner).toHaveAttribute('aria-live', 'polite');
    expect(banner.textContent).toMatch(/below your alert threshold/i);
  });

  it('agent-admin sees a "Top up" link to /admin/wallet', () => {
    renderWithClient(
      <LowBalanceBanner roles={['agent-admin']} />,
      (qc) => {
        qc.setQueryData(['wallet', 'balance'], { amount: 200, currency: 'GBP' });
        qc.setQueryData(['wallet', 'threshold'], { threshold: 500, currency: 'GBP' });
      },
    );
    const link = screen.getByRole('link', { name: /top up/i });
    expect(link).toHaveAttribute('href', '/admin/wallet');
  });

  it('non-admin sees <RequestTopUpLink/> (mailto), NOT a /admin/wallet link', () => {
    renderWithClient(
      <LowBalanceBanner roles={['agent']} />,
      (qc) => {
        qc.setQueryData(['wallet', 'balance'], { amount: 200, currency: 'GBP' });
        qc.setQueryData(['wallet', 'threshold'], { threshold: 500, currency: 'GBP' });
      },
    );
    expect(screen.queryByRole('link', { name: /^top up$/i })).toBeNull();
    const mailto = screen.getByRole('link', { name: /request top-up/i });
    expect(mailto.getAttribute('href')).toMatch(/^mailto:/);
  });

  it('Dismiss writes sessionStorage "lowBalanceDismissed" and hides the banner; localStorage NEVER written', async () => {
    const { container } = renderWithClient(
      <LowBalanceBanner roles={['agent-admin']} />,
      (qc) => {
        qc.setQueryData(['wallet', 'balance'], { amount: 200, currency: 'GBP' });
        qc.setQueryData(['wallet', 'threshold'], { threshold: 500, currency: 'GBP' });
      },
    );
    const localSetItemSpy = vi.spyOn(window.localStorage, 'setItem');
    const dismiss = screen.getByRole('button', { name: /dismiss/i });
    fireEvent.click(dismiss);
    await waitFor(() => {
      expect(container.firstChild).toBeNull();
    });
    expect(sessionStorage.getItem('lowBalanceDismissed')).toBe('1');
    expect(localSetItemSpy).not.toHaveBeenCalled();
  });

  it('does not render on next mount when sessionStorage dismissed flag is set', () => {
    sessionStorage.setItem('lowBalanceDismissed', '1');
    const { container } = renderWithClient(
      <LowBalanceBanner roles={['agent-admin']} />,
      (qc) => {
        qc.setQueryData(['wallet', 'balance'], { amount: 200, currency: 'GBP' });
        qc.setQueryData(['wallet', 'threshold'], { threshold: 500, currency: 'GBP' });
      },
    );
    expect(container.firstChild).toBeNull();
  });

  it('shares ["wallet","balance"] cache with WalletChip (single fetch round-trip)', async () => {
    const fetchSpy = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({ amount: 200, currency: 'GBP', updatedAt: '' }),
    });
    vi.stubGlobal('fetch', fetchSpy);
    renderWithClient(
      <>
        <WalletChip initialBalance={200} currency="GBP" roles={['agent-admin']} />
        <LowBalanceBanner roles={['agent-admin']} />
      </>,
      (qc) => {
        qc.setQueryData(['wallet', 'threshold'], { threshold: 500, currency: 'GBP' });
      },
    );
    // Under a single QueryClient both components must hit the cache — we
    // explicitly disabled refetchOnMount and neither component has a
    // refetchInterval that fires synchronously, so the initial render must
    // make ZERO extra fetches (the cache is seeded via the chip's
    // initialData + the test's pre-seeded threshold).
    expect(fetchSpy).not.toHaveBeenCalled();
  });
});
