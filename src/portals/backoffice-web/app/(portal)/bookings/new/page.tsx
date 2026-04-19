// Plan 06-02 Task 1 (BO-02) — 3-step manual booking entry wizard.
//
// Route: /bookings/new — reachable from the unified booking list.
// Gate: ops-cs OR ops-admin (middleware + this RSC + backend policy).
//
// The wizard renders in a client component (ManualBookingWizard) to
// support multi-step state + field validation without server round
// trips. The final submit POSTs to /api/bookings/manual which proxies
// to BookingService under BackofficeCsPolicy.

import { redirect } from 'next/navigation';
import { auth } from '@/lib/auth';
import { isOpsAdmin, isOpsCs } from '@/lib/rbac';
import { ManualBookingWizard } from './manual-booking-wizard';

export const dynamic = 'force-dynamic';

export default async function NewManualBookingPage() {
  const session = await auth();
  if (!session) redirect('/login');
  if (!isOpsCs(session) && !isOpsAdmin(session)) redirect('/forbidden');

  return (
    <section className="flex flex-col gap-4 p-6">
      <header className="flex flex-col gap-1">
        <h1 className="text-xl font-semibold text-slate-900">
          New Manual Booking
        </h1>
        <p className="text-sm text-slate-500">
          Phone / walk-in sale. Channel is stamped
          <span className="mx-1 inline-flex rounded bg-amber-100 px-2 py-0.5 text-xs font-semibold text-amber-900">
            Manual
          </span>
          on submit — no GDS call is made. Supplier reference duplicates
          within 24 hours are rejected.
        </p>
      </header>
      <ManualBookingWizard />
    </section>
  );
}
