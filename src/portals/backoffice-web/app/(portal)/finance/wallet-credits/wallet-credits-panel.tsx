'use client';

// Plan 06-01 Task 6 — D-39 wallet credits client panel.
//
// Owns:
//   1. Status tab switch via URL ?status=... (Pending / Approved / Denied / Expired).
//   2. "New credit request" dialog — AgencyId + Amount + ReasonCode radio
//      (4 D-53 values) + LinkedBookingId (optional) + Notes. POST to
//      /api/wallet-credits. Surfaces /errors/wallet-credit-invalid-amount
//      and /errors/wallet-credit-invalid-reason inline.
//   3. Approve dialog — approvalNotes textarea required. POST to
//      /api/wallet-credits/{id}/approve. Surfaces /errors/four-eyes-self-approval
//      as an inline 403 message.

import { useCallback, useState, useTransition } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';

type WalletCreditRow = {
  id: string;
  agencyId: string;
  amount: number;
  currency: string;
  reasonCode: string;
  linkedBookingId: string | null;
  notes: string;
  requestedBy: string;
  requestedAt: string;
  expiresAt: string;
  status: 'PendingApproval' | 'Approved' | 'Denied' | 'Expired';
  approvedBy: string | null;
  approvedAt: string | null;
  approvalNotes: string | null;
};

type WalletCreditListResponse = {
  rows: WalletCreditRow[];
  totalCount: number;
  page: number;
  pageSize: number;
};

const REASON_CODES = [
  'RefundedBooking',
  'GoodwillCredit',
  'DisputeResolution',
  'SupplierRefundPassthrough',
] as const;

const STATUS_TABS = [
  { key: 'PendingApproval', label: 'Pending' },
  { key: 'Approved', label: 'Approved' },
  { key: 'Denied', label: 'Denied' },
  { key: 'Expired', label: 'Expired' },
];

export function WalletCreditsPanel({
  initial,
  status,
  canCreate,
  canApprove,
}: {
  initial: WalletCreditListResponse;
  status: string;
  canCreate: boolean;
  canApprove: boolean;
}) {
  const router = useRouter();
  const searchParams = useSearchParams();
  const [pending, startTransition] = useTransition();

  // Create-dialog state.
  const [showCreate, setShowCreate] = useState(false);
  const [agencyId, setAgencyId] = useState('');
  const [amount, setAmount] = useState('');
  const [currency, setCurrency] = useState('GBP');
  const [reasonCode, setReasonCode] = useState<(typeof REASON_CODES)[number]>('RefundedBooking');
  const [linkedBookingId, setLinkedBookingId] = useState('');
  const [notes, setNotes] = useState('');
  const [createError, setCreateError] = useState<string | null>(null);

  // Approve-dialog state.
  const [activeRow, setActiveRow] = useState<WalletCreditRow | null>(null);
  const [approvalNotes, setApprovalNotes] = useState('');
  const [approveError, setApproveError] = useState<string | null>(null);

  const changeStatus = useCallback(
    (next: string) => {
      const qs = new URLSearchParams(searchParams.toString());
      qs.set('status', next);
      router.push(`?${qs.toString()}`);
    },
    [router, searchParams],
  );

  const submitCreate = useCallback(async () => {
    setCreateError(null);
    const amountNum = Number(amount);
    if (!agencyId.trim() || Number.isNaN(amountNum) || amountNum < 0.01 || amountNum > 100000) {
      setCreateError('Amount must be between £0.01 and £100,000.');
      return;
    }
    const res = await fetch('/api/wallet-credits', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({
        agencyId: agencyId.trim(),
        amount: amountNum,
        currency,
        reasonCode,
        linkedBookingId: linkedBookingId.trim() || null,
        notes: notes.trim(),
      }),
    });
    if (!res.ok) {
      const body = await res.json().catch(() => null);
      setCreateError(body?.detail ?? `Request failed (HTTP ${res.status})`);
      return;
    }
    setShowCreate(false);
    setAgencyId('');
    setAmount('');
    setLinkedBookingId('');
    setNotes('');
    startTransition(() => router.refresh());
  }, [agencyId, amount, currency, reasonCode, linkedBookingId, notes, router]);

  const submitApprove = useCallback(async () => {
    if (!activeRow || !approvalNotes.trim()) return;
    setApproveError(null);
    const res = await fetch(
      `/api/wallet-credits/${encodeURIComponent(activeRow.id)}/approve`,
      {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify({ approvalNotes: approvalNotes.trim() }),
      },
    );
    if (!res.ok) {
      if (res.status === 403) {
        setApproveError('Self-approval blocked — another ops-admin must approve.');
      } else if (res.status === 409) {
        setApproveError('Request expired or already decided.');
      } else {
        setApproveError(`Approve failed (HTTP ${res.status})`);
      }
      return;
    }
    setActiveRow(null);
    setApprovalNotes('');
    startTransition(() => router.refresh());
  }, [activeRow, approvalNotes, router]);

  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center justify-between border-b border-slate-200">
        <div role="tablist" className="flex gap-2">
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
        {canCreate && (
          <button
            onClick={() => setShowCreate(true)}
            className="mb-1 rounded bg-slate-900 px-3 py-1.5 text-sm font-medium text-white hover:bg-slate-800"
          >
            New credit request
          </button>
        )}
      </div>

      <table className="w-full text-sm">
        <thead className="text-left text-xs uppercase text-slate-500">
          <tr className="h-10 border-b border-slate-200">
            <th className="px-2">Agency</th>
            <th className="px-2">Amount</th>
            <th className="px-2">Reason</th>
            <th className="px-2">Requested by</th>
            <th className="px-2">Requested at</th>
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
              <td className="px-2 font-mono text-xs">{r.agencyId.slice(0, 8)}</td>
              <td className="px-2 font-medium tabular-nums">
                £{r.amount.toFixed(2)} {r.currency}
              </td>
              <td className="px-2">{r.reasonCode}</td>
              <td className="px-2">{r.requestedBy}</td>
              <td className="px-2 text-xs text-slate-500">
                {new Date(r.requestedAt).toLocaleString()}
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

      {showCreate && (
        <div
          role="dialog"
          aria-modal="true"
          aria-label="New wallet credit request"
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4"
        >
          <div className="w-full max-w-md rounded-lg bg-white p-6 shadow-xl">
            <h2 className="text-base font-semibold">New credit request</h2>
            <div className="mt-4 flex flex-col gap-3">
              <label className="text-sm font-medium">
                Agency Id
                <input
                  value={agencyId}
                  onChange={(e) => setAgencyId(e.target.value)}
                  className="mt-1 w-full rounded border border-slate-300 p-2 text-sm font-mono"
                  placeholder="00000000-0000-0000-0000-000000000000"
                />
              </label>
              <div className="flex gap-2">
                <label className="flex-1 text-sm font-medium">
                  Amount (£)
                  <input
                    type="number"
                    step="0.01"
                    min="0.01"
                    max="100000"
                    value={amount}
                    onChange={(e) => setAmount(e.target.value)}
                    className="mt-1 w-full rounded border border-slate-300 p-2 text-sm tabular-nums"
                  />
                </label>
                <label className="w-24 text-sm font-medium">
                  Currency
                  <input
                    value={currency}
                    onChange={(e) => setCurrency(e.target.value.toUpperCase().slice(0, 3))}
                    maxLength={3}
                    className="mt-1 w-full rounded border border-slate-300 p-2 text-sm uppercase"
                  />
                </label>
              </div>
              <fieldset className="text-sm">
                <legend className="font-medium">Reason code</legend>
                <div className="mt-1 grid grid-cols-2 gap-1">
                  {REASON_CODES.map((rc) => (
                    <label key={rc} className="flex items-center gap-2 text-xs">
                      <input
                        type="radio"
                        name="reasonCode"
                        checked={reasonCode === rc}
                        onChange={() => setReasonCode(rc)}
                      />
                      {rc}
                    </label>
                  ))}
                </div>
              </fieldset>
              <label className="text-sm font-medium">
                Linked booking (optional)
                <input
                  value={linkedBookingId}
                  onChange={(e) => setLinkedBookingId(e.target.value)}
                  className="mt-1 w-full rounded border border-slate-300 p-2 text-sm font-mono"
                  placeholder="Booking id"
                />
              </label>
              <label className="text-sm font-medium">
                Notes
                <textarea
                  value={notes}
                  onChange={(e) => setNotes(e.target.value)}
                  maxLength={1000}
                  rows={3}
                  className="mt-1 w-full rounded border border-slate-300 p-2 text-sm"
                  placeholder="e.g. Supplier refund confirmed for PNR ABC123."
                />
              </label>
              {createError && (
                <p role="alert" className="text-sm text-red-600">
                  {createError}
                </p>
              )}
            </div>
            <div className="mt-4 flex justify-end gap-2">
              <button
                onClick={() => {
                  setShowCreate(false);
                  setCreateError(null);
                }}
                className="rounded border border-slate-300 px-3 py-1.5 text-sm"
                disabled={pending}
              >
                Cancel
              </button>
              <button
                onClick={submitCreate}
                disabled={pending || !agencyId || !amount || !notes}
                className="rounded bg-slate-900 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-50"
              >
                {pending ? 'Submitting…' : 'Submit'}
              </button>
            </div>
          </div>
        </div>
      )}

      {activeRow && (
        <div
          role="dialog"
          aria-modal="true"
          aria-label="Approve wallet credit"
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4"
        >
          <div className="w-full max-w-md rounded-lg bg-white p-6 shadow-xl">
            <h2 className="text-base font-semibold">
              Approve £{activeRow.amount.toFixed(2)} to {activeRow.agencyId.slice(0, 8)}
            </h2>
            <p className="mt-1 text-sm text-slate-500">
              Requested by <strong>{activeRow.requestedBy}</strong>. Self-approval is
              forbidden (D-39 4-eyes).
            </p>
            <label className="mt-4 block text-sm font-medium">
              Approval notes
              <textarea
                value={approvalNotes}
                onChange={(e) => setApprovalNotes(e.target.value)}
                maxLength={500}
                rows={3}
                className="mt-1 w-full rounded border border-slate-300 p-2 text-sm"
                placeholder="e.g. Supplier ticket 990-XXX confirmed refunded."
              />
            </label>
            {approveError && (
              <p role="alert" className="mt-2 text-sm text-red-600">
                {approveError}
              </p>
            )}
            <div className="mt-4 flex justify-end gap-2">
              <button
                onClick={() => {
                  setActiveRow(null);
                  setApprovalNotes('');
                  setApproveError(null);
                }}
                className="rounded border border-slate-300 px-3 py-1.5 text-sm"
                disabled={pending}
              >
                Cancel
              </button>
              <button
                onClick={submitApprove}
                disabled={pending || !approvalNotes.trim()}
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
