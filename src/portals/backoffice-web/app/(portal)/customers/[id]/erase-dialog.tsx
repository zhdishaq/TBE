'use client';

// Plan 06-04 Task 3 (COMP-03 / D-57) — Typed-confirm erase dialog.
//
// UI-SPEC §Confirmation dialogs #10: the user must retype the
// customer's email exactly before the Erase button enables. On submit
// the dialog POSTs to /api/crm/customers/{id}/erase (this portal's
// Node route handler) which proxies to BackofficeService's
// ErasureController. The server validates the typed email again + the
// reason length, blocks on open sagas / duplicate tombstones, and
// returns 202 + requestId on success.
//
// The dialog surfaces RFC-7807 problem+json detail inline so ops sees
// "Customer has an open saga (bookingId=...)" or "Email already erased
// on 2026-04-01" rather than a generic 409.

import * as React from 'react';
import { useRouter } from 'next/navigation';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog';
import { Button } from '@/components/ui/button';

type Problem = {
  type?: string;
  title?: string;
  detail?: string;
  status?: number;
};

export function EraseCustomerDialog({
  customerId,
  customerEmail,
}: {
  customerId: string;
  customerEmail: string;
}) {
  const router = useRouter();
  const [open, setOpen] = React.useState(false);
  const [reason, setReason] = React.useState('');
  const [typedEmail, setTypedEmail] = React.useState('');
  const [submitting, setSubmitting] = React.useState(false);
  const [error, setError] = React.useState<Problem | null>(null);

  const reasonValid = reason.trim().length >= 1 && reason.trim().length <= 500;
  const typedMatches =
    typedEmail.trim().toLocaleLowerCase() ===
    customerEmail.trim().toLocaleLowerCase();
  const canSubmit = reasonValid && typedMatches && !submitting;

  async function submit() {
    setSubmitting(true);
    setError(null);
    try {
      const res = await fetch(
        `/api/crm/customers/${encodeURIComponent(customerId)}/erase`,
        {
          method: 'POST',
          headers: { 'content-type': 'application/json' },
          body: JSON.stringify({ reason: reason.trim(), typedEmail: typedEmail.trim() }),
        },
      );
      if (res.status === 202) {
        setOpen(false);
        setReason('');
        setTypedEmail('');
        // Refresh RSC data so the 'Anonymised' banner flips in once the
        // CRM consumer has NULLed the row.
        router.refresh();
        return;
      }
      let problem: Problem = { status: res.status };
      try {
        const body = await res.json();
        problem = { ...body, status: res.status };
      } catch {
        // non-json error body; keep status only
      }
      setError(problem);
    } catch (e) {
      setError({ detail: (e as Error).message, status: 500 });
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <AlertDialog open={open} onOpenChange={setOpen}>
      <AlertDialogTrigger asChild>
        <Button variant="destructive" aria-label="Erase customer data">
          Erase customer data
        </Button>
      </AlertDialogTrigger>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>
            Erase all PII for {customerEmail}?
          </AlertDialogTitle>
          <AlertDialogDescription>
            Customer contact details (name, email, phone) will be NULLed
            across every projection and every open saga row. Booking
            events, payment records, and financial audit trails remain
            intact per D-49. This action cannot be undone.
          </AlertDialogDescription>
        </AlertDialogHeader>

        <div className="mt-4 space-y-4">
          <label className="block text-sm">
            <span className="mb-1 block font-medium text-slate-900 dark:text-slate-100">
              Reason (required)
            </span>
            <textarea
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              maxLength={500}
              rows={3}
              className="w-full rounded border border-slate-300 bg-white px-2 py-1 text-sm dark:border-slate-700 dark:bg-slate-900"
              placeholder="e.g. Customer requested erasure via email 2026-04-20"
              required
            />
            <span className="mt-1 block text-xs text-slate-500">
              {reason.trim().length}/500
            </span>
          </label>

          <label className="block text-sm">
            <span className="mb-1 block font-medium text-slate-900 dark:text-slate-100">
              Type the customer's email to confirm
            </span>
            <input
              type="email"
              value={typedEmail}
              onChange={(e) => setTypedEmail(e.target.value)}
              aria-describedby="typed-hint"
              className="w-full rounded border border-slate-300 bg-white px-2 py-1 text-sm dark:border-slate-700 dark:bg-slate-900"
              placeholder={customerEmail}
              required
            />
            <span id="typed-hint" className="mt-1 block text-xs text-slate-500">
              Type <code>{customerEmail}</code> exactly to enable Erase.
            </span>
          </label>

          {error && (
            <div
              role="alert"
              aria-live="assertive"
              className="rounded border border-red-300 bg-red-50 p-3 text-sm text-red-900 dark:border-red-900 dark:bg-red-950/40 dark:text-red-100"
            >
              <p className="font-semibold">
                {error.title ?? `Erasure failed (${error.status ?? 'error'})`}
              </p>
              {error.detail && <p className="mt-1">{error.detail}</p>}
            </div>
          )}
        </div>

        <AlertDialogFooter>
          <AlertDialogCancel asChild>
            <Button variant="outline" disabled={submitting}>
              Cancel
            </Button>
          </AlertDialogCancel>
          <AlertDialogAction asChild>
            <Button
              variant="destructive"
              disabled={!canSubmit}
              onClick={(e: React.MouseEvent<HTMLButtonElement>) => {
                e.preventDefault();
                if (canSubmit) void submit();
              }}
            >
              {submitting ? 'Erasing…' : 'Erase'}
            </Button>
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
