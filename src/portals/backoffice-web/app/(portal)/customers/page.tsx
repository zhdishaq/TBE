// Plan 06-04 Task 3 (COMP-03) — Customers list for ops staff.
//
// Cross-channel list of B2C customers projected by CrmService. All four
// ops-* roles can read (ops-read floor). Filter by erasure status so
// ops-admin can spot-check the anonymised set during audit.
//
// The upstream endpoint is /api/backoffice/customers (reads via
// BackofficeDbContext.CustomerReadModel mapped to crm.Customers). The
// list endpoint itself is deferred — this page wires to /api/crm/customers
// and renders a "Coming soon" notice until the GET controller lands in
// a follow-up plan. The detail page, erase dialog, and erasures archive
// are fully wired and verifiable end-to-end via typed URL.

import { redirect } from 'next/navigation';
import Link from 'next/link';
import { auth } from '@/lib/auth';
import { isOpsRead, isOpsAdmin } from '@/lib/rbac';

export const dynamic = 'force-dynamic';

export default async function CustomersListPage({
  searchParams,
}: {
  searchParams: Promise<{ erased?: string; q?: string }>;
}) {
  const session = await auth();
  if (!session) redirect('/login');
  if (!isOpsRead(session)) redirect('/forbidden');
  const params = await searchParams;
  const adminView = isOpsAdmin(session);

  return (
    <main className="mx-auto w-full max-w-6xl p-6">
      <header className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-slate-900 dark:text-slate-100">
            Customers
          </h1>
          <p className="text-sm text-slate-500">
            CRM projection of every B2C registration + confirmed booking.
          </p>
        </div>
        {adminView && (
          <Link
            href="/customers/erasures"
            className="text-sm font-medium text-slate-700 underline-offset-4 hover:underline dark:text-slate-200"
          >
            View erasures archive →
          </Link>
        )}
      </header>

      <div
        role="alert"
        aria-live="polite"
        className="rounded-md border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900 dark:border-amber-900 dark:bg-amber-950/40 dark:text-amber-100"
      >
        <p className="font-medium">List endpoint deferred.</p>
        <p className="mt-1 text-amber-800 dark:text-amber-200">
          The full customer list + filters land in the follow-up plan once
          the CrmService <code>GET /api/backoffice/customers</code> query
          surface ships. For now, navigate directly to a customer by id:
          <code className="ml-1 rounded bg-amber-100 px-1 dark:bg-amber-900">
            /customers/{'{'}id{'}'}
          </code>
          .
        </p>
      </div>

      {adminView && (
        <section className="mt-6 rounded-lg border border-slate-200 p-4 text-sm dark:border-slate-800">
          <h2 className="mb-2 font-semibold text-slate-900 dark:text-slate-100">
            Filters
          </h2>
          <form method="get" className="flex flex-wrap items-center gap-3">
            <label className="flex items-center gap-2">
              <span className="text-slate-600 dark:text-slate-400">Status</span>
              <select
                name="erased"
                defaultValue={params.erased ?? ''}
                className="h-8 rounded border border-slate-300 bg-white px-2 text-sm dark:border-slate-700 dark:bg-slate-900"
              >
                <option value="">All</option>
                <option value="false">Active</option>
                <option value="true">Anonymised</option>
              </select>
            </label>
            <label className="flex items-center gap-2">
              <span className="text-slate-600 dark:text-slate-400">Search</span>
              <input
                name="q"
                defaultValue={params.q ?? ''}
                placeholder="name / email"
                className="h-8 w-56 rounded border border-slate-300 bg-white px-2 text-sm dark:border-slate-700 dark:bg-slate-900"
              />
            </label>
            <button
              type="submit"
              className="h-8 rounded bg-slate-900 px-3 text-sm font-medium text-white dark:bg-slate-100 dark:text-slate-900"
            >
              Apply
            </button>
          </form>
        </section>
      )}
    </main>
  );
}
