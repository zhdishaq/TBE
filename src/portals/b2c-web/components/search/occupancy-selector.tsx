'use client';

// Occupancy selector (HOTB-01).
//
// Popover with three counters:
//   - Rooms    (1-5)
//   - Adults   (1-9)
//   - Children (0-4)
//
// UI-SPEC labels are verbatim ("Rooms", "Adults", "Children"). Increment /
// decrement are disabled at the bounds rather than silently clamped so
// the user sees immediately why a further tap is ignored.
//
// The component is controlled — the parent form owns the OccupancySpec
// and passes it in via value/onChange. On popover close we don't flush
// a second onChange because every counter click already committed.

import { Bed, Minus, Plus, Users } from 'lucide-react';
import { useState } from 'react';
import type { OccupancySpec } from '@/types/hotel';

export interface OccupancySelectorProps {
  value: OccupancySpec;
  onChange: (next: OccupancySpec) => void;
  className?: string;
  /**
   * When true the rows render inline (no popover) — useful for mobile
   * bottom-sheet layouts where we want counters visible without a tap.
   */
  inline?: boolean;
}

export const ROOMS_MIN = 1;
export const ROOMS_MAX = 5;
export const ADULTS_MIN = 1;
export const ADULTS_MAX = 9;
export const CHILDREN_MIN = 0;
export const CHILDREN_MAX = 4;

interface RowProps {
  label: string;
  value: number;
  onDec: () => void;
  onInc: () => void;
  canDec: boolean;
  canInc: boolean;
  field: 'rooms' | 'adults' | 'children';
}

function Row({ label, value, onDec, onInc, canDec, canInc, field }: RowProps) {
  return (
    <div className="flex items-center justify-between gap-6 py-2">
      <span className="text-sm font-medium">{label}</span>
      <div className="flex items-center gap-2">
        <button
          type="button"
          onClick={onDec}
          disabled={!canDec}
          aria-label={`Decrease ${label.toLowerCase()}`}
          className="rounded-full border border-input p-1.5 disabled:cursor-not-allowed disabled:opacity-40"
          data-field={field}
        >
          <Minus size={14} />
        </button>
        <span
          aria-live="polite"
          className="min-w-6 text-center tabular-nums"
          data-testid={`count-${field}`}
        >
          {value}
        </span>
        <button
          type="button"
          onClick={onInc}
          disabled={!canInc}
          aria-label={`Increase ${label.toLowerCase()}`}
          className="rounded-full border border-input p-1.5 disabled:cursor-not-allowed disabled:opacity-40"
          data-field={field}
        >
          <Plus size={14} />
        </button>
      </div>
    </div>
  );
}

interface ControlsProps {
  value: OccupancySpec;
  onChange: (next: OccupancySpec) => void;
}

function Controls({ value, onChange }: ControlsProps) {
  function set(field: keyof OccupancySpec, delta: number) {
    const next: OccupancySpec = { ...value, [field]: value[field] + delta };
    onChange(next);
  }
  return (
    <>
      <Row
        label="Rooms"
        field="rooms"
        value={value.rooms}
        onDec={() => set('rooms', -1)}
        onInc={() => set('rooms', 1)}
        canDec={value.rooms > ROOMS_MIN}
        canInc={value.rooms < ROOMS_MAX}
      />
      <Row
        label="Adults"
        field="adults"
        value={value.adults}
        onDec={() => set('adults', -1)}
        onInc={() => set('adults', 1)}
        canDec={value.adults > ADULTS_MIN}
        canInc={value.adults < ADULTS_MAX}
      />
      <Row
        label="Children"
        field="children"
        value={value.children}
        onDec={() => set('children', -1)}
        onInc={() => set('children', 1)}
        canDec={value.children > CHILDREN_MIN}
        canInc={value.children < CHILDREN_MAX}
      />
    </>
  );
}

export function OccupancySelector({
  value,
  onChange,
  className,
  inline = false,
}: OccupancySelectorProps) {
  const [open, setOpen] = useState(false);
  const summary =
    `${value.rooms} ${value.rooms === 1 ? 'room' : 'rooms'}` +
    ` · ${value.adults + value.children} ${value.adults + value.children === 1 ? 'guest' : 'guests'}`;

  if (inline) {
    return (
      <div className={['rounded-md border border-border bg-background p-3', className].filter(Boolean).join(' ')}>
        <Controls value={value} onChange={onChange} />
      </div>
    );
  }

  return (
    <div className={['relative', className].filter(Boolean).join(' ')}>
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        aria-haspopup="dialog"
        aria-expanded={open}
        aria-label="Occupancy"
        className="flex w-full items-center gap-2 rounded-md border border-input bg-background px-3 py-2 text-start text-sm"
      >
        <Users size={16} className="text-muted-foreground" />
        <span className="flex-1">{summary}</span>
        <Bed size={14} className="text-muted-foreground" />
      </button>
      {open && (
        <div
          role="dialog"
          aria-label="Occupancy counts"
          className="absolute end-0 z-20 mt-2 w-80 rounded-md border border-border bg-popover p-3 shadow-md"
        >
          <Controls value={value} onChange={onChange} />
          <div className="mt-2 flex justify-end">
            <button
              type="button"
              className="text-sm font-medium text-blue-600 hover:underline"
              onClick={() => setOpen(false)}
            >
              Done
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
