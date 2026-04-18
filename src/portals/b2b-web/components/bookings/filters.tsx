// Plan 05-04 Task 3 — BookingsFilters.
//
// Filter form for the bookings list: client-name contains, PNR equals,
// status select, from/to date bracket. nuqs-synced (URL shareable).
// Debounce handled by the parent; this component is a controlled form.

'use client';

import type { FormEvent } from 'react';

export interface BookingsFiltersValue {
  client: string;
  pnr: string;
  status: string;
  from: string;
  to: string;
}

interface BookingsFiltersProps {
  value: BookingsFiltersValue;
  onChange: (v: BookingsFiltersValue) => void;
}

const STATUSES = [
  { value: '', label: 'All statuses' },
  { value: 'Pending', label: 'Pending' },
  { value: 'Ticketed', label: 'Ticketed' },
  { value: 'Cancelled', label: 'Cancelled' },
  { value: 'Failed', label: 'Failed' },
];

export function BookingsFilters({ value, onChange }: BookingsFiltersProps) {
  function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
  }
  function update<K extends keyof BookingsFiltersValue>(
    k: K,
    v: BookingsFiltersValue[K],
  ) {
    onChange({ ...value, [k]: v });
  }
  return (
    <form
      onSubmit={handleSubmit}
      className="grid grid-cols-1 gap-3 rounded-lg border border-border bg-card p-4 sm:grid-cols-5"
    >
      <label className="text-sm">
        <span className="mb-1 block text-muted-foreground">Client</span>
        <input
          type="text"
          value={value.client}
          onChange={(e) => update('client', e.target.value)}
          placeholder="Contains…"
          className="h-9 w-full rounded-md border border-border bg-background px-3 text-sm"
        />
      </label>
      <label className="text-sm">
        <span className="mb-1 block text-muted-foreground">PNR</span>
        <input
          type="text"
          value={value.pnr}
          onChange={(e) => update('pnr', e.target.value.toUpperCase())}
          placeholder="ABC123"
          className="h-9 w-full rounded-md border border-border bg-background px-3 text-sm font-mono uppercase"
        />
      </label>
      <label className="text-sm">
        <span className="mb-1 block text-muted-foreground">Status</span>
        <select
          value={value.status}
          onChange={(e) => update('status', e.target.value)}
          className="h-9 w-full rounded-md border border-border bg-background px-2 text-sm"
        >
          {STATUSES.map((s) => (
            <option key={s.value} value={s.value}>
              {s.label}
            </option>
          ))}
        </select>
      </label>
      <label className="text-sm">
        <span className="mb-1 block text-muted-foreground">From</span>
        <input
          type="date"
          value={value.from}
          onChange={(e) => update('from', e.target.value)}
          className="h-9 w-full rounded-md border border-border bg-background px-3 text-sm"
        />
      </label>
      <label className="text-sm">
        <span className="mb-1 block text-muted-foreground">To</span>
        <input
          type="date"
          value={value.to}
          onChange={(e) => update('to', e.target.value)}
          className="h-9 w-full rounded-md border border-border bg-background px-3 text-sm"
        />
      </label>
    </form>
  );
}
