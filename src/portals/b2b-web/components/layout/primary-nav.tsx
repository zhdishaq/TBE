// Plan 05-01 Task 1 — primary navigation component.
//
// Role-conditional nav for the B2B Agent Portal. Base items render for
// every authenticated agent session (D-32 agent + D-35 agent-readonly).
// The `Admin` entry appears ONLY when `roles.includes('agent-admin')` —
// mirrored server-side by middleware + the RSC page redirect in
// /admin/agents/page.tsx (defense in depth).
//
// Design tokens (UI-SPEC §2 Header Shell):
//   - Active route underline: `indigo-600` — UI-SPEC §Color reserved-for
//     list item 3.
//   - Focus ring: `--ring` (indigo-500 via globals.css delta from Plan 00).
//
// Source: 05-01-PLAN Task 1 action step 6, 05-UI-SPEC §2.

'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { cn } from '@/lib/utils';

interface PrimaryNavProps {
  /**
   * Keycloak realm roles from the authenticated session. The `Admin`
   * nav item renders only when this array includes `agent-admin`.
   */
  roles: string[];
  className?: string;
}

const baseItems: ReadonlyArray<{ href: string; label: string }> = [
  { href: '/dashboard', label: 'Dashboard' },
  { href: '/search/flights', label: 'Search' },
  { href: '/bookings', label: 'Bookings' },
];

const adminItems: ReadonlyArray<{ href: string; label: string }> = [
  { href: '/admin/agents', label: 'Admin' },
];

export function PrimaryNav({ roles, className }: PrimaryNavProps) {
  // usePathname returns `null` on the very first render in tests without
  // a router context. Treat null as "no active route".
  const pathname = usePathname() ?? '';
  const items = roles.includes('agent-admin')
    ? [...baseItems, ...adminItems]
    : baseItems;
  return (
    <nav aria-label="Primary" className={cn('flex items-center gap-6', className)}>
      {items.map((item) => {
        // Active if the current path starts with the nav href (covers
        // /dashboard, /search/flights vs /search/hotels, /admin/*).
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
                ? 'border-indigo-600 text-foreground'
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
