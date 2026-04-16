'use client';

// Passenger selector (B2C-03, FLTB-01).
//
// Airline rules enforced in-component (no round-trip):
//   - adults  >= 1  (must be at least one adult)
//   - total   <= 9  (fits single PNR)
//   - infantsLap      <= adults             (one-lap-per-adult rule)
//   - infantsLap + infantsSeat <= 2 * adults (2 infants per adult max)
//
// Labels are UI-SPEC verbatim — the test suite greps for exact strings.
// Errors surface inline in red-600 12px (UI-SPEC §Inline Errors) — never
// as toast, because multiple rules can trip simultaneously and a stack
// of toasts would bury the underlying cause.

import { Minus, Plus, Users } from 'lucide-react';
import { useMemo, useState } from 'react';

export interface PassengerCounts {
  adults: number;
  children: number;
  infantsLap: number;
  infantsSeat: number;
}

export interface PassengerSelectorProps {
  value: PassengerCounts;
  onChange: (next: PassengerCounts) => void;
}

const MAX_TOTAL = 9;

function totalPax(c: PassengerCounts): number {
  return c.adults + c.children + c.infantsLap + c.infantsSeat;
}

interface RuleCheck {
  canIncrement: Record<keyof PassengerCounts, boolean>;
  canDecrement: Record<keyof PassengerCounts, boolean>;
  errors: string[];
}

function evaluateRules(c: PassengerCounts): RuleCheck {
  const total = totalPax(c);
  const atCap = total >= MAX_TOTAL;
  const infantsCombined = c.infantsLap + c.infantsSeat;

  const canIncrement: RuleCheck['canIncrement'] = {
    adults: !atCap,
    children: !atCap,
    // infantsLap blocked when lap >= adults OR combined infants >= 2*adults OR total cap
    infantsLap:
      !atCap &&
      c.infantsLap < c.adults &&
      infantsCombined < 2 * c.adults,
    // infantsSeat blocked when combined >= 2*adults OR total cap
    infantsSeat: !atCap && infantsCombined < 2 * c.adults,
  };

  const canDecrement: RuleCheck['canDecrement'] = {
    adults: c.adults > 1, // at least one adult required
    children: c.children > 0,
    infantsLap: c.infantsLap > 0,
    infantsSeat: c.infantsSeat > 0,
  };

  // Emit rule copy when a boundary is reached OR violated. Tests grep the
  // strings via regex, so the surface text must be present whenever the
  // corresponding increment button is disabled for that reason.
  const errors: string[] = [];
  if (atCap) errors.push(`Maximum ${MAX_TOTAL} passengers per booking.`);
  if (c.infantsLap >= c.adults && c.adults > 0)
    errors.push('At most one infant on lap per adult.');
  if (infantsCombined >= 2 * c.adults && c.adults > 0)
    errors.push('At most two infants per adult (lap + seat combined).');
  return { canIncrement, canDecrement, errors };
}

interface RowProps {
  label: string;
  sublabel?: string;
  field: keyof PassengerCounts;
  value: number;
  onDec: () => void;
  onInc: () => void;
  canDec: boolean;
  canInc: boolean;
}

function Row({ label, sublabel, value, field, onDec, onInc, canDec, canInc }: RowProps) {
  return (
    <div className="flex items-center justify-between gap-6 py-2">
      <div className="flex flex-col">
        <span className="text-sm font-medium">{label}</span>
        {sublabel && <span className="text-xs text-muted-foreground">{sublabel}</span>}
      </div>
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

export interface PassengerSelectorProps {
  value: PassengerCounts;
  onChange: (next: PassengerCounts) => void;
  /**
   * When true the counter rows render inline (no popover). Used both by
   * the unit test harness and by the mobile full-screen search sheet
   * where we want the counters visible without a second tap.
   */
  defaultOpen?: boolean;
  /** Render WITHOUT the popover shell — for bottom-sheet layouts. */
  inline?: boolean;
}

function Controls({
  value,
  onChange,
  rules,
}: {
  value: PassengerCounts;
  onChange: (next: PassengerCounts) => void;
  rules: RuleCheck;
}) {
  function bump(field: keyof PassengerCounts, delta: number) {
    onChange({ ...value, [field]: Math.max(0, value[field] + delta) });
  }
  return (
    <>
      <Row
        label="Adults"
        sublabel="12+"
        field="adults"
        value={value.adults}
        onDec={() => bump('adults', -1)}
        onInc={() => bump('adults', 1)}
        canDec={rules.canDecrement.adults}
        canInc={rules.canIncrement.adults}
      />
      <Row
        label="Children (2–11)"
        field="children"
        value={value.children}
        onDec={() => bump('children', -1)}
        onInc={() => bump('children', 1)}
        canDec={rules.canDecrement.children}
        canInc={rules.canIncrement.children}
      />
      <Row
        label="Infants on lap (<2)"
        field="infantsLap"
        value={value.infantsLap}
        onDec={() => bump('infantsLap', -1)}
        onInc={() => bump('infantsLap', 1)}
        canDec={rules.canDecrement.infantsLap}
        canInc={rules.canIncrement.infantsLap}
      />
      <Row
        label="Infants in seat (<2)"
        field="infantsSeat"
        value={value.infantsSeat}
        onDec={() => bump('infantsSeat', -1)}
        onInc={() => bump('infantsSeat', 1)}
        canDec={rules.canDecrement.infantsSeat}
        canInc={rules.canIncrement.infantsSeat}
      />
      {rules.errors.length > 0 && (
        <ul className="mt-2 space-y-0.5 border-t border-border pt-2">
          {rules.errors.map((msg) => (
            <li key={msg} className="text-xs text-red-600" role="alert">
              {msg}
            </li>
          ))}
        </ul>
      )}
    </>
  );
}

export function PassengerSelector({
  value,
  onChange,
  defaultOpen = true,
  inline = false,
}: PassengerSelectorProps) {
  const [open, setOpen] = useState(defaultOpen);
  const rules = useMemo(() => evaluateRules(value), [value]);
  const summary = `${totalPax(value)} ${totalPax(value) === 1 ? 'traveler' : 'travelers'}`;

  if (inline) {
    return (
      <div className="rounded-md border border-border bg-background p-3">
        <Controls value={value} onChange={onChange} rules={rules} />
      </div>
    );
  }

  return (
    <div className="relative">
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        aria-haspopup="dialog"
        aria-expanded={open}
        className="flex w-full items-center gap-2 rounded-md border border-input bg-background px-3 py-2 text-start text-sm"
      >
        <Users size={16} className="text-muted-foreground" />
        <span className="flex-1">{summary}</span>
      </button>

      {open && (
        <div
          role="dialog"
          aria-label="Passenger counts"
          className="absolute end-0 z-20 mt-2 w-80 rounded-md border border-border bg-popover p-3 shadow-md"
        >
          <Controls value={value} onChange={onChange} rules={rules} />
        </div>
      )}
    </div>
  );
}
