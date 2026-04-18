// Plan 05-04 Task 3 — dashboard card components.
//
// Compact contract suite — each card is a presentational component with
// well-defined props; behaviour tests are minimal because the complex
// fetching logic lives in the parent RSC page.

import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { TtlAlertsCard } from '@/components/dashboard/ttl-alerts-card';
import { WalletSummaryCard } from '@/components/dashboard/wallet-summary-card';
import { RecentBookingsCard } from '@/components/dashboard/recent-bookings-card';
import { QuickLinksGrid } from '@/components/dashboard/quick-links-grid';

describe('TtlAlertsCard', () => {
  it('renders the amber "approaching" bucket separate from the red "urgent" bucket', () => {
    render(
      <TtlAlertsCard
        warn={[{ bookingId: 'b1', pnr: 'ABC123', hoursRemaining: 20 }]}
        urgent={[{ bookingId: 'b2', pnr: 'XYZ789', hoursRemaining: 1.5 }]}
      />,
    );
    expect(screen.getByText('ABC123')).toBeInTheDocument();
    expect(screen.getByText('XYZ789')).toBeInTheDocument();
  });

  it('colours the urgent bucket red and the warn bucket amber', () => {
    const { container } = render(
      <TtlAlertsCard
        warn={[{ bookingId: 'b1', pnr: 'ABC123', hoursRemaining: 20 }]}
        urgent={[{ bookingId: 'b2', pnr: 'XYZ789', hoursRemaining: 1.5 }]}
      />,
    );
    const markup = container.innerHTML;
    expect(markup).toMatch(/red/);
    expect(markup).toMatch(/amber/);
  });

  it('renders an empty-state message when both buckets are empty', () => {
    render(<TtlAlertsCard warn={[]} urgent={[]} />);
    expect(screen.getByText(/no upcoming ticketing deadlines/i)).toBeInTheDocument();
  });
});

describe('WalletSummaryCard', () => {
  it('renders the current balance formatted GBP', () => {
    render(
      <WalletSummaryCard balance={1250.5} currency="GBP" threshold={100} roles={['agent']} />,
    );
    expect(screen.getByText(/1,250\.50/)).toBeInTheDocument();
  });

  it('hides the low-balance banner when balance is above threshold', () => {
    render(
      <WalletSummaryCard balance={1000} currency="GBP" threshold={100} roles={['agent']} />,
    );
    expect(screen.queryByText(/low balance/i)).toBeNull();
  });

  it('shows the low-balance banner when balance is at or below threshold', () => {
    render(
      <WalletSummaryCard balance={50} currency="GBP" threshold={100} roles={['agent']} />,
    );
    expect(screen.getByText(/low balance/i)).toBeInTheDocument();
  });
});

describe('RecentBookingsCard', () => {
  it('renders the last N bookings as rows', () => {
    render(
      <RecentBookingsCard
        bookings={[
          { id: 'b1', reference: 'TBE-260418-A1', clientName: 'Alice', status: 'Ticketed' },
          { id: 'b2', reference: 'TBE-260418-B2', clientName: 'Bob', status: 'Pending' },
        ]}
      />,
    );
    expect(screen.getByText('TBE-260418-A1')).toBeInTheDocument();
    expect(screen.getByText('Alice')).toBeInTheDocument();
  });
});

describe('QuickLinksGrid', () => {
  it('hides the admin-only /admin/wallet tile for a plain agent', () => {
    render(<QuickLinksGrid roles={['agent']} />);
    expect(screen.queryByRole('link', { name: /wallet top-up/i })).toBeNull();
  });

  it('shows the /admin/wallet tile for agent-admin', () => {
    render(<QuickLinksGrid roles={['agent-admin']} />);
    expect(screen.getByRole('link', { name: /wallet top-up/i })).toHaveAttribute(
      'href',
      '/admin/wallet',
    );
  });
});
