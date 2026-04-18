// Plan 05-04 Task 3 — TtlCountdown component.
//
// Contract (UI-SPEC §Booking detail):
//   - Ticks every second; renders `Xd Yh Zm Ws` remaining.
//   - aria-live="off" on every tick to avoid SR spam.
//   - aria-live="polite" for the frame that crosses a threshold
//     (24h and 2h) so screen readers announce the state change.
//   - Colour: neutral > 24h, amber < 24h, red < 2h.
//   - Calls onThresholdCross(new state) exactly once per threshold.

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, act } from '@testing-library/react';
import { TtlCountdown } from '@/components/bookings/ttl-countdown';

describe('TtlCountdown', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });
  afterEach(() => {
    vi.useRealTimers();
  });

  it('renders remaining time in the Xd Yh Zm Ws shape', () => {
    // 1 day + 2 hours + 3 minutes + 4 seconds from "now".
    const now = new Date('2026-04-18T12:00:00.000Z');
    vi.setSystemTime(now);
    const deadline = new Date(now.getTime() + (26 * 60 * 60 + 3 * 60 + 4) * 1000);

    render(<TtlCountdown deadlineUtc={deadline.toISOString()} />);

    expect(screen.getByRole('timer')).toHaveTextContent(/1d\s+2h\s+3m\s+4s/);
  });

  it('is aria-live="off" on ordinary ticks (no spam)', () => {
    const now = new Date('2026-04-18T12:00:00.000Z');
    vi.setSystemTime(now);
    const deadline = new Date(now.getTime() + 48 * 60 * 60 * 1000);

    render(<TtlCountdown deadlineUtc={deadline.toISOString()} />);

    const timer = screen.getByRole('timer');
    expect(timer).toHaveAttribute('aria-live', 'off');

    // Advance one second — still above 24h, still aria-live=off.
    act(() => vi.advanceTimersByTime(1000));
    expect(timer).toHaveAttribute('aria-live', 'off');
  });

  it('flips to aria-live="polite" on 24h threshold cross, then back to "off"', () => {
    // Start at 24h01s — next tick crosses the 24h boundary.
    const now = new Date('2026-04-18T12:00:00.000Z');
    vi.setSystemTime(now);
    const deadline = new Date(now.getTime() + (24 * 60 * 60 + 1) * 1000);

    render(<TtlCountdown deadlineUtc={deadline.toISOString()} />);
    const timer = screen.getByRole('timer');
    expect(timer).toHaveAttribute('aria-live', 'off');

    // First tick crosses into <24h.
    act(() => vi.advanceTimersByTime(2000));
    expect(timer).toHaveAttribute('aria-live', 'polite');

    // Subsequent tick — stays < 24h but NOT a cross; back to off.
    act(() => vi.advanceTimersByTime(1000));
    expect(timer).toHaveAttribute('aria-live', 'off');
  });

  it('applies amber styling < 24h and red styling < 2h', () => {
    // 23h remaining → amber.
    const now = new Date('2026-04-18T12:00:00.000Z');
    vi.setSystemTime(now);
    const deadline = new Date(now.getTime() + 23 * 60 * 60 * 1000);

    const { unmount } = render(<TtlCountdown deadlineUtc={deadline.toISOString()} />);
    expect(screen.getByRole('timer').className).toMatch(/amber/);
    unmount();

    // 1h remaining → red.
    const deadlineUrgent = new Date(now.getTime() + 60 * 60 * 1000);
    render(<TtlCountdown deadlineUtc={deadlineUrgent.toISOString()} />);
    expect(screen.getByRole('timer').className).toMatch(/red/);
  });

  it('invokes onThresholdCross exactly once per boundary', () => {
    const now = new Date('2026-04-18T12:00:00.000Z');
    vi.setSystemTime(now);
    // Start at 24h01s so 2s forward crosses 24h.
    const deadline = new Date(now.getTime() + (24 * 60 * 60 + 1) * 1000);
    const onCross = vi.fn();
    render(
      <TtlCountdown deadlineUtc={deadline.toISOString()} onThresholdCross={onCross} />,
    );

    act(() => vi.advanceTimersByTime(2000));
    expect(onCross).toHaveBeenCalledWith('warn');
    expect(onCross).toHaveBeenCalledTimes(1);

    // Advance another tick — still <24h, shouldn't re-fire.
    act(() => vi.advanceTimersByTime(1000));
    expect(onCross).toHaveBeenCalledTimes(1);
  });
});
