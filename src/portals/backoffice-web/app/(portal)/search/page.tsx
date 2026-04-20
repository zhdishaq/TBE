// Plan 06-04 Task 3 (CRM-05) — Global Search results shell.
//
// Renders search hits for ?q=… across bookings, customers, agencies.
// The server-side global-search endpoint is deferred; this page is the
// nav anchor for the cmdk modal (Cmd/Ctrl+K) and exposes the raw query.

import { redirect } from 'next/navigation';
import { auth } from '@/lib/auth';
import { isOpsRead } from '@/lib/rbac';

export const dynamic = 'force-dynamic';

export default async function SearchPage({
  searchParams,
}: {
  searchParams: Promise<{ q?: string }>;
}) {
  const session = await auth();
  if (!session) redirect('/login');
  if (!isOpsRead(session)) redirect('/forbidden');

  const { q } = await searchParams;
  const query = (q ?? '').trim();

  return (
    <main className="mx-auto w-full max-w-5xl p-6">
      <header className="mb-6">
        <h1 className="text-2xl font-semibold text-slate-900 dark:text-slate-100">
          Search
        </h1>
        <p className="text-sm text-slate-500">
          {query ? `Showing results for "${query}"` : 'No query'}
        </p>
      </header>

      <form method="get" className="mb-6">
        <label className="block text-sm">
          <span className="mb-1 block font-medium text-slate-900 dark:text-slate-100">
            Find a booking, customer, or agency
          </span>
          <input
            name="q"
            defaultValue={query}
            placeholder="PNR / booking ref / email / agency name"
            className="w-full rounded border border-slate-300 bg-white px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-900"
            autoFocus
          />
        </label>
      </form>

      <div
        role="alert"
        aria-live="polite"
        className="rounded-md border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900 dark:border-amber-900 dark:bg-amber-950/40 dark:text-amber-100"
      >
        <p className="font-medium">Search endpoint deferred.</p>
        <p className="mt-1">
          The global-search RPC (keyed on the new PNR / customer-email /
          customer-id filtered indexes shipped in this plan) lands in the
          next iteration. The cmdk keyboard-shortcut modal (Cmd/Ctrl+K)
          will mount then.
        </p>
      </div>
    </main>
  );
}
