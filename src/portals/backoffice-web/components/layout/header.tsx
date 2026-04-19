// Plan 06-01 Task 3 — backoffice authenticated header shell.
//
// Deltas vs b2b-web/components/layout/header.tsx:
//   - No <WalletChip /> — backoffice has no per-staff wallet surface.
//   - Brand wordmark links to the backoffice home (/) not /dashboard.
//   - <AgentPortalBadge /> swapped for <BackofficePortalBadge /> (moved to
//     the parent layout — this header is lean).
//   - `agencyId` prop removed (backoffice staff are not agency-scoped).
//
// UI-SPEC SS2 Header Shell — single `h-14` bar on desktop. Left: brand.
// Middle: <PrimaryNav /> with role-conditional entries. Right: user menu.

import Link from 'next/link';
import { PrimaryNav } from '@/components/layout/primary-nav';
import { UserMenu } from '@/components/layout/user-menu';

interface HeaderProps {
  /** Staff display name from session.user.name. */
  agentName?: string | null;
  /** Unused in backoffice — retained for API compat with forked layout. */
  agencyId?: string;
  /** Keycloak realm roles — drives role-conditional nav visibility. */
  roles: string[];
}

export async function Header({ agentName, roles }: HeaderProps) {
  return (
    <header className="sticky top-0 z-30 flex h-14 items-center gap-6 border-b border-border bg-background px-6">
      <div className="flex items-center gap-3">
        <Link
          href="/"
          className="text-base font-bold text-foreground focus:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
        >
          TBE
        </Link>
      </div>
      <div className="hidden md:flex md:flex-1 md:items-center">
        <PrimaryNav roles={roles} />
      </div>
      <div className="flex-1 md:hidden" aria-hidden="true" />
      <div className="flex items-center gap-3">
        <UserMenu agentName={agentName} />
      </div>
    </header>
  );
}
