'use client';

// Plan 06-02 Task 3 (BO-06) — reconciliation queue table + side-by-side
// payload diff viewer + resolve modal.

import { useCallback, useEffect, useMemo, useState } from 'react';

type Severity = 'Low' | 'Medium' | 'High';
type Status = 'Pending' | 'Resolved';
type DiscrepancyType =
  | 'OrphanStripeEvent'
  | 'OrphanWalletRow'
  | 'AmountDrift'
  | 'UnprocessedEvent';

interface ReconciliationRow {
  id: string;
  discrepancyType: DiscrepancyType;
  severity: Severity;
  bookingId?: string;
  stripeEventId?: string;
  details: string; // JSON string: { stripe: {...}, wallet: {...} }
  detectedAtUtc: string;
  status: Status;
  resolvedBy?: string;
  resolvedAtUtc?: string;
  resolutionNotes?: string;
}

interface ListResponse {
  rows: ReconciliationRow[];
  totalCount: number;
  page: number;
  pageSize: number;
}

const SEVERITY_STYLES: Record<Severity, string> = {
  High: 'bg-rose-100 text-rose-900',
  Medium: 'bg-amber-100 text-amber-900',
  Low: 'bg-sky-100 text-sky-900',
};

const TYPE_LABEL: Record<DiscrepancyType, string> = {
  OrphanStripeEvent: 'Orphan Stripe event',
  OrphanWalletRow: 'Orphan wallet row',
  AmountDrift: 'Amount drift',
  UnprocessedEvent: 'Unprocessed event',
};

export function ReconciliationQueue({ canResolve }: { canResolve: boolean }) {
  const [rows, setRows] = useState<ReconciliationRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [statusFilter, setStatusFilter] = useState<Status>('Pending');
  const [typeFilter, setTypeFilter] = useState<string>('');
  const [severityFilter, setSeverityFilter] = useState<string>('');
  const [selected, setSelected] = useState<ReconciliationRow | null>(null);

  const fetchRows = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params = new URLSearchParams();
      params.set('status', statusFilter);
      if (typeFilter) params.set('discrepancyType', typeFilter);
      if (severityFilter) params.set('severity', severityFilter);
      const res = await fetch(`/api/reconciliation?${params.toString()}`, {
        cache: 'no-store',
      });
      if (!res.ok) {
        setError(`Load failed: ${res.status}`);
        setRows([]);
        return;
      }
      const body: ListResponse = await res.json();
      setRows(body.rows ?? []);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Load failed');
    } finally {
      setLoading(false);
    }
  }, [statusFilter, typeFilter, severityFilter]);

  useEffect(() => {
    fetchRows();
  }, [fetchRows]);

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-end gap-3 rounded border border-slate-200 bg-white p-3">
        <label className="flex flex-col gap-1 text-xs">
          <span className="font-medium text-slate-600">Status</span>
          <select
            className="rounded border border-slate-300 px-2 py-1 text-sm"
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value as Status)}
          >
            <option value="Pending">Pending</option>
            <option value="Resolved">Resolved</option>
          </select>
        </label>
        <label className="flex flex-col gap-1 text-xs">
          <span className="font-medium text-slate-600">Type</span>
          <select
            className="rounded border border-slate-300 px-2 py-1 text-sm"
            value={typeFilter}
            onChange={(e) => setTypeFilter(e.target.value)}
          >
            <option value="">All</option>
            <option value="OrphanStripeEvent">Orphan Stripe event</option>
            <option value="OrphanWalletRow">Orphan wallet row</option>
            <option value="AmountDrift">Amount drift</option>
            <option value="UnprocessedEvent">Unprocessed event</option>
          </select>
        </label>
        <label className="flex flex-col gap-1 text-xs">
          <span className="font-medium text-slate-600">Severity</span>
          <select
            className="rounded border border-slate-300 px-2 py-1 text-sm"
            value={severityFilter}
            onChange={(e) => setSeverityFilter(e.target.value)}
          >
            <option value="">All</option>
            <option value="High">High</option>
            <option value="Medium">Medium</option>
            <option value="Low">Low</option>
          </select>
        </label>
      </div>

      {loading ? <p className="text-sm text-slate-500">Loading…</p> : null}
      {error ? (
        <p
          role="alert"
          className="rounded border border-rose-200 bg-rose-50 p-3 text-sm text-rose-900"
        >
          {error}
        </p>
      ) : null}

      {!loading && !error ? (
        <div className="overflow-x-auto rounded border border-slate-200 bg-white">
          <table className="min-w-full text-sm">
            <thead className="bg-slate-50 text-left text-xs uppercase tracking-wider text-slate-500">
              <tr>
                <th className="px-3 py-2">Detected</th>
                <th className="px-3 py-2">Type</th>
                <th className="px-3 py-2">Severity</th>
                <th className="px-3 py-2">Booking</th>
                <th className="px-3 py-2">Stripe event</th>
                <th className="px-3 py-2"></th>
              </tr>
            </thead>
            <tbody>
              {rows.map((r) => (
                <tr key={r.id} className="border-t border-slate-100">
                  <td className="px-3 py-2 text-slate-600">
                    {new Date(r.detectedAtUtc).toISOString().slice(0, 19)}Z
                  </td>
                  <td className="px-3 py-2 text-slate-700">
                    {TYPE_LABEL[r.discrepancyType]}
                  </td>
                  <td className="px-3 py-2">
                    <span
                      className={`inline-flex rounded px-2 py-0.5 text-xs font-semibold ${SEVERITY_STYLES[r.severity]}`}
                    >
                      {r.severity}
                    </span>
                  </td>
                  <td className="px-3 py-2 font-mono text-xs text-slate-500">
                    {r.bookingId?.slice(0, 8) ?? '—'}
                  </td>
                  <td className="px-3 py-2 font-mono text-xs text-slate-500">
                    {r.stripeEventId ?? '—'}
                  </td>
                  <td className="px-3 py-2">
                    <button
                      type="button"
                      className="text-xs font-medium text-sky-700 hover:underline"
                      onClick={() => setSelected(r)}
                    >
                      Inspect
                    </button>
                  </td>
                </tr>
              ))}
              {rows.length === 0 ? (
                <tr>
                  <td
                    colSpan={6}
                    className="px-3 py-6 text-center text-sm text-slate-500"
                  >
                    No {statusFilter.toLowerCase()} reconciliation items.
                  </td>
                </tr>
              ) : null}
            </tbody>
          </table>
        </div>
      ) : null}

      {selected ? (
        <ReconciliationInspector
          row={selected}
          canResolve={canResolve}
          onClose={() => setSelected(null)}
          onResolved={() => {
            setSelected(null);
            fetchRows();
          }}
        />
      ) : null}
    </div>
  );
}

function ReconciliationInspector({
  row,
  canResolve,
  onClose,
  onResolved,
}: {
  row: ReconciliationRow;
  canResolve: boolean;
  onClose: () => void;
  onResolved: () => void;
}) {
  const [notes, setNotes] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const parsed = useMemo(() => {
    try {
      return JSON.parse(row.details) as {
        stripe?: unknown;
        wallet?: unknown;
      };
    } catch {
      return { stripe: null, wallet: null };
    }
  }, [row.details]);

  async function handleResolve() {
    if (!notes.trim()) {
      setError('Resolution notes required.');
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      const res = await fetch(`/api/reconciliation/${row.id}/resolve`, {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify({ notes }),
      });
      if (!res.ok) {
        let message = `Resolve failed: ${res.status}`;
        try {
          const body = await res.json();
          if (body?.detail) message = body.detail as string;
        } catch {}
        setError(message);
        return;
      }
      onResolved();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Resolve failed');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40">
      <div className="w-full max-w-4xl rounded-lg bg-white p-6 shadow-xl">
        <header className="mb-4 flex items-center justify-between">
          <h2 className="text-lg font-semibold text-slate-900">
            {TYPE_LABEL[row.discrepancyType]} —{' '}
            <span className="text-sm font-normal text-slate-500">
              detected {new Date(row.detectedAtUtc).toISOString().slice(0, 19)}
              Z
            </span>
          </h2>
          <button
            type="button"
            className="text-sm text-slate-500 hover:text-slate-700"
            onClick={onClose}
          >
            Close
          </button>
        </header>

        <div className="grid grid-cols-2 gap-3">
          <div className="rounded border border-slate-200 bg-slate-50 p-3">
            <h3 className="mb-2 text-xs font-semibold uppercase tracking-wider text-slate-500">
              Stripe
            </h3>
            <pre className="max-h-80 overflow-auto whitespace-pre-wrap break-all text-xs text-slate-800">
              {JSON.stringify(parsed.stripe ?? null, null, 2)}
            </pre>
          </div>
          <div className="rounded border border-slate-200 bg-slate-50 p-3">
            <h3 className="mb-2 text-xs font-semibold uppercase tracking-wider text-slate-500">
              Wallet
            </h3>
            <pre className="max-h-80 overflow-auto whitespace-pre-wrap break-all text-xs text-slate-800">
              {JSON.stringify(parsed.wallet ?? null, null, 2)}
            </pre>
          </div>
        </div>

        {row.status === 'Pending' && canResolve ? (
          <div className="mt-4 flex flex-col gap-2">
            <label className="flex flex-col gap-1 text-xs">
              <span className="font-medium text-slate-600">
                Resolution notes
              </span>
              <textarea
                rows={3}
                maxLength={2000}
                className="rounded border border-slate-300 px-2 py-1 text-sm"
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
                placeholder="e.g. Corrected via manual credit request WC-123"
              />
            </label>
            {error ? (
              <p
                role="alert"
                className="rounded border border-rose-200 bg-rose-50 p-2 text-xs text-rose-900"
              >
                {error}
              </p>
            ) : null}
            <div className="flex justify-end gap-2">
              <button
                type="button"
                className="rounded border border-slate-300 px-3 py-1.5 text-sm hover:bg-slate-100"
                onClick={onClose}
                disabled={submitting}
              >
                Cancel
              </button>
              <button
                type="button"
                className="rounded bg-slate-900 px-3 py-1.5 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-50"
                onClick={handleResolve}
                disabled={submitting}
              >
                {submitting ? 'Resolving…' : 'Resolve'}
              </button>
            </div>
          </div>
        ) : null}

        {row.status === 'Resolved' ? (
          <div className="mt-4 rounded border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-900">
            <p>
              <span className="font-semibold">Resolved</span> by{' '}
              <span className="font-mono">{row.resolvedBy}</span> at{' '}
              {row.resolvedAtUtc
                ? new Date(row.resolvedAtUtc).toISOString().slice(0, 19) + 'Z'
                : 'unknown'}
              .
            </p>
            {row.resolutionNotes ? (
              <p className="mt-2 whitespace-pre-wrap">{row.resolutionNotes}</p>
            ) : null}
          </div>
        ) : null}
      </div>
    </div>
  );
}
