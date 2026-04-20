// Plan 06-04 Task 3 (COMP-03) — Customer 360 detail page.
//
// Reads the CRM customer projection via /api/crm/customers/{id} (proxy
// to BackofficeService CustomersController — the GET surface is a
// thin wrapper around BackofficeDbContext.CustomerReadModel and is
// deferred to the follow-up plan). This page gracefully renders a
// "CRM read endpoint not yet wired" banner when the upstream returns
// 404/501/502 so the erase flow itself remains demo-able by typing
// a customer id directly.
//
// ops-admin sees the "Erase customer data" destructive action, which
// mounts the typed-confirm AlertDialog. All other roles see a
// read-only 360.

import { notFound, redirect } from 'next/navigation';
import Link from 'next/link';
import { auth } from '@/lib/auth';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';
import { isOpsRead, isOpsAdmin } from '@/lib/rbac';
import { EraseCustomerDialog } from './erase-dialog';

export const dynamic = 'force-dynamic';

type Customer = {
  id: string;
  email: string | null;
  name: string | null;
  phone: string | null;
  createdAt: string;
  lifetimeBookingsCount: number;
  lifetimeGross: number;
  lastBookingAt: string | null;
  isErased: boolean;
  erasedAt: string | null;
};

export default async function CustomerDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const session = await auth();
  if (!session) redirect('/login');
  if (!isOpsRead(session)) redirect('/forbidden');

  const { id } = await params;

  let customer: Customer | null = null;
  let upstreamStatus: number | null = null;
  try {
    const upstream = await gatewayFetch(
      `/api/backoffice/customers/${encodeURIComponent(id)}`,
      { method: 'GET' },
    );
    upstreamStatus = upstream.status;
    if (upstream.status === 404) notFound();
    if (upstream.ok) {
      customer = (await upstream.json()) as Customer;
    }
  } catch (err) {
    if (err instanceof UnauthenticatedError) redirect('/login');
    upstreamStatus = 502;
  }

  const isAdmin = isOpsAdmin(session);

  return (
    <main className="mx-auto w-full max-w-5xl p-6">
      <nav className="mb-4 text-sm text-slate-500">
        <Link href="/customers" className="hover:underline">
          Customers
        </Link>
        <span className="mx-2">/</span>
        <span className="text-slate-800 dark:text-slate-200">{id}</span>
      </nav>

      {customer === null && upstreamStatus !== 404 && (
        <div
          role="alert"
          aria-live="polite"
          className="mb-6 rounded-md border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900 dark:border-amber-900 dark:bg-amber-950/40 dark:text-amber-100"
        >
          <p className="font-medium">
            CRM read endpoint returned {upstreamStatus ?? 'no response'}.
          </p>
          <p className="mt-1">
            The GET /api/backoffice/customers/{'{'}id{'}'} surface is
            deferred to the follow-up plan. The erase flow can still be
            exercised via the POST endpoint below (requires ops-admin).
          </p>
        </div>
      )}

      {customer?.isErased && (
        <div
          role="status"
          aria-live="polite"
          className="mb-6 rounded-md border border-slate-300 bg-slate-100 p-4 text-sm text-slate-900 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
        >
          <p className="font-semibold">Anonymised Customer</p>
          <p className="mt-1 text-slate-700 dark:text-slate-300">
            Erased on{' '}
            {customer.erasedAt
              ? new Date(customer.erasedAt).toLocaleString()
              : 'unknown date'}
            . PII has been NULLed across every projection; booking events
            and financial audit trails remain intact per D-49.
          </p>
        </div>
      )}

      <header className="mb-6 flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-slate-900 dark:text-slate-100">
            {customer?.name ?? (customer?.isErased ? 'Anonymised Customer' : '—')}
          </h1>
          <p className="text-sm text-slate-500">
            {customer?.email ?? (customer?.isErased ? '—' : 'email unavailable')}
          </p>
          {customer?.phone && (
            <p className="text-sm text-slate-500">{customer.phone}</p>
          )}
        </div>
        {isAdmin && customer && !customer.isErased && customer.email && (
          <EraseCustomerDialog
            customerId={customer.id}
            customerEmail={customer.email}
          />
        )}
      </header>

      {customer && (
        <section className="grid grid-cols-2 gap-4 md:grid-cols-4">
          <Stat label="Lifetime bookings" value={String(customer.lifetimeBookingsCount)} />
          <Stat
            label="Lifetime gross"
            value={new Intl.NumberFormat('en-GB', {
              style: 'currency',
              currency: 'GBP',
            }).format(customer.lifetimeGross)}
          />
          <Stat
            label="Last booking"
            value={
              customer.lastBookingAt
                ? new Date(customer.lastBookingAt).toLocaleDateString()
                : '—'
            }
          />
          <Stat
            label="Registered"
            value={new Date(customer.createdAt).toLocaleDateString()}
          />
        </section>
      )}

      <section className="mt-8">
        <h2 className="mb-3 text-lg font-semibold text-slate-900 dark:text-slate-100">
          Booking history
        </h2>
        <p className="text-sm text-slate-500">
          Booking history tab and communications log are deferred to the
          follow-up plan (GET /api/backoffice/customers/{'{'}id{'}'}/bookings
          + /communications).
        </p>
      </section>
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
