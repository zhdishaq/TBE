'use client';

// Plan 06-01 Task 6 — Cancellation queue client component.
//
// Owns:
//   1. Status tab switch (Pending / Approved / Denied / Expired) via
//      URL ?status=... (useRouter push so back-button works).
//   2. Approve dialog — approvalReason textarea required; POSTs to
//      /api/bookings/{id}/cancel/approve. Self-approval is caught on the
//      backend with a 403 `/errors/four-eyes-self-approval` — we surface
//      the problem+json detail inline.
//   3. Compact h-11 rows per UI-SPEC §Spacing.
//
// No polling here — operators refresh manually via the tab click (the
// queue is low-throughput, 72h TTL per row; polling adds no value).

import { useCallback, useState, useTransition } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';

type CancellationRow = {
  id: string;
  bookingId: string;
  reasonCode: string;
  reason: string;
  requestedBy: string;
  requestedAt: string;
  expiresAt: string;
  status: 'PendingApproval' | 'Approved' | 'Denied' | 'Expired';
  approvedBy: string | null;
  approvedAt: string | null;
  approvalReason: string | null;
};

type CancellationListResponse = {
  rows: CancellationRow[];
  totalCount: number;
  page: number;
  pageSize: number;
};

const STATUS_TABS: Array<{ key: string; label: string }> = [
  { key: 'PendingApproval', label: 'Pending' },
  { key: 'Approved', label: 'Approved' },
  { key: 'Denied', label: 'Denied' },
  { key: 'Expired', label: 'Expired' },
];

export function CancellationsTable({
  initial,
  status,
  canApprove,
}: {
  initial: CancellationListResponse;
  status: string;
  canApprove: boolean;
}) {
  const router = useRouter();
  const searchParams = useSearchParams();
  const [activeRow, setActiveRow] = useState<CancellationRow | null>(null);
  const [approvalReason, setApprovalReason] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [pending, startTransition] = useTransition();

  const changeStatus = useCallback(
    (next: string) => {
      const qs = new URLSearchParams(searchParams.toString());
      qs.set('status', next);
      router.push(`?${qs.toString()}`);
    },
    [router, searchParams],
  );

  const submitApprove = useCallback(async () => {
    if (!activeRow || !approvalReason.trim()) return;
    setError(null);
    const res = await fetch(
      `/api/bookings/${encodeURIComponent(activeRow.bookingId)}/cancel/approve`,
      {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify({
          requestId: activeRow.id,
          approvalReason: approvalReason.trim(),
        }),
      },
    );
    if (!res.ok) {
      if (res.status === 403) {
        setError('Self-approval blocked — another ops-admin must approve.');
      } else if (res.status === 409) {
        setError('Request expired or already decided.');
      } else {
        setError(`Approve failed (HTTP ${res.status})`);
      }
      return;
    }
    setActiveRow(null);
    setApprovalReason('');
    startTransition(() => router.refresh());
  }, [activeRow, approvalReason, router]);

  return (
    <div className="flex flex-col gap-3">
      <div role="tablist" className="flex gap-2 border-b border-slate-200">
        {STATUS_TABS.map((t) => (
          <button
            key={t.key}
            role="tab"
            aria-selected={status === t.key}
            onClick={() => changeStatus(t.key)}
            className={`px-3 py-2 text-sm font-medium ${
              status === t.key
                ? 'border-b-2 border-slate-900 text-slate-900'
                : 'text-slate-500 hover:text-slate-700'
            }`}
          >
            {t.label}
          </button>
        ))}
      </div>

      <table className="w-full text-sm">
        <thead className="text-left text-xs uppercase text-slate-500">
          <tr className="h-10 border-b border-slate-200">
            <th className="px-2">Booking</th>
            <th className="px-2">Reason</th>
            <th className="px-2">Requested by</th>
            <th className="px-2">Requested at</th>
            <th className="px-2">Expires</th>
            <th className="px-2">Status</th>
            {canApprove && status === 'PendingApproval' && <th className="px-2">Actions</th>}
          </tr>
        </thead>
        <tbody>
          {initial.rows.length === 0 && (
            <tr>
              <td colSpan={7} className="p-8 text-center text-slate-400">
                No rows.
              </td>
            </tr>
          )}
          {initial.rows.map((r) => (
            <tr key={r.id} className="h-11 border-b border-slate-100">
              <td className="px-2 font-mono text-xs">{r.bookingId.slice(0, 8)}</td>
              <td className="px-2">
                <span className="font-medium">{r.reasonCode}</span>
                <span className="ml-2 text-slate-500">— {r.reason}</span>
              </td>
              <td className="px-2">{r.requestedBy}</td>
              <td className="px-2 text-xs text-slate-500">
                {new Date(r.requestedAt).toLocaleString()}
              </td>
              <td className="px-2 text-xs text-slate-500">
                {new Date(r.expiresAt).toLocaleString()}
              </td>
              <td className="px-2">
                <span className="rounded bg-slate-100 px-2 py-0.5 text-xs">
                  {r.status}
                </span>
              </td>
              {canApprove && status === 'PendingApproval' && (
                <td className="px-2">
                  <button
                    onClick={() => setActiveRow(r)}
                    className="rounded bg-slate-900 px-3 py-1 text-xs font-medium text-white hover:bg-slate-800"
                  >
                    Approve
                  </button>
                </td>
              )}
            </tr>
          ))}
        </tbody>
      </table>

      {activeRow && (
        <div
          role="dialog"
          aria-modal="true"
          aria-label="Approve cancellation"
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4"
        >
          <div className="w-full max-w-md rounded-lg bg-white p-6 shadow-xl">
            <h2 className="text-base font-semibold">
              Approve cancellation — booking {activeRow.bookingId.slice(0, 8)}
            </h2>
            <p className="mt-1 text-sm text-slate-500">
              Requested by <strong>{activeRow.requestedBy}</strong>. Self-approval is forbidden
              (D-48 4-eyes).
            </p>
            <label className="mt-4 block text-sm font-medium">
              Approval reason
              <textarea
                value={approvalReason}
                onChange={(e) => setApprovalReason(e.target.value)}
                maxLength={500}
                rows={3}
                className="mt-1 w-full rounded border border-slate-300 p-2 text-sm"
                placeholder="e.g. Verified customer identity; no fraud risk."
              />
            </label>
            {error && (
              <p role="alert" className="mt-2 text-sm text-red-600">
                {error}
              </p>
            )}
            <div className="mt-4 flex justify-end gap-2">
              <button
                onClick={() => {
                  setActiveRow(null);
                  setApprovalReason('');
                  setError(null);
                }}
                className="rounded border border-slate-300 px-3 py-1.5 text-sm"
                disabled={pending}
              >
                Cancel
              </button>
              <button
                onClick={submitApprove}
                disabled={pending || !approvalReason.trim()}
                className="rounded bg-slate-900 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-50"
              >
                {pending ? 'Approving…' : 'Approve'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
