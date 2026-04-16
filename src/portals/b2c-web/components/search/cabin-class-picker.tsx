'use client';

// Cabin-class picker — segmented pill group (UI-SPEC §Search Form).
//
// Four options: Economy / Premium economy / Business / First. Single
// selection. Mirrors the Metronic `tabs` visual style but uses plain
// buttons so the selected state is controllable (Tabs is defaultValue-
// only without extra props).

import { cn } from '@/lib/utils';
import type { CabinClass } from '@/lib/search-params';

export interface CabinClassPickerProps {
  value: CabinClass;
  onChange: (next: CabinClass) => void;
  className?: string;
}

const OPTIONS: Array<{ value: CabinClass; label: string }> = [
  { value: 'economy', label: 'Economy' },
  { value: 'premium', label: 'Premium economy' },
  { value: 'business', label: 'Business' },
  { value: 'first', label: 'First' },
];

export function CabinClassPicker({ value, onChange, className }: CabinClassPickerProps) {
  return (
    <div
      role="radiogroup"
      aria-label="Cabin class"
      className={cn('flex flex-wrap items-center gap-1 rounded-md border border-border bg-background p-1', className)}
    >
      {OPTIONS.map((opt) => {
        const selected = opt.value === value;
        return (
          <button
            key={opt.value}
            type="button"
            role="radio"
            aria-checked={selected}
            onClick={() => onChange(opt.value)}
            className={cn(
              'rounded px-3 py-1.5 text-sm transition-colors',
              selected
                ? 'bg-primary text-primary-foreground'
                : 'text-foreground hover:bg-muted',
            )}
          >
            {opt.label}
          </button>
        );
      })}
    </div>
  );
}
