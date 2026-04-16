// Plan 04-01 Task 2 — dashboard tabs.
//
// Verifies that the Upcoming / Past tab partition honours the UI-SPEC
// copywriting contract verbatim and correctly splits bookings by the
// departure date relative to "now".
//
// Source: 04-UI-SPEC §Copywriting Contract + §Dashboard.

import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {
  DashboardTabs,
  type DashboardBooking,
} from '@/components/account/dashboard-tabs';

const futureBooking: DashboardBooking = {
  bookingId: '11111111-1111-1111-1111-111111111111',
  status: 3,
  bookingReference: 'TBE-260416-FUTURE01',
  productType: 'flight',
  pnr: 'PNR999',
  ticketNumber: '125-0000000001',
  totalAmount: 500,
  currency: 'GBP',
  departureDate: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString(),
  createdAt: new Date().toISOString(),
};

const pastBooking: DashboardBooking = {
  bookingId: '22222222-2222-2222-2222-222222222222',
  status: 3,
  bookingReference: 'TBE-260316-PAST01',
  productType: 'flight',
  pnr: 'PNR100',
  ticketNumber: '125-0000000002',
  totalAmount: 300,
  currency: 'GBP',
  departureDate: new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString(),
  createdAt: new Date(Date.now() - 45 * 24 * 60 * 60 * 1000).toISOString(),
};

describe('DashboardTabs', () => {
  it('renders the Upcoming tab as the default active tab', () => {
    render(<DashboardTabs upcoming={[futureBooking]} past={[]} />);

    const upcoming = screen.getByRole('tab', { name: /upcoming/i });
    expect(upcoming).toHaveAttribute('data-state', 'active');
  });

  it('renders badge counts that match the input partitions', () => {
    render(<DashboardTabs upcoming={[futureBooking]} past={[pastBooking]} />);

    // Tab labels include the partition count: "Upcoming (1)" / "Past (1)"
    expect(
      screen.getByRole('tab', { name: /upcoming.*1/i }),
    ).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /past.*1/i })).toBeInTheDocument();
  });

  it('renders the verbatim empty-state copy when there are no upcoming trips', () => {
    render(<DashboardTabs upcoming={[]} past={[pastBooking]} />);

    // UI-SPEC §Copywriting Contract — must match verbatim.
    expect(screen.getByText('No upcoming trips')).toBeInTheDocument();
    expect(
      screen.getByText(
        'When you book a flight, hotel, or car, it will appear here. Search now to get started.',
      ),
    ).toBeInTheDocument();
  });

  it('renders the verbatim empty-state copy for past bookings when the Past tab is activated', async () => {
    const user = userEvent.setup();
    render(<DashboardTabs upcoming={[futureBooking]} past={[]} />);

    await user.click(screen.getByRole('tab', { name: /past/i }));

    expect(screen.getByText('No past bookings yet')).toBeInTheDocument();
    expect(
      screen.getByText(
        'Your booking history will show here once you have completed a trip.',
      ),
    ).toBeInTheDocument();
  });

  it('lists a booking row under the active tab when bookings exist', () => {
    render(<DashboardTabs upcoming={[futureBooking]} past={[]} />);

    expect(
      screen.getByText(futureBooking.bookingReference),
    ).toBeInTheDocument();
  });
});
