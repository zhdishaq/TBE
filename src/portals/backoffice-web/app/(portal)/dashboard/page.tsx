// Plan 06-01 Task 3 — authenticated backoffice dashboard placeholder.
//
// The real BO-08 dashboard (KPI tiles: pending approvals, open DLQ rows,
// unresolved bookings) ships in Plan 06-04. This wave intentionally
// renders a minimal stub so (a) authenticated visitors have a landing
// surface and (b) middleware.ts can soft-redirect role-mismatched users
// to /dashboard without 404-ing.

import { auth } from '@/lib/auth';

export default async function DashboardPage() {
  const session = await auth();
  const name = session?.user?.name ?? 'ops';
  return (
    <main className="mx-auto flex max-w-4xl flex-col gap-6 px-6 py-10">
      <header className="flex flex-col gap-2">
        <h1 className="text-2xl font-semibold">Backoffice dashboard</h1>
        <p className="text-sm text-muted-foreground">
          Welcome back, {name}. Approvals, DLQ, and reconciliation tiles
          land in Plan 06-04.
        </p>
      </header>
      <section aria-label="Shortcut tiles" className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <a
          href="/approvals"
          className="rounded-md border border-slate-200 p-4 transition hover:border-slate-900 dark:border-slate-800 dark:hover:border-slate-200"
        >
          <h2 className="text-sm font-semibold">Approvals</h2>
          <p className="text-xs text-muted-foreground">4-eyes queue (ops-admin)</p>
        </a>
        <a
          href="/finance/wallet-credits"
          className="rounded-md border border-slate-200 p-4 transition hover:border-slate-900 dark:border-slate-800 dark:hover:border-slate-200"
        >
          <h2 className="text-sm font-semibold">Wallet credits</h2>
          <p className="text-xs text-muted-foreground">D-39 manual credit (ops-finance / ops-admin)</p>
        </a>
        <a
          href="/operations/dlq"
          className="rounded-md border border-slate-200 p-4 transition hover:border-slate-900 dark:border-slate-800 dark:hover:border-slate-200"
        >
          <h2 className="text-sm font-semibold">Dead letters</h2>
          <p className="text-xs text-muted-foreground">MassTransit _error triage (ops-admin)</p>
        </a>
      </section>
    </main>
  );
}
