// Plan 05-01 Task 1 — user-menu RED tests.
//
// UI-SPEC §Global CTAs: sign-out literal is "Sign out" not "Logout".
// Acceptance grep #8 on 05-01-PLAN Task 1 asserts both.

import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';

// Stub next-auth/react before importing the component — we never want to
// actually call signOut during a unit test.
const signOutSpy = vi.fn();
vi.mock('next-auth/react', () => ({
  signOut: (...args: unknown[]) => signOutSpy(...args),
}));

import { UserMenu } from '@/components/layout/user-menu';

describe('UserMenu — Sign out literal', () => {
  it('renders the "Sign out" label (never "Logout")', async () => {
    render(<UserMenu agentName="Ada Lovelace" agencyId="AG-001" />);
    // DropdownMenu — the label may be rendered inside the trigger OR in
    // the menu that opens on click. Either way, the literal "Sign out"
    // must be present in the DOM after interaction.
    const trigger = screen.getByRole('button');
    fireEvent.click(trigger);
    expect(
      screen.getByText(/^Sign out$/),
    ).toBeInTheDocument();
    // Negative assertion — the starterKit default label "Logout" must
    // not leak into the B2B portal copy.
    expect(screen.queryByText(/^Logout$/)).toBeNull();
  });
});
