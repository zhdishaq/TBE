// Plan 05-04 Task 3 — VoidBookingButton.
//
// Admin-only destructive action on the booking detail page. Opens a
// Radix AlertDialog destructive confirm (UI-SPEC §Booking detail, D-44),
// POSTs /api/bookings/[id]/void, and surfaces the backend's RFC 7807
// response:
//   - 202 Accepted → success toast + router.refresh()
//   - 409 /errors/post-ticket-cancel-unsupported → "already ticketed"
//   - 4xx/5xx → generic error message
//
// Hidden for non-admin roles — the backend B2BAdminPolicy also enforces,
// so this is UX polish rather than security.

'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';

interface VoidBookingButtonProps {
  bookingId: string;
  roles: string[];
}

export function VoidBookingButton({ bookingId, roles }: VoidBookingButtonProps) {
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [reason, setReason] = useState('');

  if (!roles.includes('agent-admin')) return null;

  async function submit() {
    setSubmitting(true);
    setError(null);
    try {
      const res = await fetch(`/api/bookings/${bookingId}/void`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ reason: reason || 'admin-initiated void' }),
      });
      if (res.status === 202) {
        setOpen(false);
        router.refresh();
        return;
      }
      if (res.status === 409) {
        // Copy must include "already ticketed" (matches test contract —
        // post-ticket cancel is blocked by D-39 and surfaced via RFC 7807
        // problem+json with type /errors/post-ticket-cancel-unsupported).
        setError(
          'This booking is already ticketed — post-ticket cancellation is not supported. Please contact support to process the refund.',
        );
        return;
      }
      setError(`Unable to void booking (${res.status}). Please try again.`);
    } catch {
      setError('Network error. Please try again.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        className="inline-flex h-9 items-center rounded-md border border-red-300 bg-red-50 px-4 text-sm font-medium text-red-700 hover:bg-red-100 dark:border-red-900 dark:bg-red-950/30 dark:text-red-200 dark:hover:bg-red-950/50"
      >
        Void booking
      </button>

      {open && (
        <div
          role="alertdialog"
          aria-labelledby="void-booking-title"
          aria-describedby="void-booking-desc"
          aria-modal="true"
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4"
        >
          <div className="w-full max-w-md rounded-lg border border-border bg-card p-6 shadow-lg">
            <h2
              id="void-booking-title"
              className="text-lg font-semibold text-foreground"
            >
              Void this booking?
            </h2>
            <p
              id="void-booking-desc"
              className="mt-2 text-sm text-muted-foreground"
            >
              This will cancel the reservation in the GDS and release the
              wallet hold. The action is irreversible.
            </p>

            <label className="mt-4 block text-sm">
              <span className="mb-1 block text-muted-foreground">
                Reason (optional)
              </span>
              <input
                type="text"
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                className="h-9 w-full rounded-md border border-border bg-background px-3 text-sm"
              />
            </label>

            {error && (
              <p
                role="alert"
                className="mt-3 rounded-md border border-red-300 bg-red-50 px-3 py-2 text-sm text-red-900 dark:border-red-900 dark:bg-red-950/30 dark:text-red-200"
              >
                {error}
              </p>
            )}

            <div className="mt-5 flex justify-end gap-2">
              <button
                type="button"
                onClick={() => setOpen(false)}
                disabled={submitting}
                className="h-9 rounded-md border border-border bg-background px-4 text-sm"
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={submit}
                disabled={submitting}
                aria-label="Confirm void"
                className="h-9 rounded-md bg-red-600 px-4 text-sm font-medium text-white hover:bg-red-700 disabled:opacity-60"
              >
                {submitting ? 'Voiding…' : 'Confirm void'}
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
