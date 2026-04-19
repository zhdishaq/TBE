// Plan 06-01 Task 6 — D-39 manual wallet credit queue.
//
// ops-finance + ops-admin: view list + open new credit requests.
// ops-admin only: approve PendingApproval requests.
//
// All the real validation (Amount in [0.01, 100000], ReasonCode in the
// D-53 enum, self-approval guard) is enforced by WalletCreditRequestsController
// with RFC-7807 problem+json. This page surfaces validation failures
// inline.

import { redirect } from 'next/navigation';
import { auth } from '@/lib/auth';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';
import { isOpsFinance, isOpsAdmin, hasAnyRole } from '@/lib/rbac';
import { WalletCreditsPanel } from './wallet-credits-panel';

export const dynamic = 'force-dynamic';

type WalletCreditRow = {
  id: string;
  agencyId: string;
  amount: number;
  currency: string;
  reasonCode: string;
  linkedBookingId: string | null;
  notes: string;
  requestedBy: string;
  requestedAt: string;
  expiresAt: string;
  status: 'PendingApproval' | 'Approved' | 'Denied' | 'Expired';
  approvedBy: string | null;
  approvedAt: string | null;
  approvalNotes: string | null;
};

type WalletCreditListResponse = {
  rows: WalletCreditRow[];
  totalCount: number;
  page: number;
  pageSize: number;
};

export default async function WalletCreditsPage({
  searchParams,
}: {
  searchParams: Promise<{ status?: string }>;
}) {
  const session = await auth();
  if (!session) redirect('/login');
  if (!hasAnyRole(session, ['ops-finance', 'ops-admin'])) redirect('/forbidden');

  const params = await searchParams;
  const status = params.status ?? 'PendingApproval';

  let initial: WalletCreditListResponse = {
    rows: [],
    totalCount: 0,
    page: 1,
    pageSize: 20,
  };
  try {
    const upstream = await gatewayFetch(
      `/api/backoffice/wallet-credits?status=${encodeURIComponent(status)}&page=1&pageSize=20`,
      { method: 'GET' },
    );
    if (upstream.ok) {
      initial = (await upstream.json()) as WalletCreditListResponse;
    }
  } catch (err) {
    if (err instanceof UnauthenticatedError) redirect('/login');
  }

  return (
    <section className="flex flex-col gap-4 p-6">
      <header className="flex flex-col gap-1">
        <h1 className="text-xl font-semibold text-slate-900">
          Manual Wallet Credits
        </h1>
        <p className="text-sm text-slate-500">
          Post-ticket refunds and goodwill credits (D-39). Amounts are
          bounded £0.01 – £100,000. Self-approval is forbidden; ops-admin
          must approve requests opened by another user.
        </p>
      </header>
      <WalletCreditsPanel
        initial={initial}
        status={status}
        canCreate={isOpsFinance(session) || isOpsAdmin(session)}
        canApprove={isOpsAdmin(session)}
      />
    </section>
  );
}
