// Plan 06-04 Task 3 (CRM-02) — Agencies list shell.
//
// ops-finance + ops-admin read; ops-admin alone mutates credit limit.
// The list endpoint is deferred to the follow-up plan; this page is
// the nav anchor and role gate.

import { redirect } from 'next/navigation';
import Link from 'next/link';
import { auth } from '@/lib/auth';
import { isOpsFinance, isOpsAdmin } from '@/lib/rbac';

export const dynamic = 'force-dynamic';

export default async function AgenciesListPage() {
  const session = await auth();
  if (!session) redirect('/login');
  if (!isOpsFinance(session) && !isOpsAdmin(session)) redirect('/forbidden');

  return (
    <main className="mx-auto w-full max-w-6xl p-6">
      <header className="mb-6">
        <h1 className="text-2xl font-semibold text-slate-900 dark:text-slate-100">
          Agencies
        </h1>
        <p className="text-sm text-slate-500">
          B2B agency directory + wallet + credit-limit overrides.
        </p>
      </header>

      <div
        role="alert"
        aria-live="polite"
        className="rounded-md border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900 dark:border-amber-900 dark:bg-amber-950/40 dark:text-amber-100"
      >
        <p className="font-medium">List endpoint deferred.</p>
        <p className="mt-1">
          Navigate to an agency directly via{' '}
          <code className="rounded bg-amber-100 px-1 dark:bg-amber-900">
            /agencies/{'{'}id{'}'}
          </code>
          . The credit-limit PATCH endpoint (Plan 06-04 CRM-02) is already
          wired server-side via{' '}
          <Link href="/finance" className="underline">
            /finance
          </Link>{' '}
          / BackofficeService AgencyWalletsController.
        </p>
      </div>
    </main>
  );
}
