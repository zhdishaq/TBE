'use client';

// Plan 06-02 Task 2 (BO-07) — supplier contract create/edit modal form.
// Submits to POST /api/suppliers or PUT /api/suppliers/[id]. Surfaces
// RFC-7807 problem+json errors (/errors/supplier-contract-*) inline.

import { useState } from 'react';

interface Props {
  mode: 'create' | 'edit';
  initial: {
    id: string;
    supplierName: string;
    productType: string;
    netRate: number;
    commissionPercent: number;
    currency: string;
    validFrom: string;
    validTo: string;
    notes: string;
  } | null;
  onClose: () => void;
  onSaved: () => void;
}

export function SupplierContractForm({ mode, initial, onClose, onSaved }: Props) {
  const [supplierName, setSupplierName] = useState(initial?.supplierName ?? '');
  const [productType, setProductType] = useState(initial?.productType ?? 'Flight');
  const [netRate, setNetRate] = useState(initial?.netRate?.toString() ?? '0');
  const [commissionPercent, setCommissionPercent] = useState(
    initial?.commissionPercent?.toString() ?? '10',
  );
  const [currency, setCurrency] = useState(initial?.currency ?? 'GBP');
  const [validFrom, setValidFrom] = useState(
    (initial?.validFrom ?? new Date().toISOString()).slice(0, 10),
  );
  const [validTo, setValidTo] = useState(
    (
      initial?.validTo ??
      new Date(Date.now() + 365 * 24 * 3600 * 1000).toISOString()
    ).slice(0, 10),
  );
  const [notes, setNotes] = useState(initial?.notes ?? '');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    setSubmitting(true);
    setError(null);

    const payload = {
      supplierName,
      productType,
      netRate: Number(netRate),
      commissionPercent: Number(commissionPercent),
      currency,
      validFrom: new Date(`${validFrom}T00:00:00Z`).toISOString(),
      validTo: new Date(`${validTo}T00:00:00Z`).toISOString(),
      notes,
    };

    const url =
      mode === 'create' ? '/api/suppliers' : `/api/suppliers/${initial?.id}`;
    const method = mode === 'create' ? 'POST' : 'PUT';

    try {
      const res = await fetch(url, {
        method,
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify(payload),
      });
      if (!res.ok) {
        // Problem+json body: map to human-readable inline error.
        let message = `${method} failed: ${res.status}`;
        try {
          const body = await res.json();
          if (body?.detail) message = body.detail as string;
          else if (body?.title) message = body.title as string;
        } catch {
          // ignore non-JSON bodies
        }
        setError(message);
        return;
      }
      onSaved();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Save failed');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40">
      <div className="w-full max-w-lg rounded-lg bg-white p-6 shadow-xl">
        <header className="mb-4 flex items-center justify-between">
          <h2 className="text-lg font-semibold text-slate-900">
            {mode === 'create' ? 'New supplier contract' : 'Edit contract'}
          </h2>
          <button
            type="button"
            className="text-sm text-slate-500 hover:text-slate-700"
            onClick={onClose}
          >
            Close
          </button>
        </header>

        <form className="flex flex-col gap-3" onSubmit={onSubmit}>
          <label className="flex flex-col gap-1 text-xs">
            <span className="font-medium text-slate-600">Supplier name</span>
            <input
              required
              type="text"
              className="rounded border border-slate-300 px-2 py-1 text-sm"
              value={supplierName}
              onChange={(e) => setSupplierName(e.target.value)}
            />
          </label>
          <div className="grid grid-cols-2 gap-3">
            <label className="flex flex-col gap-1 text-xs">
              <span className="font-medium text-slate-600">Product type</span>
              <select
                className="rounded border border-slate-300 px-2 py-1 text-sm"
                value={productType}
                onChange={(e) => setProductType(e.target.value)}
              >
                <option value="Flight">Flight</option>
                <option value="Hotel">Hotel</option>
                <option value="Car">Car</option>
                <option value="Package">Package</option>
              </select>
            </label>
            <label className="flex flex-col gap-1 text-xs">
              <span className="font-medium text-slate-600">Currency</span>
              <input
                type="text"
                minLength={3}
                maxLength={3}
                className="rounded border border-slate-300 px-2 py-1 text-sm uppercase"
                value={currency}
                onChange={(e) => setCurrency(e.target.value.toUpperCase())}
              />
            </label>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <label className="flex flex-col gap-1 text-xs">
              <span className="font-medium text-slate-600">Net rate</span>
              <input
                type="number"
                step="0.01"
                min={0}
                className="rounded border border-slate-300 px-2 py-1 text-sm"
                value={netRate}
                onChange={(e) => setNetRate(e.target.value)}
              />
            </label>
            <label className="flex flex-col gap-1 text-xs">
              <span className="font-medium text-slate-600">
                Commission percent
              </span>
              <input
                type="number"
                step="0.01"
                min={0}
                max={100}
                className="rounded border border-slate-300 px-2 py-1 text-sm"
                value={commissionPercent}
                onChange={(e) => setCommissionPercent(e.target.value)}
              />
            </label>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <label className="flex flex-col gap-1 text-xs">
              <span className="font-medium text-slate-600">Valid from</span>
              <input
                type="date"
                className="rounded border border-slate-300 px-2 py-1 text-sm"
                value={validFrom}
                onChange={(e) => setValidFrom(e.target.value)}
              />
            </label>
            <label className="flex flex-col gap-1 text-xs">
              <span className="font-medium text-slate-600">Valid to</span>
              <input
                type="date"
                className="rounded border border-slate-300 px-2 py-1 text-sm"
                value={validTo}
                onChange={(e) => setValidTo(e.target.value)}
              />
            </label>
          </div>
          <label className="flex flex-col gap-1 text-xs">
            <span className="font-medium text-slate-600">Notes</span>
            <textarea
              rows={3}
              maxLength={2000}
              className="rounded border border-slate-300 px-2 py-1 text-sm"
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
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

          <div className="mt-2 flex justify-end gap-2">
            <button
              type="button"
              className="rounded border border-slate-300 px-3 py-1.5 text-sm hover:bg-slate-100"
              onClick={onClose}
              disabled={submitting}
            >
              Cancel
            </button>
            <button
              type="submit"
              className="rounded bg-slate-900 px-3 py-1.5 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-50"
              disabled={submitting}
            >
              {submitting ? 'Saving…' : 'Save'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
