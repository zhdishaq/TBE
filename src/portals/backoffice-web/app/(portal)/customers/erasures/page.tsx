// Plan 06-04 Task 3 (COMP-03 / D-57) — Erasures archive page.
//
// ops-admin only. Lists the tombstones written by the CRM erasure
// consumer, ordered by ErasedAt DESC (backed by
// IX_CustomerErasureTombstones_ErasedAt). The list endpoint is
// deferred to the follow-up plan; this page renders the page chrome
// + empty-state messaging so the nav link from the Customers list is
// not broken.

import { redirect } from 'next/navigation';
import Link from 'next/link';
import { auth } from '@/lib/auth';
import { isOpsAdmin } from '@/lib/rbac';

export const dynamic = 'force-dynamic';

export default async function CustomerErasuresArchivePage() {
  const session = await auth();
  if (!session) redirect('/login');
  if (!isOpsAdmin(session)) redirect('/forbidden');

  return (
    <main className="mx-auto w-full max-w-5xl p-6">
      <nav className="mb-4 text-sm text-slate-500">
        <Link href="/customers" className="hover:underline">
          Customers
        </Link>
        <span className="mx-2">/</span>
        <span className="text-slate-800 dark:text-slate-200">Erasures</span>
      </nav>

      <header className="mb-6">
        <h1 className="text-2xl font-semibold text-slate-900 dark:text-slate-100">
          Erasures archive
        </h1>
        <p className="text-sm text-slate-500">
          Every completed GDPR erasure, ordered most recent first. Each
          tombstone records the hashed email, the actor, and the stated
          reason.
        </p>
      </header>

      <div
        role="alert"
        aria-live="polite"
        className="rounded-md border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900 dark:border-amber-900 dark:bg-amber-950/40 dark:text-amber-100"
      >
        <p className="font-medium">List endpoint deferred.</p>
        <p className="mt-1">
          The archive list query lands in the follow-up plan
          (<code>GET /api/backoffice/customers/erasures</code>). Tombstones
          are still written and enforced server-side by the ErasureController
          + CrmService consumer; a repeat erasure of the same email is
          correctly blocked today.
        </p>
      </div>
    </main>
  );
}
