// Plan 06-02 Task 3 (BO-06) — payment reconciliation queue page.
//
// Route: /finance/reconciliation — any ops-* role can read; resolve is
// gated in the backend on BackofficeFinancePolicy.

import { redirect } from 'next/navigation';
import { auth } from '@/lib/auth';
import { isOpsRead, hasAnyRole } from '@/lib/rbac';
import { ReconciliationQueue } from './reconciliation-queue';

export const dynamic = 'force-dynamic';

export default async function ReconciliationPage() {
  const session = await auth();
  if (!session) redirect('/login');
  if (!isOpsRead(session)) redirect('/forbidden');

  const canResolve = hasAnyRole(session, ['ops-finance', 'ops-admin']);

  return (
    <section className="flex flex-col gap-4 p-6">
      <header className="flex flex-col gap-1">
        <h1 className="text-xl font-semibold text-slate-900">
          Payment Reconciliation
        </h1>
        <p className="text-sm text-slate-500">
          Discrepancies between Stripe charge events and the wallet
          ledger surfaced by the nightly reconciliation job. Click a row
          to view the side-by-side payload diff and resolve.
        </p>
      </header>
      <ReconciliationQueue canResolve={canResolve} />
    </section>
  );
}
