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

import { redirect } from 'next/navigation';
import { auth } from '@/lib/auth';
import { Header } from '@/components/layout/header';

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
      <Header
        agentName={session.user?.name ?? null}
        agencyId={session.user?.agency_id}
        roles={session.roles ?? []}
      />
      <div className="flex-1">{children}</div>
    </>
  );
}
