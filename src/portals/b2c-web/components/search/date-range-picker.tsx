'use client';

// Date-range picker (B2C-03, UI-SPEC §Date Range).
//
// Pattern copied from `.planning/phases/04-b2c-portal-customer-facing/04-PATTERNS.md`
// §date-range-picker and the starterKit layout-1 page.
//
// Constraints from UI-SPEC:
//   - min date = today (no past dates; zero out to midnight for DST safety)
//   - max date = today + 361 days (most GDS fare engines cap at 361d)
//
// We render a trigger button showing the selected range, and a Popover
// with react-day-picker in range mode (2 months side-by-side on desktop,
// 1 month on mobile — CSS handles the responsive case via the Calendar
// component's own internals).

import { useState } from 'react';
import { CalendarDays } from 'lucide-react';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { Calendar } from '@/components/ui/calendar';
import { Button } from '@/components/ui/button';

export interface DateRangeValue {
  from: Date | null;
  to: Date | null;
}

export interface DateRangePickerProps {
  value: DateRangeValue;
  onChange: (next: DateRangeValue) => void;
  className?: string;
}

function formatDate(d: Date): string {
  return d.toLocaleDateString(undefined, {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  });
}

export function DateRangePicker({ value, onChange, className }: DateRangePickerProps) {
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  const maxDate = new Date(today);
  maxDate.setDate(maxDate.getDate() + 361);

  const [open, setOpen] = useState(false);
  const [temp, setTemp] = useState<DateRangeValue>(value);

  function apply() {
    onChange(temp);
    setOpen(false);
  }
  function reset() {
    setTemp({ from: null, to: null });
  }

  const label =
    value.from && value.to
      ? `${formatDate(value.from)} → ${formatDate(value.to)}`
      : value.from
      ? `${formatDate(value.from)} → …`
      : 'Select dates';

  return (
    <div className={className}>
      <Popover open={open} onOpenChange={setOpen}>
        <PopoverTrigger asChild>
          <Button
            variant="outline"
            className="flex w-full items-center justify-start gap-2"
            aria-label="Date range"
          >
            <CalendarDays size={16} className="me-0.5 text-muted-foreground" />
            <span className="truncate text-sm">{label}</span>
          </Button>
        </PopoverTrigger>
        <PopoverContent className="w-auto p-0" align="end">
          <Calendar
            mode="range"
            defaultMonth={temp.from ?? undefined}
            selected={{ from: temp.from ?? undefined, to: temp.to ?? undefined }}
            onSelect={(range: { from?: Date; to?: Date } | undefined) =>
              setTemp({ from: range?.from ?? null, to: range?.to ?? null })
            }
            numberOfMonths={2}
            disabled={{ before: today, after: maxDate }}
          />
          <div className="flex items-center justify-end gap-1.5 border-t border-border p-3">
            <Button variant="outline" onClick={reset}>
              Reset
            </Button>
            <Button onClick={apply}>Apply</Button>
          </div>
        </PopoverContent>
      </Popover>
    </div>
  );
}
