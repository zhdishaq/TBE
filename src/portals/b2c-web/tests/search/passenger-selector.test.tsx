// Task 2 RED — PassengerSelector airline-rule validation (FLTB-01).
//
// Rules enforced:
//   - total pax ≤ 9
//   - infants_on_lap ≤ adults
//   - infants_on_lap + infants_in_seat ≤ 2 * adults
//   - increment/decrement buttons disable at limits
//   - inline error copy shown (not toast) when a rule is violated
//
// The component is controlled; we drive it via keyboard/user events.

import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import {
  PassengerSelector,
  type PassengerCounts,
} from '@/components/search/passenger-selector';

function Harness(initial: Partial<PassengerCounts> = {}) {
  const [value, setValue] = useState<PassengerCounts>({
    adults: 1,
    children: 0,
    infantsLap: 0,
    infantsSeat: 0,
    ...initial,
  });
  return { value, setValue };
}

function Driver(props: { initial?: Partial<PassengerCounts> }) {
  const h = Harness(props.initial);
  return <PassengerSelector value={h.value} onChange={h.setValue} />;
}

describe('<PassengerSelector>', () => {
  it('renders the four labels from UI-SPEC', () => {
    render(<Driver />);
    expect(screen.getByText(/^Adults$/)).toBeInTheDocument();
    expect(screen.getByText(/^Children \(2–11\)$/)).toBeInTheDocument();
    expect(screen.getByText(/^Infants on lap \(<2\)$/)).toBeInTheDocument();
    expect(screen.getByText(/^Infants in seat \(<2\)$/)).toBeInTheDocument();
  });

  it('caps total pax at 9 (adult +) ', async () => {
    const user = userEvent.setup();
    render(<Driver initial={{ adults: 9 }} />);
    const incAdults = screen.getByRole('button', { name: /increase adults/i });
    // Already 9 — increase should be disabled.
    expect(incAdults).toBeDisabled();
    await user.click(incAdults); // no-op
    expect(
      screen.queryByText(/Maximum\s*9\s*passengers/i),
    ).toBeInTheDocument();
  });

  it('disables infantsLap increment when infantsLap == adults', async () => {
    render(<Driver initial={{ adults: 1, infantsLap: 1 }} />);
    const incLap = screen.getByRole('button', { name: /increase infants on lap/i });
    expect(incLap).toBeDisabled();
    expect(
      screen.getByText(/At most one infant on lap per adult/i),
    ).toBeInTheDocument();
  });

  it('respects the 2× adults cap for combined infants', async () => {
    // 1 adult, 1 lap infant, 1 seat infant  -> 2 infants, cap is 2×1=2
    // Increasing infantsSeat should be blocked.
    render(<Driver initial={{ adults: 1, infantsLap: 1, infantsSeat: 1 }} />);
    const incSeat = screen.getByRole('button', {
      name: /increase infants in seat/i,
    });
    expect(incSeat).toBeDisabled();
  });

  it('decrement at zero is disabled', () => {
    render(<Driver initial={{ adults: 1, children: 0 }} />);
    const decChildren = screen.getByRole('button', { name: /decrease children/i });
    expect(decChildren).toBeDisabled();
  });

  it('adults decrement is disabled at 1 (airline rule — at least one adult)', () => {
    render(<Driver initial={{ adults: 1 }} />);
    const decAdults = screen.getByRole('button', { name: /decrease adults/i });
    expect(decAdults).toBeDisabled();
  });
});
