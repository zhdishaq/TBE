// Plan 06-01 Task 3 — backoffice authenticated route group layout.
//
// Gates every route inside (portal) behind an authenticated session.
// Unauthenticated visitors redirect to /login; the shell provides the
// BackofficePortalBadge + role chip + 4-eyes queue pill + nav chrome on
// every authenticated page.
//
// Phase 6 Plan 06-01 deltas vs b2b-web/app/(portal)/layout.tsx:
//   - REMOVED <LowBalanceBanner /> — that is a B2B agency-wallet concern,
//     not a backoffice concern.
//   - ADDED <BackofficePortalBadge /> in the header slot.
//   - ADDED <FourEyesApprovalBadge /> rendered when ops-admin has pending
//     approvals. The pending count is a static 0 in Task 3; Tasks 5-6 wire
//     the real RSC fetch when the StaffBookingActions + WalletCreditRequests
//     controllers exist.
//   - ADDED role chip text derived from `session.user.ops_role` for the
//     signed-in staff identity visual.

import { redirect } from 'next/navigation';
import { auth } from '@/lib/auth';
import { Header } from '@/components/layout/header';
import { BackofficePortalBadge } from '@/components/layout/BackofficePortalBadge';
import { FourEyesApprovalBadge } from '@/components/layout/FourEyesApprovalBadge';

export default async function PortalLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const session = await auth();
  if (!session) {
    // Middleware already gates every path, but we defence-in-depth at the
    // layout so a future middleware regression cannot leak a renderable
    // shell to an unauthenticated client.
    redirect('/login');
  }

  // Placeholder — Tasks 5/6 replace with a real RSC fetch against
  // /api/backoffice/approvals?status=PendingApproval&visibleTo=me.
  const pendingApprovalCount = 0;

  const opsRole = session.user?.ops_role;
  const roleChipLabel = opsRole ? opsRole.replace('ops-', '').toUpperCase() : null;

  return (
    <>
      <div className="flex h-12 items-center gap-3 border-b border-slate-200 bg-slate-50 px-6 dark:border-slate-800 dark:bg-slate-950">
        <BackofficePortalBadge />
        {roleChipLabel && (
          <span
            aria-label={`Role: ${opsRole}`}
            className="inline-flex h-7 items-center rounded-full border border-slate-300 bg-white px-3 text-[11px] font-semibold uppercase tracking-wide text-slate-700 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-200"
          >
            {roleChipLabel}
          </span>
        )}
        <FourEyesApprovalBadge pendingCount={pendingApprovalCount} />
        {/* Global search placeholder — Plan 06-02 lands the cmdk modal. */}
        <div
          role="search"
          aria-label="Global search (coming soon)"
          className="ml-auto hidden h-8 w-72 items-center rounded-md border border-slate-200 px-3 text-xs text-slate-400 dark:border-slate-700 dark:text-slate-500 sm:inline-flex"
        >
          Press / to search
        </div>
      </div>
      <Header
        agentName={session.user?.name ?? null}
        agencyId={undefined}
        roles={session.roles ?? []}
      />
      <div className="flex-1">{children}</div>
    </>
  );
}
