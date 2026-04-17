// Plan 05-01 Task 1 — user-menu RED tests.
//
// UI-SPEC §Global CTAs: sign-out literal is "Sign out" not "Logout".
// Acceptance grep #8 on 05-01-PLAN Task 1 asserts both.
//
// Implementation note (jsdom + Radix DropdownMenu): Radix's Dropdown
// opens on `pointerdown` via its own pointer-event capture, so
// `fireEvent.click` does NOT reliably open the portal in jsdom. Use
// `@testing-library/user-event` (which simulates the full
// pointerdown -> pointerup -> click sequence) and query with the async
// `findByText` so the portal has a tick to mount.

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// Stub next-auth/react before importing the component — we never want to
// actually call signOut during a unit test.
const signOutSpy = vi.fn();
vi.mock('next-auth/react', () => ({
  signOut: (...args: unknown[]) => signOutSpy(...args),
}));

import { UserMenu } from '@/components/layout/user-menu';

describe('UserMenu — Sign out literal', () => {
  it('renders the "Sign out" label (never "Logout") when the menu is opened', async () => {
    const user = userEvent.setup();
    render(<UserMenu agentName="Ada Lovelace" agencyId="AG-001" />);
    // Open the menu — use userEvent because Radix listens for
    // pointerdown, not the synthetic click fired by fireEvent.
    const trigger = screen.getByRole('button', {
      name: /open account menu/i,
    });
    await user.click(trigger);
    // Radix portals the menu content into document.body. RTL's `screen`
    // queries document.body by default, so findByText (async) resolves
    // once the portal mounts.
    expect(await screen.findByText(/^Sign out$/)).toBeInTheDocument();
    // Negative assertion — the starterKit default label "Logout" must
    // not leak into the B2B portal copy.
    expect(screen.queryByText(/^Logout$/)).toBeNull();
  });
});
