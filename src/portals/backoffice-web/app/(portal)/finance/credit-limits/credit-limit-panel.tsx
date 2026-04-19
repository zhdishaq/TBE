'use client';

// Plan 06-04 Task 2 / CRM-02 / D-61 — credit-limit editor client panel.
//
// Owns:
//   1. AgencyId input (GUID textbox; in Plan 06-04 Task 3 the Agency 360
//      page pre-populates this via URL ?agencyId=).
//   2. CreditLimit numeric input (£ prefix; tabular-nums for alignment).
//   3. Reason textarea (10-500 chars; required per controller validation).
//   4. Save → PATCH /api/backoffice/payments/agencies/{agencyId}/credit-limit.
//   5. Surface /errors/credit-limit-out-of-range and
//      /errors/credit-limit-reason-required inline with the offending field.

import { useState, useTransition } from 'react';

type ProblemJson = {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
};

export function CreditLimitPanel({ initialAgencyId }: { initialAgencyId: string }) {
  const [pending, startTransition] = useTransition();
  const [agencyId, setAgencyId] = useState(initialAgencyId);
  const [creditLimit, setCreditLimit] = useState('');
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const onSave = () => {
    setError(null);
    setSuccess(null);

    // Client-side validation mirrors the controller so operators get
    // fast feedback; the server is still authoritative on 400 / 401 / 404.
    const trimmedAgency = agencyId.trim();
    if (!/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/.test(trimmedAgency)) {
      setError('AgencyId must be a GUID.');
      return;
    }
    const limit = Number(creditLimit);
    if (!Number.isFinite(limit) || limit < 0) {
      setError('CreditLimit must be a non-negative number.');
      return;
    }
    const trimmedReason = reason.trim();
    if (trimmedReason.length < 10 || trimmedReason.length > 500) {
      setError('Reason must be between 10 and 500 characters.');
      return;
    }

    startTransition(async () => {
      const res = await fetch(
        `/api/credit-limits/${encodeURIComponent(trimmedAgency)}`,
        {
          method: 'PATCH',
          headers: { 'content-type': 'application/json' },
          body: JSON.stringify({ creditLimit: limit, reason: trimmedReason }),
        },
      );
      if (res.status === 204) {
        setSuccess(
          `Credit limit updated for ${trimmedAgency} → £${limit.toFixed(2)}.`,
        );
        return;
      }
      let body: ProblemJson | null = null;
      try {
        body = (await res.json()) as ProblemJson;
      } catch {
        body = null;
      }
      setError(
        body?.title ??
          `Request failed with status ${res.status}${body?.type ? ` (${body.type})` : ''}`,
      );
    });
  };

  return (
    <form
      className="flex max-w-xl flex-col gap-4 rounded border border-slate-200 bg-white p-4"
      onSubmit={(e) => {
        e.preventDefault();
        onSave();
      }}
    >
      <label className="flex flex-col gap-1">
        <span className="text-sm font-medium text-slate-700">Agency ID (GUID)</span>
        <input
          className="rounded border border-slate-300 px-2 py-1 font-mono text-sm"
          value={agencyId}
          onChange={(e) => setAgencyId(e.target.value)}
          required
          placeholder="00000000-0000-0000-0000-000000000000"
        />
      </label>
      <label className="flex flex-col gap-1">
        <span className="text-sm font-medium text-slate-700">Credit Limit (£)</span>
        <input
          type="number"
          step="0.01"
          min="0"
          max="100000"
          className="rounded border border-slate-300 px-2 py-1 text-right tabular-nums"
          value={creditLimit}
          onChange={(e) => setCreditLimit(e.target.value)}
          required
        />
      </label>
      <label className="flex flex-col gap-1">
        <span className="text-sm font-medium text-slate-700">
          Reason (10-500 chars; appears in audit log)
        </span>
        <textarea
          className="rounded border border-slate-300 px-2 py-1 text-sm"
          rows={3}
          maxLength={500}
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          required
        />
      </label>

      {error ? (
        <p className="rounded bg-red-50 px-3 py-2 text-sm text-red-700">{error}</p>
      ) : null}
      {success ? (
        <p className="rounded bg-emerald-50 px-3 py-2 text-sm text-emerald-700">
          {success}
        </p>
      ) : null}

      <div className="flex justify-end">
        <button
          type="submit"
          disabled={pending}
          className="rounded bg-slate-900 px-3 py-1 text-sm text-white disabled:opacity-50"
        >
          {pending ? 'Saving…' : 'Save'}
        </button>
      </div>
    </form>
  );
}
