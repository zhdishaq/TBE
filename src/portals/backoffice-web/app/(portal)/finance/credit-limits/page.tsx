// Plan 06-04 Task 2 / CRM-02 / D-61 — agency credit-limit editor.
//
// ops-finance + ops-admin: open the inline popover (Agency 360 tab link).
// Save → PATCH /api/backoffice/payments/agencies/{id}/credit-limit.
// A 400/401/403/404 response surfaces inline via problem+json.type.
//
// Page shell is deliberately thin — the real UX is the popover form on
// Agency 360 (Plan 06-04 Task 3). This page is the dev-landing page
// executives can bookmark; it renders an agencyId picker + the same
// popover content inline. The gateway route `backoffice-payments` routes
// `/api/backoffice/payments/*` to the payment-cluster with path strip.

import { redirect } from 'next/navigation';
import { auth } from '@/lib/auth';
import { hasAnyRole } from '@/lib/rbac';
import { CreditLimitPanel } from './credit-limit-panel';

export const dynamic = 'force-dynamic';

export default async function CreditLimitsPage({
  searchParams,
}: {
  searchParams: Promise<{ agencyId?: string }>;
}) {
  const session = await auth();
  if (!session) redirect('/login');
  if (!hasAnyRole(session, ['ops-finance', 'ops-admin'])) redirect('/forbidden');

  const params = await searchParams;

  return (
    <section className="flex flex-col gap-4 p-6">
      <header className="flex flex-col gap-1">
        <h1 className="text-xl font-semibold text-slate-900">
          Agency Credit Limits
        </h1>
        <p className="text-sm text-slate-500">
          Edit an agency&apos;s overdraft allowance (D-61). Raising the
          limit here unblocks bookings that would otherwise fail at
          reserve time with <code>/errors/wallet-credit-over-limit</code>.
          Every save writes a <code>payment.CreditLimitAuditLog</code>
          row + publishes <code>AgencyCreditLimitChanged</code>.
        </p>
      </header>
      <CreditLimitPanel initialAgencyId={params.agencyId ?? ''} />
    </section>
  );
}
