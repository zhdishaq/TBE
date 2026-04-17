// Plan 05-01 Task 2 — sub-agent list (compact table) + dialog triggers.
//
// UI-SPEC §Sub-Agent List:
//   - Compact data grid with `h-11` row height.
//   - Columns: Name, Email, Role, Status (dot), Created, Actions.
//   - Header button `Create sub-agent` (top-right) opens
//     `<CreateSubAgentDialog>`.
//   - Row action `Deactivate` opens `<DeactivateSubAgentDialog>` (admin
//     only; disabled if already deactivated).
//   - Empty state: heading `No sub-agents yet`, body + `Create sub-agent`
//     CTA (UI-SPEC §Empty states).
//
// Data fetching:
//   - The RSC `/admin/agents/page.tsx` hands us `initialUsers` so the
//     first paint has no client round-trip.
//   - TanStack Query's `useQuery` picks up `initialData` and then owns
//     refetch + invalidate on mutation success.

'use client';

import * as React from 'react';
import { useQuery } from '@tanstack/react-query';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import { CreateSubAgentDialog } from '@/components/admin/create-sub-agent-dialog';
import { DeactivateSubAgentDialog } from '@/components/admin/deactivate-sub-agent-dialog';
import type { AgencyUser } from '@/lib/keycloak-b2b-admin';

async function fetchAgencyUsers(): Promise<AgencyUser[]> {
  const resp = await fetch('/api/agents', {
    method: 'GET',
    cache: 'no-store',
  });
  if (!resp.ok) {
    throw new Error(`list returned ${resp.status}`);
  }
  const payload = (await resp.json()) as { items?: AgencyUser[] };
  return payload.items ?? [];
}

function formatRole(roles: string[]): string {
  if (roles.includes('agent-admin')) return 'Agency admin';
  if (roles.includes('agent-readonly')) return 'Agent (read-only)';
  if (roles.includes('agent')) return 'Agent';
  return '—';
}

function formatDate(ms: number): string {
  if (!ms) return '—';
  const d = new Date(ms);
  // ISO date only — UI-SPEC §Dates uses `YYYY-MM-DD` on compact tables
  // so rows align without timezone ambiguity across agencies.
  return d.toISOString().slice(0, 10);
}

export interface SubAgentListProps {
  initialUsers: AgencyUser[];
}

export function SubAgentList({ initialUsers }: SubAgentListProps) {
  const [createOpen, setCreateOpen] = React.useState(false);
  const [deactivateTarget, setDeactivateTarget] =
    React.useState<AgencyUser | null>(null);

  const query = useQuery({
    queryKey: ['agency-users'],
    queryFn: fetchAgencyUsers,
    initialData: initialUsers,
  });

  const users = query.data ?? [];
  const hasUsers = users.length > 0;

  return (
    <section className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h2 className="sr-only">Sub-agent list</h2>
        <div
          className="text-xs text-muted-foreground"
          aria-live="polite"
        >
          {hasUsers
            ? `${users.length} sub-agent${users.length === 1 ? '' : 's'}`
            : ''}
        </div>
        <Button
          type="button"
          onClick={() => setCreateOpen(true)}
          className="ml-auto"
        >
          Create sub-agent
        </Button>
      </div>
      {hasUsers ? (
        <div className="overflow-hidden rounded-md border border-border">
          <table className="w-full border-collapse text-sm">
            <thead className="bg-muted/40">
              <tr className="text-left">
                <th className="px-3 py-2 font-medium">Name</th>
                <th className="px-3 py-2 font-medium">Email</th>
                <th className="px-3 py-2 font-medium">Role</th>
                <th className="px-3 py-2 font-medium">Status</th>
                <th className="px-3 py-2 font-medium">Created</th>
                <th className="px-3 py-2 font-medium text-right">Actions</th>
              </tr>
            </thead>
            <tbody>
              {users.map((u) => {
                const displayName = `${u.firstName ?? ''} ${u.lastName ?? ''}`.trim() || u.email;
                return (
                  <tr
                    key={u.id}
                    className="h-11 border-t border-border hover:bg-muted/20"
                  >
                    <td className="px-3">{displayName}</td>
                    <td className="px-3 text-muted-foreground">{u.email}</td>
                    <td className="px-3">{formatRole(u.roles)}</td>
                    <td className="px-3">
                      <span
                        className={cn(
                          'inline-flex items-center gap-2 text-xs font-medium',
                          u.enabled ? 'text-foreground' : 'text-muted-foreground',
                        )}
                      >
                        <span
                          aria-hidden="true"
                          className={cn(
                            'inline-block h-2 w-2 rounded-full',
                            u.enabled ? 'bg-emerald-500' : 'bg-zinc-500',
                          )}
                        />
                        {u.enabled ? 'Active' : 'Deactivated'}
                      </span>
                    </td>
                    <td className="px-3 tabular-nums text-muted-foreground">
                      {formatDate(u.createdTimestamp)}
                    </td>
                    <td className="px-3 text-right">
                      <Button
                        type="button"
                        variant="ghost"
                        size="sm"
                        disabled={!u.enabled}
                        onClick={() => setDeactivateTarget(u)}
                      >
                        Deactivate
                      </Button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      ) : (
        <div className="flex flex-col items-center justify-center gap-3 rounded-md border border-dashed border-border px-6 py-14 text-center">
          <h3 className="text-lg font-semibold text-foreground">
            No sub-agents yet
          </h3>
          <p className="max-w-md text-sm text-muted-foreground">
            Create a sub-agent so they can log in and book under your agency.
          </p>
          <Button type="button" onClick={() => setCreateOpen(true)}>
            Create sub-agent
          </Button>
        </div>
      )}
      <CreateSubAgentDialog
        open={createOpen}
        onOpenChange={setCreateOpen}
        onSuccess={(email) =>
          toast.success(
            `Sub-agent created. They will receive a verification email at ${email}.`,
          )
        }
      />
      <DeactivateSubAgentDialog
        open={deactivateTarget !== null}
        onOpenChange={(open) => {
          if (!open) setDeactivateTarget(null);
        }}
        target={
          deactivateTarget
            ? {
                id: deactivateTarget.id,
                firstName: deactivateTarget.firstName,
                lastName: deactivateTarget.lastName,
              }
            : null
        }
        onSuccess={() => {
          const displayName =
            deactivateTarget
              ? `${deactivateTarget.firstName} ${deactivateTarget.lastName}`.trim()
              : 'Sub-agent';
          toast.success(`${displayName} deactivated.`);
        }}
      />
    </section>
  );
}
