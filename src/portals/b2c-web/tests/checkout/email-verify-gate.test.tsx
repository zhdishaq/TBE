// Task 3 RED — EmailVerifyGate (D-06, Pitfall 7, T-04-02-03).
//
// Behaviour spec:
//   - verified=true  → nothing rendered
//   - verified=false → modal with heading "Verify your email first"
//   - Resend button POSTs to /api/auth/resend-verification
//   - The modal cannot be dismissed (no close affordance)

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { EmailVerifyGate } from '@/components/checkout/email-verify-gate';

describe('<EmailVerifyGate>', () => {
  let originalFetch: typeof fetch;
  beforeEach(() => {
    originalFetch = global.fetch;
  });
  afterEach(() => {
    global.fetch = originalFetch;
    vi.restoreAllMocks();
  });

  it('renders nothing when verified=true', () => {
    const { container } = render(
      <EmailVerifyGate email="test@example.com" verified={true} />,
    );
    expect(container.textContent ?? '').not.toMatch(/Verify your email first/);
  });

  it('renders the verify heading when verified=false', () => {
    render(<EmailVerifyGate email="test@example.com" verified={false} />);
    expect(
      screen.getByRole('heading', { name: /Verify your email first/i }),
    ).toBeInTheDocument();
    expect(screen.getByText(/test@example\.com/)).toBeInTheDocument();
  });

  it('Resend button POSTs /api/auth/resend-verification', async () => {
    const fetchSpy = vi.fn(async () => new Response('{}', { status: 200 }));
    global.fetch = fetchSpy as unknown as typeof fetch;

    const user = userEvent.setup();
    render(<EmailVerifyGate email="test@example.com" verified={false} />);
    await user.click(screen.getByRole('button', { name: /resend email/i }));

    expect(fetchSpy).toHaveBeenCalledTimes(1);
    const [url, init] = fetchSpy.mock.calls[0];
    expect(String(url)).toBe('/api/auth/resend-verification');
    expect((init as RequestInit).method).toBe('POST');
  });
});
