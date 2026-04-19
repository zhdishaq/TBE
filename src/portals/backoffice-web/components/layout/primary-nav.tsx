// Plan 06-01 Task 3 — backoffice primary navigation.
//
// Role-conditional nav for the tbe-backoffice portal. Base items render
// for every authenticated ops-* session. Role-specific entries appear
// only when the caller holds the matching realm role (mirrored server-
// side by middleware.ts — defence in depth).
//
// Nav taxonomy (D-46 / UI-SPEC §2):
//   - Dashboard   — every ops-* role
//   - Bookings    — ops-cs, ops-admin
//   - Finance     — ops-finance, ops-admin (wallet credits + reconciliation)
//   - Approvals   — ops-admin (4-eyes queue)
//   - Operations  — ops-admin (DLQ, outbox)
//
// Active-route underline uses slate-900 (backoffice accent) not indigo-600.

'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { cn } from '@/lib/utils';

interface PrimaryNavProps {
  /** Keycloak realm roles from the authenticated session. */
  roles: string[];
  className?: string;
}

interface NavItem {
  href: string;
  label: string;
  requires?: readonly string[]; // any-of
}

const items: ReadonlyArray<NavItem> = [
  { href: '/dashboard', label: 'Dashboard' },
  { href: '/bookings', label: 'Bookings', requires: ['ops-cs', 'ops-admin'] },
  {
    href: '/finance/wallet-credits',
    label: 'Finance',
    requires: ['ops-finance', 'ops-admin'],
  },
  { href: '/approvals', label: 'Approvals', requires: ['ops-admin'] },
  {
    href: '/operations/dlq',
    label: 'Operations',
    requires: ['ops-admin'],
  },
];

export function PrimaryNav({ roles, className }: PrimaryNavProps) {
  const pathname = usePathname() ?? '';
  const visible = items.filter(
    (item) => !item.requires || item.requires.some((r) => roles.includes(r)),
  );
  return (
    <nav aria-label="Primary" className={cn('flex items-center gap-6', className)}>
      {visible.map((item) => {
        const isActive =
          pathname === item.href || pathname.startsWith(`${item.href}/`);
        return (
          <Link
            key={item.href}
            href={item.href}
            aria-current={isActive ? 'page' : undefined}
            className={cn(
              'inline-flex h-14 items-center text-sm font-medium transition-colors',
              'border-b-2 border-transparent hover:text-foreground',
              isActive
                ? 'border-slate-900 text-foreground dark:border-slate-200'
                : 'text-muted-foreground',
              'focus:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2',
            )}
          >
            {item.label}
          </Link>
        );
      })}
    </nav>
  );
}
