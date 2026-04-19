// Plan 06-01 Task 6 — BO-03 staff-initiated booking cancellation queue.
//
// ops-cs + ops-admin: create cancellation requests via /bookings/[id].
// ops-admin (only): approve or deny PendingApproval requests here.
//
// Tabs: Pending | Approved | Denied | Expired. Compact h-11 rows per
// UI-SPEC. Approve/Deny dialogs require a reason (1..500 chars).
//
// Defence-in-depth role check — the BackofficeService controller is the
// authoritative gate. Middleware blocks non-ops users; this RSC adds a
// fail-fast layer in case the caller reached the path by some other
// route (direct navigation, stale session, etc.).

import { redirect } from 'next/navigation';
import { auth } from '@/lib/auth';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';
import { isOpsCs, isOpsAdmin } from '@/lib/rbac';
import { CancellationsTable } from './cancellations-table';

export const dynamic = 'force-dynamic';

type CancellationRow = {
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
  approvalReason: string | null;
};

type CancellationListResponse = {
  rows: CancellationRow[];
  totalCount: number;
  page: number;
  pageSize: number;
};

export default async function CancellationsPage({
  searchParams,
}: {
  searchParams: Promise<{ status?: string }>;
}) {
  const session = await auth();
  if (!session) redirect('/login');
  // ops-cs + ops-admin can view; approve is ops-admin only (enforced in
  // dialog + backend).
  if (!isOpsCs(session) && !isOpsAdmin(session)) redirect('/forbidden');

  const params = await searchParams;
  const status = params.status ?? 'PendingApproval';

  let initial: CancellationListResponse = {
    rows: [],
    totalCount: 0,
    page: 1,
    pageSize: 20,
  };
  try {
    const upstream = await gatewayFetch(
      `/api/backoffice/bookings/cancellations?status=${encodeURIComponent(status)}&page=1&pageSize=20`,
      { method: 'GET' },
    );
    if (upstream.ok) {
      initial = (await upstream.json()) as CancellationListResponse;
    }
  } catch (err) {
    if (err instanceof UnauthenticatedError) redirect('/login');
    // Otherwise render with empty initial and let the client retry.
  }

  return (
    <section className="flex flex-col gap-4 p-6">
      <header className="flex flex-col gap-1">
        <h1 className="text-xl font-semibold text-slate-900">
          Cancellation Requests
        </h1>
        <p className="text-sm text-slate-500">
          Staff-initiated booking cancellations awaiting 4-eyes approval
          (BO-03 / D-48). Self-approval is forbidden and enforced at the
          backend.
        </p>
      </header>
      <CancellationsTable
        initial={initial}
        status={status}
        canApprove={isOpsAdmin(session)}
      />
    </section>
  );
}
