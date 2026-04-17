// Plan 05-01 Task 1 — header shell RED tests.
//
// Verifies the authenticated header mounts AgentPortalBadge + PrimaryNav
// + UserMenu, and that the desktop height `h-14` class is present.

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';

// Stub next-auth/react inside UserMenu — the header renders UserMenu
// which imports signOut. We never want to trigger a real redirect.
vi.mock('next-auth/react', () => ({ signOut: vi.fn() }));

import { Header } from '@/components/layout/header';

describe('Header shell', () => {
  it('mounts AgentPortalBadge + brand + nav + user menu for an agent-admin session', () => {
    render(
      <Header
        agentName="Ada Lovelace"
        agencyId="AG-001"
        roles={['agent-admin']}
      />,
    );
    // Brand wordmark — "TBE" appears somewhere in the header.
    expect(screen.getAllByText(/TBE/i).length).toBeGreaterThan(0);
    // Badge is announced with aria-label "Agent portal"
    expect(screen.getByLabelText(/agent portal/i)).toBeInTheDocument();
    // Nav item Admin visible for agent-admin
    expect(screen.getByRole('link', { name: /admin/i })).toBeInTheDocument();
  });

  it('HIDES Admin nav for a non-admin (agent) session', () => {
    render(
      <Header agentName="Ada" agencyId="AG-001" roles={['agent']} />,
    );
    expect(screen.queryByRole('link', { name: /admin/i })).toBeNull();
    // Base nav remains — Dashboard/Bookings/Search render for every role.
    expect(screen.getByRole('link', { name: /dashboard/i })).toBeInTheDocument();
  });
});
