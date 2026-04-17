// Plan 05-01 Task 1 — authenticated user menu (dropdown).
//
// UI-SPEC §2 Header Shell — right cluster: "user menu with agency name
// + agent name + Sign out". UI-SPEC §Global CTAs mandates the literal
// "Sign out" (never the starterKit default, carried over from Phase 4).
//
// Accessibility: the trigger is a <button> with an aria-label describing
// the user so screen readers announce context before the menu opens.

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
  /** Display name from the Keycloak session (may be null if the realm
   *  only has email; the initial fallback is derived here). */
  agentName?: string | null;
  /** D-33 single-valued agency_id — rendered as a readonly label so the
   *  admin can confirm which agency they are signed into. */
  agencyId?: string;
  className?: string;
}

function initial(name?: string | null): string {
  if (!name) return '?';
  const trimmed = name.trim();
  return trimmed.length > 0 ? trimmed[0].toUpperCase() : '?';
}

export function UserMenu({ agentName, agencyId, className }: UserMenuProps) {
  const label = agentName?.trim() || 'Agent';
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
            className="flex h-7 w-7 items-center justify-center rounded-full bg-indigo-600 text-xs font-semibold text-white"
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
            {agencyId ? (
              <span className="text-xs text-muted-foreground">Agency: {agencyId}</span>
            ) : null}
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
