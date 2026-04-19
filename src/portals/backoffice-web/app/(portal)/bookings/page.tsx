// Plan 06-01 Task 7 (BO-01) — unified booking list page.
//
// Cross-channel (B2C / B2B / Manual) booking view for ops staff. All
// four ops-* roles can read; no agency filter (cross-tenant is the
// intended behaviour per T-6-05).
//
// Filters:
//   - Channel   (?channel=B2C|B2B|Manual)
//   - Free-text (?q=...)  matches PNR / CustomerName / CustomerEmail / BookingRef
//   - Date range (?from=ISO&to=ISO)
// Paging via ?page=&pageSize= — page size clamped to [1..100] on the backend.
//
// Defence-in-depth — the gateway + BackofficeReadPolicy are authoritative;
// this RSC redirects unauthenticated / missing-role callers fail-fast so
// a future middleware regression cannot render a list shell to a non-ops
// session.

import { redirect } from 'next/navigation';
import Link from 'next/link';
import { auth } from '@/lib/auth';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';
import { isOpsRead } from '@/lib/rbac';
import { formatMoney } from '@/lib/format-money';

export const dynamic = 'force-dynamic';

type BookingListRow = {
  bookingId: string;
  channel: 'B2C' | 'B2B' | 'Manual' | string;
  currentState: number;
  pnr: string | null;
  ticketNumber: string | null;
  customerName: string | null;
  customerEmail: string | null;
  agencyId: string | null;
  grossAmount: number;
  currency: string;
  bookingReference: string;
  createdAt: string;
};

type BookingListResponse = {
  rows: BookingListRow[];
  totalCount: number;
  page: number;
  pageSize: number;
};

const CHANNEL_TABS: Array<{ key: string; label: string }> = [
  { key: '', label: 'All' },
  { key: 'B2C', label: 'B2C' },
  { key: 'B2B', label: 'B2B' },
  { key: 'Manual', label: 'Manual' },
];

export default async function BookingsPage({
  searchParams,
}: {
  searchParams: Promise<{
    channel?: string;
    q?: string;
    from?: string;
    to?: string;
    page?: string;
    pageSize?: string;
  }>;
}) {
  const session = await auth();
  if (!session) redirect('/login');
  if (!isOpsRead(session)) redirect('/forbidden');

  const params = await searchParams;
  const channel = (params.channel ?? '').trim();
  const q = (params.q ?? '').trim();
  const from = (params.from ?? '').trim();
  const to = (params.to ?? '').trim();
  const page = Math.max(1, Number.parseInt(params.page ?? '1', 10) || 1);
  const pageSize = Math.min(
    100,
    Math.max(1, Number.parseInt(params.pageSize ?? '20', 10) || 20),
  );

  const qs = new URLSearchParams();
  if (channel) qs.set('channel', channel);
  if (q) qs.set('q', q);
  if (from) qs.set('from', from);
  if (to) qs.set('to', to);
  qs.set('page', String(page));
  qs.set('pageSize', String(pageSize));

  let data: BookingListResponse = {
    rows: [],
    totalCount: 0,
    page,
    pageSize,
  };
  try {
    const upstream = await gatewayFetch(
      `/api/backoffice/bookings?${qs.toString()}`,
      { method: 'GET' },
    );
    if (upstream.ok) {
      data = (await upstream.json()) as BookingListResponse;
    }
  } catch (err) {
    if (err instanceof UnauthenticatedError) redirect('/login');
  }

  const totalPages = Math.max(1, Math.ceil(data.totalCount / pageSize));

  return (
    <section className="flex flex-col gap-4 p-6">
      <header className="flex flex-col gap-1">
        <h1 className="text-xl font-semibold text-slate-900">Bookings</h1>
        <p className="text-sm text-slate-500">
          Unified cross-channel booking list (BO-01). All four ops-* roles
          have read access. Click a row to see the event timeline and any
          active cancellation requests.
        </p>
      </header>

      {/* Channel tabs — preserve q/from/to/pageSize when switching */}
      <nav
        aria-label="Channel filter"
        className="flex gap-1 border-b border-slate-200"
      >
        {CHANNEL_TABS.map((t) => {
          const p = new URLSearchParams();
          if (t.key) p.set('channel', t.key);
          if (q) p.set('q', q);
          if (from) p.set('from', from);
          if (to) p.set('to', to);
          p.set('pageSize', String(pageSize));
          const active = (channel || '') === t.key;
          return (
            <Link
              key={t.key || 'all'}
              href={`/bookings?${p.toString()}`}
              aria-current={active ? 'page' : undefined}
              className={
                'inline-flex h-9 items-center px-4 text-sm font-medium ' +
                (active
                  ? 'border-b-2 border-slate-900 text-slate-900'
                  : 'text-slate-500 hover:text-slate-700')
              }
            >
              {t.label}
            </Link>
          );
        })}
      </nav>

      {/* Filter form (GET — RSC re-renders on submit) */}
      <form
        action="/bookings"
        method="GET"
        className="grid grid-cols-1 gap-2 sm:grid-cols-4"
      >
        <input type="hidden" name="channel" value={channel} />
        <input type="hidden" name="pageSize" value={pageSize} />
        <label className="flex flex-col gap-1 text-xs text-slate-600 sm:col-span-2">
          <span>Search (PNR, name, email, booking ref)</span>
          <input
            type="search"
            name="q"
            defaultValue={q}
            className="h-9 rounded-md border border-slate-300 px-3 text-sm"
            placeholder="PNR123 / alice@example.com / TBE-…"
          />
        </label>
        <label className="flex flex-col gap-1 text-xs text-slate-600">
          <span>From (UTC)</span>
          <input
            type="datetime-local"
            name="from"
            defaultValue={from}
            className="h-9 rounded-md border border-slate-300 px-2 text-sm"
          />
        </label>
        <label className="flex flex-col gap-1 text-xs text-slate-600">
          <span>To (UTC)</span>
          <input
            type="datetime-local"
            name="to"
            defaultValue={to}
            className="h-9 rounded-md border border-slate-300 px-2 text-sm"
          />
        </label>
        <div className="flex items-end gap-2 sm:col-span-4">
          <button
            type="submit"
            className="h-9 rounded-md bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800"
          >
            Apply filters
          </button>
          <Link
            href="/bookings"
            className="h-9 rounded-md border border-slate-300 px-4 text-sm font-medium leading-9 text-slate-700 hover:bg-slate-50"
          >
            Reset
          </Link>
          <span className="ml-auto self-center text-xs text-slate-500">
            {data.totalCount.toLocaleString()} total
          </span>
        </div>
      </form>

      {/* Rows */}
      <div className="overflow-hidden rounded-md border border-slate-200">
        <table className="w-full text-sm tabular-nums">
          <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
            <tr className="h-9">
              <th className="px-3 font-medium">Booking ref</th>
              <th className="px-3 font-medium">Channel</th>
              <th className="px-3 font-medium">PNR</th>
              <th className="px-3 font-medium">Customer</th>
              <th className="px-3 font-medium text-right">Amount</th>
              <th className="px-3 font-medium">Created (UTC)</th>
            </tr>
          </thead>
          <tbody>
            {data.rows.length === 0 && (
              <tr>
                <td
                  colSpan={6}
                  className="h-11 px-3 text-center text-xs text-slate-400"
                >
                  No bookings match the current filters.
                </td>
              </tr>
            )}
            {data.rows.map((r) => (
              <tr
                key={r.bookingId}
                className="h-11 border-t border-slate-100 hover:bg-slate-50"
              >
                <td className="px-3">
                  <Link
                    href={`/bookings/${r.bookingId}`}
                    className="font-medium text-slate-900 underline-offset-2 hover:underline"
                  >
                    {r.bookingReference}
                  </Link>
                </td>
                <td className="px-3">
                  <span
                    className={
                      'inline-flex h-6 items-center rounded-full px-2 text-[11px] font-semibold uppercase ' +
                      (r.channel === 'B2C'
                        ? 'bg-blue-100 text-blue-700'
                        : r.channel === 'B2B'
                          ? 'bg-emerald-100 text-emerald-700'
                          : r.channel === 'Manual'
                            ? 'bg-amber-100 text-amber-700'
                            : 'bg-slate-100 text-slate-700')
                    }
                  >
                    {r.channel}
                  </span>
                </td>
                <td className="px-3 text-slate-700">{r.pnr ?? '—'}</td>
                <td className="px-3 text-slate-700">
                  {r.customerName ?? '—'}
                  {r.customerEmail && (
                    <span className="ml-1 text-xs text-slate-400">
                      &lt;{r.customerEmail}&gt;
                    </span>
                  )}
                </td>
                <td className="px-3 text-right text-slate-900">
                  {formatMoney(r.grossAmount, r.currency)}
                </td>
                <td className="px-3 text-slate-500">
                  {new Date(r.createdAt).toISOString().slice(0, 19).replace('T', ' ')}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Paging */}
      <nav
        aria-label="Pagination"
        className="flex items-center justify-between text-xs text-slate-500"
      >
        <span>
          Page {data.page} of {totalPages} ({data.pageSize} per page)
        </span>
        <div className="flex gap-2">
          {page > 1 && (
            <Link
              href={`/bookings?${new URLSearchParams({
                ...(channel && { channel }),
                ...(q && { q }),
                ...(from && { from }),
                ...(to && { to }),
                page: String(page - 1),
                pageSize: String(pageSize),
              }).toString()}`}
              className="h-8 rounded-md border border-slate-300 px-3 leading-8 hover:bg-slate-50"
            >
              Prev
            </Link>
          )}
          {page < totalPages && (
            <Link
              href={`/bookings?${new URLSearchParams({
                ...(channel && { channel }),
                ...(q && { q }),
                ...(from && { from }),
                ...(to && { to }),
                page: String(page + 1),
                pageSize: String(pageSize),
              }).toString()}`}
              className="h-8 rounded-md border border-slate-300 px-3 leading-8 hover:bg-slate-50"
            >
              Next
            </Link>
          )}
        </div>
      </nav>
    </section>
  );
}
