// Plan 05-04 Task 3 — RecentBookingsCard.
//
// Compact list of the 5 most recent bookings for the agency, linked to
// the booking detail. Part of the 2-column dashboard grid (D-44).

import Link from 'next/link';

export interface RecentBookingRow {
  id: string;
  reference: string;
  clientName: string;
  status: string;
}

interface RecentBookingsCardProps {
  bookings: RecentBookingRow[];
}

export function RecentBookingsCard({ bookings }: RecentBookingsCardProps) {
  return (
    <section
      aria-labelledby="recent-bookings-heading"
      className="rounded-lg border border-border bg-card p-6 shadow-sm"
    >
      <div className="mb-4 flex items-center justify-between">
        <h2
          id="recent-bookings-heading"
          className="text-lg font-semibold text-foreground"
        >
          Recent bookings
        </h2>
        <Link href="/bookings" className="text-sm text-indigo-600 hover:underline">
          View all
        </Link>
      </div>
      {bookings.length === 0 ? (
        <p className="text-sm text-muted-foreground">No bookings yet.</p>
      ) : (
        <table className="w-full text-sm">
          <thead className="text-xs uppercase text-muted-foreground">
            <tr>
              <th className="py-2 text-left font-medium">Reference</th>
              <th className="py-2 text-left font-medium">Client</th>
              <th className="py-2 text-right font-medium">Status</th>
            </tr>
          </thead>
          <tbody>
            {bookings.map((b) => (
              <tr key={b.id} className="border-t border-border">
                <td className="py-2">
                  <Link
                    href={`/bookings/${b.id}`}
                    className="font-mono hover:underline"
                  >
                    {b.reference}
                  </Link>
                </td>
                <td className="py-2">{b.clientName}</td>
                <td className="py-2 text-right text-xs text-muted-foreground">
                  {b.status}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}
