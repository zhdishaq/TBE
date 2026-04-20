// Plan 06-04 Task 3 (CRM-02) — Agency 360 shell.
//
// Header: agency name, wallet balance, credit limit, status chip.
// Tabs: Overview, Agents, Bookings, Wallet ledger, Credit-limit audit.
// Detail GET is deferred to the follow-up plan; the credit-limit
// PATCH dialog lives in the existing /finance surface.

import { notFound, redirect } from 'next/navigation';
import Link from 'next/link';
import { auth } from '@/lib/auth';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';
import { isOpsFinance, isOpsAdmin } from '@/lib/rbac';

export const dynamic = 'force-dynamic';

type Agency = {
  id: string;
  name: string;
  walletBalance: number;
  creditLimit: number;
  currency: string;
  createdAt: string;
  status: 'Active' | 'Suspended' | string;
};

export default async function AgencyDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const session = await auth();
  if (!session) redirect('/login');
  if (!isOpsFinance(session) && !isOpsAdmin(session)) redirect('/forbidden');

  const { id } = await params;

  let agency: Agency | null = null;
  let upstreamStatus: number | null = null;
  try {
    const upstream = await gatewayFetch(
      `/api/backoffice/agencies/${encodeURIComponent(id)}`,
      { method: 'GET' },
    );
    upstreamStatus = upstream.status;
    if (upstream.status === 404) notFound();
    if (upstream.ok) {
      agency = (await upstream.json()) as Agency;
    }
  } catch (err) {
    if (err instanceof UnauthenticatedError) redirect('/login');
    upstreamStatus = 502;
  }

  return (
    <main className="mx-auto w-full max-w-5xl p-6">
      <nav className="mb-4 text-sm text-slate-500">
        <Link href="/agencies" className="hover:underline">
          Agencies
        </Link>
        <span className="mx-2">/</span>
        <span className="text-slate-800 dark:text-slate-200">{id}</span>
      </nav>

      {agency === null && upstreamStatus !== 404 && (
        <div
          role="alert"
          aria-live="polite"
          className="mb-6 rounded-md border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900 dark:border-amber-900 dark:bg-amber-950/40 dark:text-amber-100"
        >
          <p className="font-medium">
            Agency detail endpoint returned {upstreamStatus ?? 'no response'}.
          </p>
          <p className="mt-1">
            The GET /api/backoffice/agencies/{'{'}id{'}'} surface is
            deferred to the follow-up plan. Credit-limit PATCH
            (CRM-02 / D-61) is fully wired via the existing{' '}
            <Link href="/finance" className="underline">
              /finance
            </Link>{' '}
            surface.
          </p>
        </div>
      )}

      {agency && (
        <>
          <header className="mb-6">
            <h1 className="text-2xl font-semibold text-slate-900 dark:text-slate-100">
              {agency.name}
            </h1>
            <p className="text-sm text-slate-500">{agency.status}</p>
          </header>

          <section className="grid grid-cols-2 gap-4 md:grid-cols-3">
            <Stat
              label="Wallet balance"
              value={new Intl.NumberFormat('en-GB', {
                style: 'currency',
                currency: agency.currency,
              }).format(agency.walletBalance)}
            />
            <Stat
              label="Credit limit"
              value={new Intl.NumberFormat('en-GB', {
                style: 'currency',
                currency: agency.currency,
              }).format(agency.creditLimit)}
            />
            <Stat
              label="Registered"
              value={new Date(agency.createdAt).toLocaleDateString()}
            />
          </section>
        </>
      )}
    </main>
  );
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border border-slate-200 bg-white p-3 dark:border-slate-800 dark:bg-slate-900">
      <p className="text-xs uppercase tracking-wide text-slate-500">{label}</p>
      <p className="mt-1 text-lg font-semibold text-slate-900 dark:text-slate-100">
        {value}
      </p>
    </div>
  );
}
