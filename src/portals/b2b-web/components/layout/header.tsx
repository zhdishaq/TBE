// Plan 05-01 Task 1 — authenticated header shell.
//
// UI-SPEC §2 Header Shell — single `h-14` bar on desktop. Left cluster:
// brand wordmark `TBE` + <AgentPortalBadge /> (D-42). Middle: <PrimaryNav>
// with role-conditional Admin. Right: wallet-chip placeholder (Plan 02
// fills) + <UserMenu>.
//
// D-42: indigo-600 accent AND "AGENT PORTAL" outline badge appear on
// every authenticated route. The accent never spreads beyond the badge
// and the active nav underline — everything else is neutral.

import Link from 'next/link';
import { AgentPortalBadge } from '@/components/layout/agent-portal-badge';
import { PrimaryNav } from '@/components/layout/primary-nav';
import { UserMenu } from '@/components/layout/user-menu';

interface HeaderProps {
  /** Agent display name from session.user.name. */
  agentName?: string | null;
  /** D-33 single-valued agency_id from session.user.agency_id. */
  agencyId?: string;
  /** Keycloak realm roles — drives Admin nav visibility. */
  roles: string[];
}

export function Header({ agentName, agencyId, roles }: HeaderProps) {
  return (
    <header className="sticky top-0 z-30 flex h-14 items-center gap-6 border-b border-border bg-background px-6">
      {/* Brand + badge — D-42 differentiation (always visible). */}
      <div className="flex items-center gap-3">
        <Link
          href="/dashboard"
          className="text-base font-bold text-foreground focus:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
        >
          TBE
        </Link>
        <AgentPortalBadge />
      </div>

      {/* Primary nav — role-conditional Admin item (md+). */}
      <div className="hidden md:flex md:flex-1 md:items-center">
        <PrimaryNav roles={roles} />
      </div>
      {/* Mobile: nav collapses below breakpoint; the spacer keeps the
          right cluster flush to the edge. */}
      <div className="flex-1 md:hidden" aria-hidden="true" />

      {/* Right cluster: wallet-chip placeholder (Plan 02 wires real data)
          + user menu. Placeholder uses `--` per Plan 05-01 action step 4. */}
      <div className="flex items-center gap-3">
        <span
          aria-label="Wallet balance: not yet loaded"
          className="hidden h-9 items-center gap-2 rounded-full border border-indigo-600 px-3 text-sm font-semibold tabular-nums text-foreground md:inline-flex"
        >
          <span aria-hidden="true" className="text-muted-foreground">
            Wallet
          </span>
          <span>--</span>
        </span>
        <UserMenu agentName={agentName} agencyId={agencyId} />
      </div>
    </header>
  );
}
