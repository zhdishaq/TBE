// Plan 05-01 Task 1 -- authenticated header shell.
// Plan 05-02 Task 3 delta:
//   - Wallet-chip placeholder removed. The header is now an async RSC that
//     fetches the initial wallet balance server-side via
//     `gatewayFetch('/api/b2b/wallet/balance')` and hands it to the client
//     <WalletChip /> which polls every 30s.
//   - If the balance fetch fails (network / 403), the chip falls back to a
//     zero-balance initial render -- it will self-correct on the next poll.
//
// UI-SPEC SS2 Header Shell -- single `h-14` bar on desktop. Left cluster:
// brand wordmark `TBE` + <AgentPortalBadge /> (D-42). Middle: <PrimaryNav>
// with role-conditional Admin. Right: <WalletChip /> + <UserMenu>.

import Link from 'next/link';
import { AgentPortalBadge } from '@/components/layout/agent-portal-badge';
import { PrimaryNav } from '@/components/layout/primary-nav';
import { UserMenu } from '@/components/layout/user-menu';
import { WalletChip } from '@/components/wallet/wallet-chip';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';

interface HeaderProps {
  /** Agent display name from session.user.name. */
  agentName?: string | null;
  /** D-33 single-valued agency_id from session.user.agency_id. */
  agencyId?: string;
  /** Keycloak realm roles -- drives Admin nav visibility + wallet CTA branch. */
  roles: string[];
}

interface WalletBalancePayload {
  amount: number;
  currency: string;
}

async function loadInitialBalance(): Promise<WalletBalancePayload> {
  try {
    const r = await gatewayFetch('/api/b2b/wallet/balance');
    if (!r.ok) return { amount: 0, currency: 'GBP' };
    return (await r.json()) as WalletBalancePayload;
  } catch (err) {
    if (err instanceof UnauthenticatedError) return { amount: 0, currency: 'GBP' };
    return { amount: 0, currency: 'GBP' };
  }
}

export async function Header({ agentName, agencyId, roles }: HeaderProps) {
  const balance = await loadInitialBalance();
  return (
    <header className="sticky top-0 z-30 flex h-14 items-center gap-6 border-b border-border bg-background px-6">
      {/* Brand + badge -- D-42 differentiation (always visible). */}
      <div className="flex items-center gap-3">
        <Link
          href="/dashboard"
          className="text-base font-bold text-foreground focus:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
        >
          TBE
        </Link>
        <AgentPortalBadge />
      </div>

      {/* Primary nav -- role-conditional Admin item (md+). */}
      <div className="hidden md:flex md:flex-1 md:items-center">
        <PrimaryNav roles={roles} />
      </div>
      <div className="flex-1 md:hidden" aria-hidden="true" />

      {/* Right cluster: wallet chip (server-hydrated + 30s polled) + user menu. */}
      <div className="flex items-center gap-3">
        <WalletChip
          initialBalance={balance.amount}
          currency={balance.currency}
          roles={roles}
        />
        <UserMenu agentName={agentName} agencyId={agencyId} />
      </div>
    </header>
  );
}
