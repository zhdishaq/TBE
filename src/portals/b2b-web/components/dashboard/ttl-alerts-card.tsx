// Plan 05-04 Task 3 — TtlAlertsCard.
//
// Renders the TTL-alerts bucket pair on the /dashboard grid (UI-SPEC
// §Dashboard, D-44). Two lists, amber for <24h warn + red for <2h urgent;
// each entry links to /bookings/{id}.

import Link from 'next/link';
import { cn } from '@/lib/utils';

export interface TtlAlertsRow {
  bookingId: string;
  pnr: string;
  hoursRemaining: number;
  clientName?: string;
}

interface TtlAlertsCardProps {
  warn: TtlAlertsRow[];
  urgent: TtlAlertsRow[];
}

export function TtlAlertsCard({ warn, urgent }: TtlAlertsCardProps) {
  const hasAny = warn.length + urgent.length > 0;
  return (
    <section
      aria-labelledby="ttl-alerts-heading"
      className="rounded-lg border border-border bg-card p-6 shadow-sm"
    >
      <h2
        id="ttl-alerts-heading"
        className="mb-4 text-lg font-semibold text-foreground"
      >
        Ticketing deadlines
      </h2>

      {!hasAny ? (
        <p className="text-sm text-muted-foreground">
          No upcoming ticketing deadlines in the next 24 hours.
        </p>
      ) : (
        <div className="flex flex-col gap-4">
          {urgent.length > 0 && (
            <Bucket
              tone="red"
              title="Urgent — under 2 hours"
              rows={urgent}
            />
          )}
          {warn.length > 0 && (
            <Bucket
              tone="amber"
              title="Approaching — under 24 hours"
              rows={warn}
            />
          )}
        </div>
      )}
    </section>
  );
}

interface BucketProps {
  tone: 'amber' | 'red';
  title: string;
  rows: TtlAlertsRow[];
}

function Bucket({ tone, title, rows }: BucketProps) {
  const toneClasses =
    tone === 'red'
      ? 'border-red-300 bg-red-50 text-red-900 dark:border-red-900 dark:bg-red-950/30 dark:text-red-200'
      : 'border-amber-300 bg-amber-50 text-amber-900 dark:border-amber-900 dark:bg-amber-950/30 dark:text-amber-200';
  return (
    <div
      className={cn(
        'rounded-md border px-4 py-3',
        toneClasses,
      )}
    >
      <p className="mb-2 text-sm font-medium">{title}</p>
      <ul className="flex flex-col gap-1 text-sm">
        {rows.map((r) => (
          <li key={r.bookingId} className="flex items-center justify-between">
            <Link
              href={`/bookings/${r.bookingId}`}
              className="font-mono hover:underline"
            >
              {r.pnr}
            </Link>
            <span className="tabular-nums">
              {formatHours(r.hoursRemaining)}
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}

function formatHours(hours: number): string {
  if (hours < 1) {
    return `${Math.round(hours * 60)}m`;
  }
  return `${hours.toFixed(1)}h`;
}
