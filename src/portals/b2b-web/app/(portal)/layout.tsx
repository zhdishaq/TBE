// Plan 05-01 Task 1 — authenticated route group layout.
//
// Gates every route inside (portal) behind an authenticated session.
// Unauthenticated visitors redirect to /login; the server-rendered
// <Header /> provides the D-42 AgentPortalBadge + role-conditional nav
// on every authenticated page.
//
// 05-PATTERNS §19 anticipates a wallet-chip `initial-balance` RSC fetch
// here once Plan 05-02 wires the balance route. Plan 05-01 intentionally
// ships a header placeholder ("--") so the nav chrome is complete before
// the wallet route exists — the AgentPortalBadge + nav are the blocker
// for every downstream plan, not the balance.
//
// Plan 05-05 Task 5 — sitewide <LowBalanceBanner/> mounted ABOVE <Header/>
// so it sits above the nav chrome on every authenticated page. The banner
// shares the `['wallet','balance']` TanStack cache with the header's
// <WalletChip/> so the two components cost a single network round-trip per
// 30-second poll cycle. Role prop is forwarded so non-admin agents see the
// mailto CTA and admin users see the /admin/wallet top-up link.

import { redirect } from 'next/navigation';
import { auth } from '@/lib/auth';
import { Header } from '@/components/layout/header';
import { LowBalanceBanner } from '@/components/wallet/low-balance-banner';

export default async function PortalLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const session = await auth();
  if (!session) {
    // Middleware already gates these prefixes, but we defence-in-depth at
    // the layout so a future middleware regression cannot leak a
    // renderable shell to an unauthenticated client.
    redirect('/login');
  }
  return (
    <>
      <LowBalanceBanner roles={session.roles ?? []} />
      <Header
        agentName={session.user?.name ?? null}
        agencyId={session.user?.agency_id}
        roles={session.roles ?? []}
      />
      <div className="flex-1">{children}</div>
    </>
  );
}
