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
});
