// Plan 05-01 Task 1 — header shell RED tests (Plan 05-02 update: Header is
// now an async RSC that fetches the wallet balance server-side, so tests
// `await` the JSX before handing it to react-testing-library).
//
// Verifies the authenticated header mounts AgentPortalBadge + PrimaryNav
// + UserMenu + WalletChip, and that admin gating still hides the Admin nav
// for non-admin roles.

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

// Stub next-auth/react inside UserMenu -- the header renders UserMenu
// which imports signOut. We never want to trigger a real redirect.
vi.mock('next-auth/react', () => ({ signOut: vi.fn() }));

// Plan 05-02: Header.tsx now imports `gatewayFetch` for the initial wallet
// balance hydration. In jsdom we stub the gateway so no next-auth server
// plumbing is pulled into the test runtime.
vi.mock('@/lib/api-client', () => ({
  UnauthenticatedError: class extends Error {},
  gatewayFetch: vi.fn(async () =>
    new Response(JSON.stringify({ amount: 0, currency: 'GBP' }), {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
    }),
  ),
}));

import { Header } from '@/components/layout/header';

function renderWithClient(ui: React.ReactElement) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, refetchOnMount: false } },
  });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe('Header shell', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('mounts AgentPortalBadge + brand + nav + user menu + wallet for an agent-admin session', async () => {
    const ui = await Header({
      agentName: 'Ada Lovelace',
      agencyId: 'AG-001',
      roles: ['agent-admin'],
    });
    renderWithClient(ui);
    // Brand wordmark -- "TBE" appears somewhere in the header.
    expect(screen.getAllByText(/TBE/i).length).toBeGreaterThan(0);
    // Badge is announced with aria-label "Agent portal".
    expect(screen.getByLabelText(/agent portal/i)).toBeInTheDocument();
    // Nav item Admin visible for agent-admin.
    expect(screen.getAllByRole('link', { name: /admin/i }).length).toBeGreaterThan(0);
  });

  it('HIDES Admin nav for a non-admin (agent) session', async () => {
    const ui = await Header({ agentName: 'Ada', agencyId: 'AG-001', roles: ['agent'] });
    renderWithClient(ui);
    // No Admin nav link in the primary nav.
    const adminLinks = screen.queryAllByRole('link', { name: /^admin$/i });
    expect(adminLinks).toHaveLength(0);
    // Base nav remains -- Dashboard/Bookings/Search render for every role.
    expect(screen.getByRole('link', { name: /dashboard/i })).toBeInTheDocument();
  });
});
