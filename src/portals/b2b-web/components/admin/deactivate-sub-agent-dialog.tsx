// Plan 05-01 Task 2 — destructive confirmation for sub-agent deactivate.
//
// Uses Radix `AlertDialog` (not `Dialog`) per UI-SPEC §Destructive
// confirmations — AlertDialog's ARIA semantics + forced focus trap on
// Cancel match the "destructive, needs deliberate confirmation" story.
//
// UI-SPEC §Copywriting (verbatim):
//   title: `Deactivate {firstName} {lastName}?`
//   body : `They will no longer be able to log in. Bookings they have
//           already made remain under your agency. You can reactivate
//           them later from this list.`
//   destructive CTA: `Deactivate sub-agent` (red)
//   cancel     CTA: `Keep sub-agent active`
//
// D-44 lock: no typed confirmation — two-button footer, AlertDialog
// focus lands on Cancel by default (accident-resistant without being
// obstructive).

'use client';

import * as React from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';

export interface DeactivateSubAgentDialogProps {
  /** Controlled open state — the parent opens on row-action click. */
  open: boolean;
  onOpenChange: (open: boolean) => void;
  target: { id: string; firstName: string; lastName: string } | null;
  /** Optional analytics / toast hook called after a 204 from PATCH. */
  onSuccess?: () => void;
}

async function patchDeactivate(userId: string): Promise<void> {
  const resp = await fetch(`/api/agents/${userId}/deactivate`, {
    method: 'PATCH',
  });
  if (resp.status === 204) return;
  if (resp.status === 403) throw new Error('forbidden');
  throw new Error('deactivate_failed');
}

export function DeactivateSubAgentDialog({
  open,
  onOpenChange,
  target,
  onSuccess,
}: DeactivateSubAgentDialogProps) {
  const queryClient = useQueryClient();
  const mutation = useMutation({
    mutationFn: (userId: string) => patchDeactivate(userId),
    onSuccess: () => {
      // Invalidate the agency-users query so the list row flips its
      // status dot to `Deactivated` without a full round-trip.
      queryClient.invalidateQueries({ queryKey: ['agency-users'] });
      onSuccess?.();
      onOpenChange(false);
    },
  });

  const displayName = target
    ? `${target.firstName} ${target.lastName}`.trim()
    : '';

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>
            {target ? `Deactivate ${displayName}?` : 'Deactivate sub-agent?'}
          </AlertDialogTitle>
          <AlertDialogDescription>
            They will no longer be able to log in. Bookings they have already
            made remain under your agency. You can reactivate them later from
            this list.
          </AlertDialogDescription>
        </AlertDialogHeader>
        {mutation.isError ? (
          <p
            role="alert"
            className="text-sm text-destructive"
          >
            We couldn&rsquo;t deactivate that sub-agent. Please try again.
          </p>
        ) : null}
        <AlertDialogFooter>
          <AlertDialogCancel disabled={mutation.isPending}>
            Keep sub-agent active
          </AlertDialogCancel>
          <AlertDialogAction
            variant="destructive"
            disabled={!target || mutation.isPending}
            onClick={(event: React.MouseEvent<HTMLButtonElement>) => {
              // Radix fires the onClick before closing the dialog; we
              // need to prevent the default close until the mutation
              // resolves (otherwise the dialog disappears before the
              // error toast could surface).
              event.preventDefault();
              if (target) mutation.mutate(target.id);
            }}
          >
            {mutation.isPending ? 'Deactivating…' : 'Deactivate sub-agent'}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
