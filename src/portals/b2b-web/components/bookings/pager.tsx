// Plan 05-04 Task 3 — BookingsPager.
//
// Pure presentational pager for the bookings table. 20/50/100 page-size
// select per D-44; prev/next buttons bounded by `total`. URL state-sync
// lives in the parent (nuqs).

'use client';

interface BookingsPagerProps {
  page: number;
  size: number;
  total: number;
  onNavigate: (page: number) => void;
  onSizeChange: (size: number) => void;
}

const SIZES = [20, 50, 100] as const;

export function BookingsPager({
  page,
  size,
  total,
  onNavigate,
  onSizeChange,
}: BookingsPagerProps) {
  const lastPage = Math.max(1, Math.ceil(total / size));
  const atFirst = page <= 1;
  const atLast = page >= lastPage;

  return (
    <div className="flex flex-wrap items-center justify-between gap-3 border-t border-border px-4 py-3 text-sm">
      <label className="flex items-center gap-2 text-muted-foreground">
        <span id="pager-size-label">Page size</span>
        <select
          aria-labelledby="pager-size-label"
          aria-label="Page size"
          value={size}
          onChange={(e) => onSizeChange(Number(e.target.value))}
          className="h-8 rounded-md border border-border bg-background px-2 text-sm"
        >
          {SIZES.map((s) => (
            <option key={s} value={s}>
              {s}
            </option>
          ))}
        </select>
      </label>

      <div className="flex items-center gap-3">
        <span className="text-muted-foreground" aria-live="polite">
          Page {page} of {lastPage} · {total.toLocaleString()} bookings
        </span>
        <button
          type="button"
          aria-label="Previous page"
          disabled={atFirst}
          onClick={() => onNavigate(page - 1)}
          className="h-8 rounded-md border border-border px-3 text-sm disabled:cursor-not-allowed disabled:opacity-50"
        >
          Previous
        </button>
        <button
          type="button"
          aria-label="Next page"
          disabled={atLast}
          onClick={() => onNavigate(page + 1)}
          className="h-8 rounded-md border border-border px-3 text-sm disabled:cursor-not-allowed disabled:opacity-50"
        >
          Next
        </button>
      </div>
    </div>
  );
}
