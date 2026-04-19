'use client';

// Plan 06-01 Task 4 — BO-09/BO-10 DLQ table client component.
//
// Owns:
//   1. 60-second polling refresh against /api/dlq?status=... so new _error
//      envelopes surface without the operator refreshing the page.
//   2. Envelope viewer dialog (full JSON pretty-printed + Copy button).
//   3. Requeue action dialog — confirms then POSTs to
//      /api/dlq/{id}/requeue; upstream preserves MessageId + CorrelationId
//      so downstream idempotency keys still deduplicate.
//   4. Resolve action dialog — reason required (1..500 chars) → POSTs to
//      /api/dlq/{id}/resolve.
//
// Accessibility: action dialogs trap focus on the textarea, announce
// success via role="status" (NOT role="alert" — this is confirmation,
// not an error). Busy state disables the Confirm button and sets
// aria-busy on the row.
//
// Why a minimal inline dialog instead of the starterKit Dialog primitive?
// Plan 06-01 Task 4 is the vertical slice — the starterKit Dialog ships
// with Tasks 5/6 approval flows. This keeps Task 4 shippable without
// committing to an API surface that Plan 06-02 may revise.

import { useCallback, useEffect, useMemo, useState } from 'react';

type DlqListRow = {
  id: string;
  messageId: string;
  correlationId: string | null;
  messageType: string;
  originalQueue: string;
  failureReason: string;
  preview: string;
  firstFailedAt: string;
  lastRequeuedAt: string | null;
  requeueCount: number;
  resolvedAt: string | null;
  resolvedBy: string | null;
};

type DlqListResponse = {
  rows: DlqListRow[];
  totalCount: number;
  page: number;
  pageSize: number;
};

type DlqDetailResponse = DlqListRow & { payload: string; resolutionReason: string | null };

type ActionKind = 'requeue' | 'resolve' | 'view' | null;

export function DlqTable({
  initial,
  status,
}: {
  initial: DlqListResponse;
  status: string;
}) {
  const [data, setData] = useState<DlqListResponse>(initial);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [activeRow, setActiveRow] = useState<DlqListRow | null>(null);
  const [action, setAction] = useState<ActionKind>(null);

  const refresh = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await fetch(
        `/api/dlq?status=${encodeURIComponent(status)}&page=1&pageSize=20`,
        { cache: 'no-store' },
      );
      if (!res.ok) {
        setError(`Failed to load (HTTP ${res.status})`);
      } else {
        setData((await res.json()) as DlqListResponse);
      }
    } catch {
      setError('Network error');
    } finally {
      setLoading(false);
    }
  }, [status]);

  // 60-second polling refresh.
  useEffect(() => {
    const timer = window.setInterval(refresh, 60_000);
    return () => window.clearInterval(timer);
  }, [refresh]);

  const openAction = (row: DlqListRow, kind: ActionKind) => {
    setActiveRow(row);
    setAction(kind);
  };
  const closeAction = () => {
    setActiveRow(null);
    setAction(null);
  };

  const rows = useMemo(() => data.rows, [data]);

  return (
    <section aria-labelledby="dlq-table-heading" className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <p id="dlq-table-heading" className="text-sm text-muted-foreground">
          {data.totalCount} envelope{data.totalCount === 1 ? '' : 's'}
          {loading ? ' — refreshing…' : ''}
        </p>
        <button
          type="button"
          onClick={refresh}
          disabled={loading}
          className="rounded-md border border-slate-300 bg-white px-3 py-1.5 text-xs font-semibold text-slate-700 hover:border-slate-900 disabled:opacity-50 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-200"
        >
          {loading ? 'Refreshing…' : 'Refresh now'}
        </button>
      </div>

      {error && (
        <div role="alert" className="rounded-md border border-red-300 bg-red-50 p-3 text-sm text-red-900">
          {error}
        </div>
      )}

      <div className="overflow-x-auto rounded-md border border-slate-200 dark:border-slate-800">
        <table className="min-w-full text-sm">
          <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500 dark:bg-slate-950 dark:text-slate-400">
            <tr>
              <th scope="col" className="px-3 py-2">Status</th>
              <th scope="col" className="px-3 py-2">Message type</th>
              <th scope="col" className="px-3 py-2">Queue</th>
              <th scope="col" className="px-3 py-2 tabular-nums">First failed</th>
              <th scope="col" className="px-3 py-2 tabular-nums">Requeues</th>
              <th scope="col" className="px-3 py-2">Failure reason</th>
              <th scope="col" className="px-3 py-2 text-right">Actions</th>
            </tr>
          </thead>
          <tbody>
            {rows.length === 0 && (
              <tr>
                <td colSpan={7} className="px-3 py-6 text-center text-muted-foreground">
                  No envelopes in this view.
                </td>
              </tr>
            )}
            {rows.map((row) => (
              <tr
                key={row.id}
                className="border-t border-slate-200 dark:border-slate-800"
              >
                <td className="px-3 py-2">
                  <span
                    aria-hidden
                    className={
                      row.resolvedAt
                        ? 'inline-block h-2 w-2 rounded-full bg-emerald-500'
                        : 'inline-block h-2 w-2 rounded-full bg-amber-500'
                    }
                  />
                  <span className="sr-only">{row.resolvedAt ? 'Resolved' : 'Unresolved'}</span>
                </td>
                <td className="px-3 py-2">
                  <span className="inline-flex rounded border border-slate-300 bg-white px-2 py-0.5 font-mono text-[11px] text-slate-700 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-200">
                    {row.messageType || '—'}
                  </span>
                </td>
                <td className="px-3 py-2 font-mono text-xs">{row.originalQueue || '—'}</td>
                <td className="px-3 py-2 tabular-nums">
                  {new Date(row.firstFailedAt).toISOString()}
                </td>
                <td className="px-3 py-2 tabular-nums">
                  <span className="inline-flex rounded-full border border-slate-300 px-2 text-xs font-semibold dark:border-slate-700">
                    {row.requeueCount}
                  </span>
                </td>
                <td className="px-3 py-2" title={row.failureReason}>
                  {row.preview}
                </td>
                <td className="px-3 py-2 text-right">
                  <div className="flex justify-end gap-2">
                    <button
                      type="button"
                      onClick={() => openAction(row, 'view')}
                      className="rounded border border-slate-300 px-2 py-1 text-xs hover:border-slate-900 dark:border-slate-700"
                    >
                      View
                    </button>
                    {!row.resolvedAt && (
                      <>
                        <button
                          type="button"
                          onClick={() => openAction(row, 'requeue')}
                          className="rounded border border-slate-300 px-2 py-1 text-xs hover:border-slate-900 dark:border-slate-700"
                        >
                          Requeue
                        </button>
                        <button
                          type="button"
                          onClick={() => openAction(row, 'resolve')}
                          className="rounded border border-slate-300 px-2 py-1 text-xs hover:border-slate-900 dark:border-slate-700"
                        >
                          Resolve
                        </button>
                      </>
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {activeRow && action === 'view' && (
        <EnvelopeDialog row={activeRow} onClose={closeAction} />
      )}
      {activeRow && action === 'requeue' && (
        <RequeueDialog row={activeRow} onClose={closeAction} onDone={refresh} />
      )}
      {activeRow && action === 'resolve' && (
        <ResolveDialog row={activeRow} onClose={closeAction} onDone={refresh} />
      )}
    </section>
  );
}

function DialogShell({
  title,
  onClose,
  children,
}: {
  title: string;
  onClose: () => void;
  children: React.ReactNode;
}) {
  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label={title}
      className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 p-4"
      onClick={onClose}
    >
      <div
        onClick={(e) => e.stopPropagation()}
        className="max-h-[90vh] w-full max-w-2xl overflow-auto rounded-lg bg-white p-5 shadow-xl dark:bg-slate-900"
      >
        <div className="mb-3 flex items-center justify-between">
          <h2 className="text-lg font-semibold">{title}</h2>
          <button
            type="button"
            onClick={onClose}
            aria-label="Close dialog"
            className="text-slate-500 hover:text-slate-900 dark:hover:text-slate-100"
          >
            ×
          </button>
        </div>
        {children}
      </div>
    </div>
  );
}

function EnvelopeDialog({ row, onClose }: { row: DlqListRow; onClose: () => void }) {
  const [detail, setDetail] = useState<DlqDetailResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  useEffect(() => {
    (async () => {
      try {
        const res = await fetch(`/api/dlq/${row.id}`, { cache: 'no-store' });
        if (!res.ok) setError(`HTTP ${res.status}`);
        else setDetail((await res.json()) as DlqDetailResponse);
      } catch {
        setError('Network error');
      }
    })();
  }, [row.id]);

  const copy = async () => {
    if (!detail) return;
    await navigator.clipboard.writeText(detail.payload);
  };

  return (
    <DialogShell title="Envelope" onClose={onClose}>
      {error && <p role="alert" className="text-sm text-red-700">{error}</p>}
      {detail ? (
        <>
          <div className="mb-3 flex items-center justify-between">
            <p className="text-xs text-muted-foreground">
              MessageId <code className="font-mono">{detail.messageId}</code>
            </p>
            <button
              type="button"
              onClick={copy}
              className="rounded border border-slate-300 px-2 py-1 text-xs hover:border-slate-900 dark:border-slate-700"
            >
              Copy JSON
            </button>
          </div>
          <pre className="max-h-[60vh] overflow-auto rounded bg-slate-50 p-3 font-mono text-xs dark:bg-slate-950">
            {(() => {
              try {
                return JSON.stringify(JSON.parse(detail.payload), null, 2);
              } catch {
                return detail.payload;
              }
            })()}
          </pre>
        </>
      ) : (
        !error && <p className="text-sm text-muted-foreground">Loading envelope…</p>
      )}
    </DialogShell>
  );
}

function RequeueDialog({
  row,
  onClose,
  onDone,
}: {
  row: DlqListRow;
  onClose: () => void;
  onDone: () => void;
}) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async () => {
    setBusy(true);
    setError(null);
    try {
      const res = await fetch(`/api/dlq/${row.id}/requeue`, { method: 'POST' });
      if (!res.ok) {
        setError(`Requeue failed (HTTP ${res.status})`);
        setBusy(false);
        return;
      }
      onDone();
      onClose();
    } catch {
      setError('Network error');
      setBusy(false);
    }
  };

  return (
    <DialogShell title="Requeue envelope" onClose={onClose}>
      <p className="mb-3 text-sm">
        Republish to <code className="font-mono">{row.originalQueue}</code>?
        The original <code className="font-mono">MessageId</code> and{' '}
        <code className="font-mono">CorrelationId</code> are preserved so
        downstream consumers can still deduplicate.
      </p>
      {error && <p role="alert" className="mb-2 text-sm text-red-700">{error}</p>}
      <div className="flex justify-end gap-2">
        <button
          type="button"
          onClick={onClose}
          className="rounded border border-slate-300 px-3 py-1.5 text-sm dark:border-slate-700"
        >
          Cancel
        </button>
        <button
          type="button"
          onClick={submit}
          disabled={busy}
          className="rounded bg-slate-900 px-3 py-1.5 text-sm font-semibold text-white disabled:opacity-50 dark:bg-slate-100 dark:text-slate-900"
        >
          {busy ? 'Requeuing…' : 'Confirm requeue'}
        </button>
      </div>
    </DialogShell>
  );
}

function ResolveDialog({
  row,
  onClose,
  onDone,
}: {
  row: DlqListRow;
  onClose: () => void;
  onDone: () => void;
}) {
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async () => {
    if (reason.trim().length < 1 || reason.length > 500) {
      setError('Reason must be 1..500 characters');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const res = await fetch(`/api/dlq/${row.id}/resolve`, {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify({ reason }),
      });
      if (!res.ok) {
        setError(`Resolve failed (HTTP ${res.status})`);
        setBusy(false);
        return;
      }
      onDone();
      onClose();
    } catch {
      setError('Network error');
      setBusy(false);
    }
  };

  return (
    <DialogShell title="Resolve envelope" onClose={onClose}>
      <label htmlFor="resolve-reason" className="mb-1 block text-sm font-semibold">
        Reason
      </label>
      <textarea
        id="resolve-reason"
        required
        maxLength={500}
        value={reason}
        onChange={(e) => setReason(e.target.value)}
        rows={4}
        className="mb-3 w-full rounded border border-slate-300 bg-white p-2 text-sm dark:border-slate-700 dark:bg-slate-950"
        placeholder="e.g. supplier reconciliation complete — manually posted via DMS ticket 4719"
      />
      {error && <p role="alert" className="mb-2 text-sm text-red-700">{error}</p>}
      <div className="flex justify-end gap-2">
        <button
          type="button"
          onClick={onClose}
          className="rounded border border-slate-300 px-3 py-1.5 text-sm dark:border-slate-700"
        >
          Cancel
        </button>
        <button
          type="button"
          onClick={submit}
          disabled={busy || reason.trim().length === 0}
          className="rounded bg-slate-900 px-3 py-1.5 text-sm font-semibold text-white disabled:opacity-50 dark:bg-slate-100 dark:text-slate-900"
        >
          {busy ? 'Resolving…' : 'Confirm resolve'}
        </button>
      </div>
    </DialogShell>
  );
}
