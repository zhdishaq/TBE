// Plan 06-01 Task 7 (BO-01) — booking detail page.
//
// Renders the booking summary + audit timeline (dbo.BookingEvents) +
// any cancellation requests for the same booking. All four ops-* roles
// can view; ops-cs / ops-admin see the "Open cancellation" action
// (handled via Task 6 surface — link out from here).

import { notFound, redirect } from 'next/navigation';
import Link from 'next/link';
import { auth } from '@/lib/auth';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';
import { isOpsRead, isOpsCs, isOpsAdmin } from '@/lib/rbac';
import { formatMoney } from '@/lib/format-money';

export const dynamic = 'force-dynamic';

type BookingEventDto = {
  eventId: string;
  bookingId: string;
  eventType: string;
  occurredAt: string;
  actor: string;
  correlationId: string;
};

type CancellationRequestDto = {
  id: string;
  bookingId: string;
  reasonCode: string;
  reason: string;
  requestedBy: string;
  requestedAt: string;
  expiresAt: string;
  status: 'PendingApproval' | 'Approved' | 'Denied' | 'Expired';
  approvedBy: string | null;
  approvedAt: string | null;
};

type BookingDetailResponse = {
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
  bookingEvents: BookingEventDto[];
  cancellationRequests: CancellationRequestDto[];
};

export default async function BookingDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const session = await auth();
  if (!session) redirect('/login');
  if (!isOpsRead(session)) redirect('/forbidden');

  const { id } = await params;

  let detail: BookingDetailResponse | null = null;
  try {
    const upstream = await gatewayFetch(
      `/api/backoffice/bookings/${encodeURIComponent(id)}`,
      { method: 'GET' },
    );
    if (upstream.status === 404) notFound();
    if (upstream.ok) {
      detail = (await upstream.json()) as BookingDetailResponse;
    }
  } catch (err) {
    if (err instanceof UnauthenticatedError) redirect('/login');
  }

  if (!detail) notFound();

  const canCancel = isOpsCs(session) || isOpsAdmin(session);
  const hasOpenCancellation = detail.cancellationRequests.some(
    (c) => c.status === 'PendingApproval',
  );

  return (
    <section className="flex flex-col gap-6 p-6">
      <nav aria-label="breadcrumb" className="text-xs text-slate-500">
        <Link href="/bookings" className="hover:text-slate-700">
          ← All bookings
        </Link>
      </nav>

      {/* Summary */}
      <header className="flex flex-col gap-2">
        <div className="flex items-center gap-3">
          <h1 className="text-xl font-semibold text-slate-900">
            {detail.bookingReference}
          </h1>
          <span
            className={
              'inline-flex h-6 items-center rounded-full px-2 text-[11px] font-semibold uppercase ' +
              (detail.channel === 'B2C'
                ? 'bg-blue-100 text-blue-700'
                : detail.channel === 'B2B'
                  ? 'bg-emerald-100 text-emerald-700'
                  : detail.channel === 'Manual'
                    ? 'bg-amber-100 text-amber-700'
                    : 'bg-slate-100 text-slate-700')
            }
          >
            {detail.channel}
          </span>
          <span className="text-xs text-slate-500">
            State: {detail.currentState}
          </span>
        </div>
        <dl className="grid grid-cols-2 gap-x-6 gap-y-1 text-sm text-slate-700 sm:grid-cols-4">
          <div>
            <dt className="text-xs uppercase text-slate-400">PNR</dt>
            <dd className="tabular-nums">{detail.pnr ?? '—'}</dd>
          </div>
          <div>
            <dt className="text-xs uppercase text-slate-400">Ticket</dt>
            <dd className="tabular-nums">{detail.ticketNumber ?? '—'}</dd>
          </div>
          <div>
            <dt className="text-xs uppercase text-slate-400">Customer</dt>
            <dd>{detail.customerName ?? '—'}</dd>
          </div>
          <div>
            <dt className="text-xs uppercase text-slate-400">Email</dt>
            <dd>{detail.customerEmail ?? '—'}</dd>
          </div>
          <div>
            <dt className="text-xs uppercase text-slate-400">Agency</dt>
            <dd className="break-all">{detail.agencyId ?? '—'}</dd>
          </div>
          <div>
            <dt className="text-xs uppercase text-slate-400">Amount</dt>
            <dd className="tabular-nums">
              {formatMoney(detail.grossAmount, detail.currency)}
            </dd>
          </div>
          <div>
            <dt className="text-xs uppercase text-slate-400">Created (UTC)</dt>
            <dd>
              {new Date(detail.createdAt)
                .toISOString()
                .slice(0, 19)
                .replace('T', ' ')}
            </dd>
          </div>
        </dl>
        {canCancel && !hasOpenCancellation && (
          <div>
            <Link
              href={`/bookings/cancellations?status=PendingApproval`}
              className="inline-flex h-8 items-center rounded-md border border-slate-300 px-3 text-xs font-medium text-slate-700 hover:bg-slate-50"
            >
              Manage cancellations →
            </Link>
          </div>
        )}
      </header>

      {/* Cancellation requests */}
      <article className="flex flex-col gap-2">
        <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-600">
          Cancellation requests
        </h2>
        {detail.cancellationRequests.length === 0 ? (
          <p className="text-xs text-slate-400">
            No cancellation requests for this booking.
          </p>
        ) : (
          <div className="overflow-hidden rounded-md border border-slate-200">
            <table className="w-full text-sm">
              <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
                <tr className="h-9">
                  <th className="px-3 font-medium">Status</th>
                  <th className="px-3 font-medium">Reason</th>
                  <th className="px-3 font-medium">Requested by</th>
                  <th className="px-3 font-medium">Requested at</th>
                  <th className="px-3 font-medium">Expires</th>
                </tr>
              </thead>
              <tbody>
                {detail.cancellationRequests.map((c) => (
                  <tr
                    key={c.id}
                    className="h-11 border-t border-slate-100"
                  >
                    <td className="px-3">
                      <span
                        className={
                          'inline-flex h-6 items-center rounded-full px-2 text-[11px] font-semibold uppercase ' +
                          (c.status === 'PendingApproval'
                            ? 'bg-amber-100 text-amber-700'
                            : c.status === 'Approved'
                              ? 'bg-emerald-100 text-emerald-700'
                              : c.status === 'Denied'
                                ? 'bg-rose-100 text-rose-700'
                                : 'bg-slate-100 text-slate-600')
                        }
                      >
                        {c.status}
                      </span>
                    </td>
                    <td className="px-3">
                      <div className="text-xs text-slate-500">{c.reasonCode}</div>
                      <div className="text-slate-700">{c.reason}</div>
                    </td>
                    <td className="px-3 text-slate-700">{c.requestedBy}</td>
                    <td className="px-3 text-slate-500">
                      {new Date(c.requestedAt)
                        .toISOString()
                        .slice(0, 19)
                        .replace('T', ' ')}
                    </td>
                    <td className="px-3 text-slate-500">
                      {new Date(c.expiresAt)
                        .toISOString()
                        .slice(0, 19)
                        .replace('T', ' ')}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </article>

      {/* Event timeline */}
      <article className="flex flex-col gap-2">
        <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-600">
          Event timeline
        </h2>
        {detail.bookingEvents.length === 0 ? (
          <p className="text-xs text-slate-400">
            No events recorded yet. The BookingEvents write path is wired by
            Plan 06-01 Task 4 / Task 6; older bookings may have no entries.
          </p>
        ) : (
          <ol className="flex flex-col gap-2">
            {detail.bookingEvents.map((e) => (
              <li
                key={e.eventId}
                className="flex gap-3 rounded-md border border-slate-200 bg-white p-3 text-sm"
              >
                <div className="flex min-w-[180px] flex-col text-xs text-slate-500">
                  <span className="tabular-nums">
                    {new Date(e.occurredAt)
                      .toISOString()
                      .slice(0, 19)
                      .replace('T', ' ')}
                  </span>
                  <span className="truncate">{e.actor}</span>
                </div>
                <div className="flex-1">
                  <div className="font-medium text-slate-900">{e.eventType}</div>
                  <div className="text-[11px] text-slate-400">
                    correlation {e.correlationId}
                  </div>
                </div>
              </li>
            ))}
          </ol>
        )}
      </article>
    </section>
  );
}
