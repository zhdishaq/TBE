// Plan 05-04 Task 3 — BookingsTable.
//
// Tabular view of the bookings list. Each row links to the detail page.
// Pure presentational; fetching, paging, and filtering happen upstream.

import Link from 'next/link';
import { formatMoney } from '@/lib/format-money';

export interface BookingRow {
  id: string;
  reference: string;
  pnr: string | null;
  clientName: string | null;
  status: string;
  createdAtUtc: string;
  grossAmount: number | null;
  currency: string | null;
  ticketingDeadlineUtc: string | null;
}

interface BookingsTableProps {
  rows: BookingRow[];
}

export function BookingsTable({ rows }: BookingsTableProps) {
  if (rows.length === 0) {
    return (
      <div className="rounded-md border border-border bg-muted/30 p-8 text-center text-sm text-muted-foreground">
        No bookings match the current filters.
      </div>
    );
  }
  return (
    <table className="w-full border-collapse text-sm">
      <thead className="bg-muted/50 text-xs uppercase text-muted-foreground">
        <tr>
          <th className="px-4 py-2 text-left font-medium">Reference</th>
          <th className="px-4 py-2 text-left font-medium">PNR</th>
          <th className="px-4 py-2 text-left font-medium">Client</th>
          <th className="px-4 py-2 text-left font-medium">Status</th>
          <th className="px-4 py-2 text-right font-medium">Total</th>
          <th className="px-4 py-2 text-left font-medium">Created</th>
        </tr>
      </thead>
      <tbody>
        {rows.map((r) => (
          <tr
            key={r.id}
            className="border-t border-border transition-colors hover:bg-accent/50"
          >
            <td className="px-4 py-2">
              <Link
                href={`/bookings/${r.id}`}
                className="font-mono text-indigo-600 hover:underline"
              >
                {r.reference}
              </Link>
            </td>
            <td className="px-4 py-2 font-mono">{r.pnr ?? '—'}</td>
            <td className="px-4 py-2">{r.clientName ?? '—'}</td>
            <td className="px-4 py-2">{r.status}</td>
            <td className="px-4 py-2 text-right tabular-nums">
              {r.grossAmount !== null && r.currency
                ? formatMoney(r.grossAmount, r.currency)
                : '—'}
            </td>
            <td className="px-4 py-2 text-xs text-muted-foreground">
              {new Date(r.createdAtUtc).toLocaleDateString('en-GB')}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
