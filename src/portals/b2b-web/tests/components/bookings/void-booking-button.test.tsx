// Plan 05-04 Task 3 — VoidBookingButton.
//
// Contract (D-44, UI-SPEC §Booking detail):
//   - Only rendered for roles.includes('agent-admin').
//   - Radix AlertDialog confirm destructive (red-600 action).
//   - On confirm → POST /api/bookings/[id]/void with reason.
//   - 202 AcceptedResponse → success toast + refresh.
//   - 409 (post-ticket) → error toast with "already ticketed" copy.
//   - Disabled state while in-flight (no double-submit).

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { VoidBookingButton } from '@/components/bookings/void-booking-button';

describe('VoidBookingButton', () => {
  beforeEach(() => {
    // Default fetch mock returns 202 Accepted.
    (global as unknown as { fetch: typeof fetch }).fetch = vi.fn(async () =>
      new Response(null, { status: 202 }),
    ) as unknown as typeof fetch;
  });

  it('renders for agent-admin only', () => {
    const { rerender } = render(
      <VoidBookingButton bookingId="b1" roles={['agent']} />,
    );
    expect(screen.queryByRole('button', { name: /void booking/i })).toBeNull();
    rerender(<VoidBookingButton bookingId="b1" roles={['agent-admin']} />);
    expect(screen.getByRole('button', { name: /void booking/i })).toBeInTheDocument();
  });

  it('opens a destructive confirmation dialog and posts on confirm', async () => {
    const user = userEvent.setup();
    render(<VoidBookingButton bookingId="b-123" roles={['agent-admin']} />);

    await user.click(screen.getByRole('button', { name: /void booking/i }));
    // Radix AlertDialog sets role="alertdialog"
    expect(await screen.findByRole('alertdialog')).toBeInTheDocument();

    const confirm = screen.getByRole('button', { name: /confirm void/i });
    // Destructive copy — red-600 per UI-SPEC.
    expect(confirm.className).toMatch(/red/);

    await user.click(confirm);
    await waitFor(() => {
      expect(global.fetch).toHaveBeenCalledWith(
        '/api/bookings/b-123/void',
        expect.objectContaining({
          method: 'POST',
        }),
      );
    });
  });

  it('handles 409 (post-ticket) with an error toast', async () => {
    (global as unknown as { fetch: typeof fetch }).fetch = vi.fn(async () =>
      new Response(
        JSON.stringify({ type: '/errors/post-ticket-cancel-unsupported' }),
        {
          status: 409,
          headers: { 'Content-Type': 'application/problem+json' },
        },
      ),
    ) as unknown as typeof fetch;
    const user = userEvent.setup();
    render(<VoidBookingButton bookingId="b-123" roles={['agent-admin']} />);

    await user.click(screen.getByRole('button', { name: /void booking/i }));
    await user.click(await screen.findByRole('button', { name: /confirm void/i }));

    // Error surface — either inline or via role="alert".
    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent(/already ticketed/i);
    });
  });
});
