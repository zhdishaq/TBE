// Plan 06-04 Task 3 (CRM-04) — Upcoming Trips shell.
//
// Ops-facing "who flies tomorrow" view. Pulls bookings whose
// itinerary first-segment departure is within the next 7 days across
// all channels. ops-cs + ops-admin read. The GET surface is deferred
// to the follow-up plan; this page is the nav target + role gate.

import { redirect } from 'next/navigation';
import { auth } from '@/lib/auth';
import { isOpsCs, isOpsAdmin } from '@/lib/rbac';

export const dynamic = 'force-dynamic';

export default async function UpcomingTripsPage() {
  const session = await auth();
  if (!session) redirect('/login');
  if (!isOpsCs(session) && !isOpsAdmin(session)) redirect('/forbidden');

  return (
    <main className="mx-auto w-full max-w-6xl p-6">
      <header className="mb-6">
        <h1 className="text-2xl font-semibold text-slate-900 dark:text-slate-100">
          Upcoming trips
        </h1>
        <p className="text-sm text-slate-500">
          Departures within the next 7 days, cross-channel.
        </p>
      </header>

      <div
        role="alert"
        aria-live="polite"
        className="rounded-md border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900 dark:border-amber-900 dark:bg-amber-950/40 dark:text-amber-100"
      >
        <p className="font-medium">List endpoint deferred.</p>
        <p className="mt-1">
          The CRM-04 upcoming-trips projection is planned for the next
          iteration. The filtered index{' '}
          <code>IX_BookingSagaState_CustomerId</code> (shipped in this
          plan) plus a new projection key on first-segment departure time
          will back it.
        </p>
      </div>
    </main>
  );
}
