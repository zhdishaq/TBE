// Plan 06-01 Task 3 — backoffice user menu (dropdown).
//
// UI-SPEC §2 Header Shell right cluster: staff display name + Sign out.
// Deltas vs b2b-web/components/layout/user-menu.tsx:
//   - `agencyId` prop removed — backoffice staff are not agency-scoped.
//   - Initial chip uses slate-900 accent (portal palette) not indigo-600.
//   - Fallback display label is "Staff" not "Agent".

'use client';

import { ChevronDown, LogOut } from 'lucide-react';
import { signOut } from 'next-auth/react';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { cn } from '@/lib/utils';

interface UserMenuProps {
  /** Display name from the Keycloak session (may be null). */
  agentName?: string | null;
  className?: string;
}

function initial(name?: string | null): string {
  if (!name) return '?';
  const trimmed = name.trim();
  return trimmed.length > 0 ? trimmed[0].toUpperCase() : '?';
}

export function UserMenu({ agentName, className }: UserMenuProps) {
  const label = agentName?.trim() || 'Staff';
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button
          type="button"
          aria-label={`Open account menu for ${label}`}
          className={cn(
            'inline-flex h-9 items-center gap-2 rounded-md px-2 text-sm font-medium',
            'hover:bg-accent hover:text-foreground',
            'focus:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2',
            className,
          )}
        >
          <span
            aria-hidden="true"
            className="flex h-7 w-7 items-center justify-center rounded-full bg-slate-900 text-xs font-semibold text-white dark:bg-slate-200 dark:text-slate-900"
          >
            {initial(agentName)}
          </span>
          <ChevronDown className="h-4 w-4 text-muted-foreground" aria-hidden="true" />
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-56">
        <DropdownMenuLabel>
          <div className="flex flex-col gap-0.5">
            <span className="text-sm font-medium text-foreground">{label}</span>
          </div>
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuItem
          onSelect={() => signOut({ callbackUrl: '/login' })}
          className="gap-2"
        >
          <LogOut className="h-4 w-4" aria-hidden="true" />
          <span>Sign out</span>
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
