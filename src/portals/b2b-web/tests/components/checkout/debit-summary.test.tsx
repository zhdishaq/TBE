// Plan 05-02 Task 3 RED tests -- DebitSummary.
//
// Verifies the primary CTA copy (`Confirm booking -- debit PS{gross}`), that
// a 202 response routes to /checkout/success with the booking id, and that
// a 409 swaps the component for <InsufficientFundsPanel /> in-place.

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { DebitSummary } from '@/components/checkout/debit-summary';

const pushMock = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: pushMock, replace: vi.fn() }),
}));

describe('DebitSummary', () => {
  beforeEach(() => {
    pushMock.mockReset();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('renders the `Confirm booking -- debit PS{gross}` CTA', () => {
    render(
      <DebitSummary
        gross={230}
        balance={1000}
        currency="GBP"
        onConfirm="/api/b2b/bookings"
        payload={{ offerId: 'o1' }}
        roles={['agent-admin']}
      />,
    );
    expect(
      screen.getByRole('button', { name: /Confirm booking.*debit/i }),
    ).toBeInTheDocument();
  });

  it('routes to /checkout/success?booking=... on a 202 response', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        status: 202,
        ok: true,
        json: async () => ({ bookingId: 'BK-123' }),
      }),
    );
    render(
      <DebitSummary
        gross={230}
        balance={1000}
        currency="GBP"
        onConfirm="/api/b2b/bookings"
        payload={{ offerId: 'o1' }}
        roles={['agent']}
      />,
    );
    fireEvent.click(screen.getByRole('button', { name: /Confirm booking/i }));
    await waitFor(() => {
      expect(pushMock).toHaveBeenCalledWith('/checkout/success?booking=BK-123');
    });
  });

  it('swaps in <InsufficientFundsPanel /> when the POST returns 409', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({ status: 409, ok: false, json: async () => ({}) }),
    );
    render(
      <DebitSummary
        gross={1000}
        balance={1200}
        currency="GBP"
        onConfirm="/api/b2b/bookings"
        payload={{ offerId: 'o1' }}
        roles={['agent']}
        adminEmail="admin@acme.test"
      />,
    );
    fireEvent.click(screen.getByRole('button', { name: /Confirm booking/i }));
    await waitFor(() => {
      // Role-aware insufficient-funds copy lands in place.
      expect(screen.getByRole('alert')).toBeInTheDocument();
    });
  });
});
