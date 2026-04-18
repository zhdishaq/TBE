// Plan 05-02 Task 3 RED tests -- InsufficientFundsPanel.
//
// Role-aware copy: agent-admin -> "Top up now" link to /admin/wallet.
// Non-admin -> "Request top-up" mailto: link with the agency-admin email.
// Panel always announces itself as role="alert".

import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { InsufficientFundsPanel } from '@/components/checkout/insufficient-funds-panel';

describe('InsufficientFundsPanel', () => {
  it('is announced via role="alert" so assistive tech picks it up', () => {
    render(
      <InsufficientFundsPanel
        gross={1000}
        balance={300}
        currency="GBP"
        roles={['agent']}
        adminEmail="admin@acme.test"
      />,
    );
    expect(screen.getByRole('alert')).toBeInTheDocument();
  });

  it('renders a "Top up now" link to /admin/wallet for agent-admin', () => {
    render(
      <InsufficientFundsPanel
        gross={1000}
        balance={300}
        currency="GBP"
        roles={['agent-admin']}
      />,
    );
    const link = screen.getByRole('link', { name: /Top up now/i });
    expect(link).toHaveAttribute('href', '/admin/wallet');
    // Non-admin CTA must not appear for an admin.
    expect(screen.queryByRole('link', { name: /Request top-up/i })).toBeNull();
  });

  it('renders a mailto "Request top-up" link for a non-admin agent', () => {
    render(
      <InsufficientFundsPanel
        gross={1000}
        balance={300}
        currency="GBP"
        roles={['agent']}
        adminEmail="admin@acme.test"
      />,
    );
    const link = screen.getByRole('link', { name: /Request top-up/i });
    expect(link.getAttribute('href') ?? '').toMatch(/^mailto:admin@acme\.test/);
    // Admin CTA must not appear.
    expect(screen.queryByRole('link', { name: /Top up now/i })).toBeNull();
  });

  // Plan 05-05 Task 5 retrofit — the panel now delegates its non-admin
  // mailto to the new <RequestTopUpLink/> primitive (T-05-03-09 mitigation).
  // The admin branch already points at /admin/wallet from the original 05-02
  // contract; these facts re-assert the contract AFTER the retrofit to
  // guard the interface shape.

  it('05-05 retrofit: agent-admin branch links to /admin/wallet via "Top up now"', () => {
    render(
      <InsufficientFundsPanel
        gross={1000}
        balance={300}
        currency="GBP"
        roles={['agent-admin']}
      />,
    );
    const link = screen.getByRole('link', { name: /top up now/i });
    expect(link).toHaveAttribute('href', '/admin/wallet');
  });

  it('05-05 retrofit: non-admin branch delegates mailto to RequestTopUpLink (no body param, no session material)', () => {
    render(
      <InsufficientFundsPanel
        gross={1000}
        balance={300}
        currency="GBP"
        roles={['agent']}
        adminEmail="owner@acme.test"
      />,
    );
    const link = screen.getByRole('link', { name: /request top-up/i });
    const href = link.getAttribute('href') ?? '';
    expect(href.startsWith('mailto:owner@acme.test')).toBe(true);
    // T-05-03-09 / T-05-05-02: no body param, no session identifiers cross
    // into the user's default mail client — subject only.
    expect(href).not.toMatch(/body=/i);
    expect(href).not.toMatch(/agency_?id/i);
    expect(href).not.toMatch(/token/i);
    expect(href).not.toMatch(/balance/i);
  });

  it('05-05 retrofit: panel remains role="alert" (urgent/blocking — distinct from passive LowBalanceBanner)', () => {
    render(
      <InsufficientFundsPanel
        gross={1000}
        balance={300}
        currency="GBP"
        roles={['agent']}
        adminEmail="owner@acme.test"
      />,
    );
    const panel = screen.getByRole('alert');
    expect(panel).toBeInTheDocument();
  });
});
