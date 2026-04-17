// Plan 05-02 Task 3 RED stub -- Header (WalletChip not yet wired).
// Still renders the brand + badge + nav + user-menu cluster so the
// Plan 05-01 regression tests pass; WalletChip is stubbed so Task 3 RED
// asserts the missing 30s poll + role-aware link contract.

import Link from 'next/link';
import { AgentPortalBadge } from '@/components/layout/agent-portal-badge';
import { PrimaryNav } from '@/components/layout/primary-nav';
import { UserMenu } from '@/components/layout/user-menu';

interface HeaderProps {
  agentName?: string | null;
  agencyId?: string;
  roles: string[];
}

// RED stub: synchronous (Task 3 RED tests `await Header(...)` and this
// throws because the props object isn't a promise -- the test harness sees
// `TypeError: Header(...).then is not a function`). Structural signal that
// the async RSC + WalletChip wiring has not yet landed.
export function Header({ agentName, agencyId, roles }: HeaderProps) {
  return (
    <header className="sticky top-0 z-30 flex h-14 items-center gap-6 border-b border-border bg-background px-6">
      <div className="flex items-center gap-3">
        <Link href="/dashboard" className="text-base font-bold text-foreground">
          TBE
        </Link>
        <AgentPortalBadge />
      </div>
      <div className="hidden md:flex md:flex-1 md:items-center">
        <PrimaryNav roles={roles} />
      </div>
      <div className="flex-1 md:hidden" aria-hidden="true" />
      <div className="flex items-center gap-3">
        <UserMenu agentName={agentName} agencyId={agencyId} />
      </div>
    </header>
  );
}
