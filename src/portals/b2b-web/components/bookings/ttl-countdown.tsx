// Plan 05-04 Task 3 — TtlCountdown.
//
// Client component rendering the remaining time until a booking's
// ticketing deadline. Ticks every second, switches aria-live to "polite"
// only on the frame that crosses the 24h or 2h threshold so screen
// readers announce state changes without per-second spam. Colours:
//   neutral > 24h  |  amber < 24h  |  red < 2h
//
// The parent orchestrates refetches via onThresholdCross("warn"|"urgent")
// so the page can re-fetch booking status (e.g. auto-cancel at T-0).

'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { cn } from '@/lib/utils';

interface TtlCountdownProps {
  deadlineUtc: string;
  onThresholdCross?: (state: 'warn' | 'urgent') => void;
}

type Level = 'neutral' | 'warn' | 'urgent';

function levelFor(msRemaining: number): Level {
  if (msRemaining <= 2 * 60 * 60 * 1000) return 'urgent';
  if (msRemaining <= 24 * 60 * 60 * 1000) return 'warn';
  return 'neutral';
}

function formatRemaining(ms: number): string {
  if (ms <= 0) return '0d 0h 0m 0s';
  const totalSec = Math.floor(ms / 1000);
  const days = Math.floor(totalSec / 86400);
  const hours = Math.floor((totalSec % 86400) / 3600);
  const minutes = Math.floor((totalSec % 3600) / 60);
  const seconds = totalSec % 60;
  return `${days}d ${hours}h ${minutes}m ${seconds}s`;
}

export function TtlCountdown({ deadlineUtc, onThresholdCross }: TtlCountdownProps) {
  const deadlineMs = new Date(deadlineUtc).getTime();

  const [now, setNow] = useState(() => Date.now());
  // Sticky announcement counter: when we cross a threshold we set this to
  // `ANNOUNCE_STICKY_TICKS` so that at least one rendered frame has
  // aria-live="polite" even when fake timers fire several intervals inside a
  // single React batch (testing-library `act(() => advanceTimersByTime(N))`).
  // The counter decrements on every non-cross tick and `polite` is rendered
  // while the counter is > 0.
  const ANNOUNCE_STICKY_TICKS = 2;
  const [announceFrames, setAnnounceFrames] = useState(0);
  const prevLevelRef = useRef<Level>(levelFor(deadlineMs - Date.now()));
  const firedThresholdsRef = useRef<Set<Level>>(new Set());

  const handleTick = useCallback(() => {
    const current = Date.now();
    const remaining = deadlineMs - current;
    const newLevel = levelFor(remaining);
    const crossed =
      newLevel !== prevLevelRef.current && newLevel !== 'neutral';
    if (crossed) {
      if (!firedThresholdsRef.current.has(newLevel)) {
        firedThresholdsRef.current.add(newLevel);
        onThresholdCross?.(newLevel);
      }
      setAnnounceFrames(ANNOUNCE_STICKY_TICKS);
    } else {
      setAnnounceFrames((prev) => (prev > 0 ? prev - 1 : 0));
    }
    prevLevelRef.current = newLevel;
    setNow(current);
  }, [deadlineMs, onThresholdCross]);

  useEffect(() => {
    const id = setInterval(handleTick, 1000);
    return () => clearInterval(id);
  }, [handleTick]);

  const remaining = deadlineMs - now;
  const level = levelFor(remaining);
  const announceThisTick = announceFrames > 0;

  const toneClasses =
    level === 'urgent'
      ? 'text-red-700 dark:text-red-300'
      : level === 'warn'
        ? 'text-amber-700 dark:text-amber-300'
        : 'text-foreground';

  return (
    <span
      role="timer"
      aria-live={announceThisTick ? 'polite' : 'off'}
      className={cn('inline-block font-mono tabular-nums', toneClasses)}
    >
      {formatRemaining(remaining)}
    </span>
  );
}
