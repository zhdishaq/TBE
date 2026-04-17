// Plan 05-01 Task 1 — primary-nav RED tests.
//
// Asserts the role-conditional render contract from 05-01-PLAN.md
// acceptance criteria: Admin nav appears ONLY when roles includes
// 'agent-admin'; base nav items (Dashboard / Search / Bookings) always
// render.

import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';

import { PrimaryNav } from '@/components/layout/primary-nav';

describe('PrimaryNav role-conditional admin item', () => {
  it('renders Dashboard / Search / Bookings for any authenticated session', () => {
    render(<PrimaryNav roles={['agent']} />);
    expect(screen.getByRole('link', { name: /dashboard/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /search/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /bookings/i })).toBeInTheDocument();
  });

  it('HIDES Admin nav when roles does not include agent-admin', () => {
    render(<PrimaryNav roles={['agent']} />);
    expect(screen.queryByRole('link', { name: /admin/i })).toBeNull();
  });

  it('SHOWS Admin nav when roles includes agent-admin', () => {
    render(<PrimaryNav roles={['agent-admin']} />);
    expect(screen.getByRole('link', { name: /admin/i })).toBeInTheDocument();
  });

  it('HIDES Admin nav for agent-readonly session (D-35)', () => {
    render(<PrimaryNav roles={['agent-readonly']} />);
    expect(screen.queryByRole('link', { name: /admin/i })).toBeNull();
    // Base nav still renders — read-only sees Dashboard/Bookings/Search.
    expect(screen.getByRole('link', { name: /dashboard/i })).toBeInTheDocument();
  });
});
