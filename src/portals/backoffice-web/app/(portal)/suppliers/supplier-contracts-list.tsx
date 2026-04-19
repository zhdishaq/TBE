'use client';

// Plan 06-02 Task 2 (BO-07) — supplier contracts list + create/edit/
// soft-delete client component.
//
// The backend SupplierContractsController computes Status
// (Upcoming / Active / Expired) server-side so that the client needs no
// per-tick recomputation or timezone awareness.

import { useCallback, useEffect, useState } from 'react';
import { SupplierContractForm } from './supplier-contract-form';

type Status = 'Upcoming' | 'Active' | 'Expired';
type ProductType = 'Flight' | 'Hotel' | 'Car' | 'Package';

interface SupplierContractRow {
  id: string;
  supplierName: string;
  productType: ProductType;
  netRate: number;
  commissionPercent: number;
  currency: string;
  validFrom: string;
  validTo: string;
  notes: string;
  createdBy: string;
  createdAt: string;
  updatedBy?: string;
  updatedAt?: string;
  status: Status;
}

interface ListResponse {
  rows: SupplierContractRow[];
  totalCount: number;
  page: number;
  pageSize: number;
}

const STATUS_STYLES: Record<Status, string> = {
  Active: 'bg-emerald-100 text-emerald-900',
  Upcoming: 'bg-sky-100 text-sky-900',
  Expired: 'bg-slate-200 text-slate-700',
};

export function SupplierContractsList({ canMutate }: { canMutate: boolean }) {
  const [rows, setRows] = useState<SupplierContractRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [productType, setProductType] = useState<string>('');
  const [status, setStatus] = useState<string>('');
  const [q, setQ] = useState<string>('');
  const [editing, setEditing] = useState<SupplierContractRow | null>(null);
  const [creating, setCreating] = useState(false);

  const fetchRows = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params = new URLSearchParams();
      if (productType) params.set('productType', productType);
      if (status) params.set('status', status);
      if (q) params.set('q', q);
      const res = await fetch(
        `/api/suppliers${params.toString() ? `?${params.toString()}` : ''}`,
        { cache: 'no-store' },
      );
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
  }, [productType, status, q]);

  useEffect(() => {
    fetchRows();
  }, [fetchRows]);

  async function handleDelete(row: SupplierContractRow) {
    if (!confirm(`Soft-delete contract with ${row.supplierName}?`)) return;
    const res = await fetch(`/api/suppliers/${row.id}`, { method: 'DELETE' });
    if (!res.ok) {
      alert(`Delete failed: ${res.status}`);
      return;
    }
    fetchRows();
  }

  return (
    <div className="flex flex-col gap-4">
      {/* Filters */}
      <div className="flex flex-wrap items-end gap-3 rounded border border-slate-200 bg-white p-3">
        <label className="flex flex-col gap-1 text-xs">
          <span className="font-medium text-slate-600">Product type</span>
          <select
            className="rounded border border-slate-300 px-2 py-1 text-sm"
            value={productType}
            onChange={(e) => setProductType(e.target.value)}
          >
            <option value="">All</option>
            <option value="Flight">Flight</option>
            <option value="Hotel">Hotel</option>
            <option value="Car">Car</option>
            <option value="Package">Package</option>
          </select>
        </label>
        <label className="flex flex-col gap-1 text-xs">
          <span className="font-medium text-slate-600">Status</span>
          <select
            className="rounded border border-slate-300 px-2 py-1 text-sm"
            value={status}
            onChange={(e) => setStatus(e.target.value)}
          >
            <option value="">All</option>
            <option value="Active">Active</option>
            <option value="Upcoming">Upcoming</option>
            <option value="Expired">Expired</option>
          </select>
        </label>
        <label className="flex flex-col gap-1 text-xs">
          <span className="font-medium text-slate-600">Supplier search</span>
          <input
            type="text"
            className="rounded border border-slate-300 px-2 py-1 text-sm"
            placeholder="e.g. Airline X"
            value={q}
            onChange={(e) => setQ(e.target.value)}
          />
        </label>
        <div className="ml-auto">
          {canMutate ? (
            <button
              type="button"
              className="rounded bg-slate-900 px-3 py-1.5 text-sm font-medium text-white hover:bg-slate-800"
              onClick={() => setCreating(true)}
            >
              New contract
            </button>
          ) : null}
        </div>
      </div>

      {loading ? (
        <p className="text-sm text-slate-500">Loading…</p>
      ) : null}
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
                <th className="px-3 py-2">Supplier</th>
                <th className="px-3 py-2">Product</th>
                <th className="px-3 py-2">Net rate</th>
                <th className="px-3 py-2">Commission %</th>
                <th className="px-3 py-2">Validity</th>
                <th className="px-3 py-2">Status</th>
                {canMutate ? <th className="px-3 py-2">Actions</th> : null}
              </tr>
            </thead>
            <tbody>
              {rows.map((r) => (
                <tr key={r.id} className="border-t border-slate-100">
                  <td className="px-3 py-2 text-slate-900">{r.supplierName}</td>
                  <td className="px-3 py-2 text-slate-700">{r.productType}</td>
                  <td className="px-3 py-2 text-slate-700">
                    {r.netRate.toFixed(2)} {r.currency}
                  </td>
                  <td className="px-3 py-2 text-slate-700">
                    {r.commissionPercent}%
                  </td>
                  <td className="px-3 py-2 text-slate-700">
                    {r.validFrom.slice(0, 10)} → {r.validTo.slice(0, 10)}
                  </td>
                  <td className="px-3 py-2">
                    <span
                      className={`inline-flex rounded px-2 py-0.5 text-xs font-semibold ${STATUS_STYLES[r.status]}`}
                    >
                      {r.status}
                    </span>
                  </td>
                  {canMutate ? (
                    <td className="px-3 py-2">
                      <div className="flex gap-2">
                        <button
                          type="button"
                          className="text-xs font-medium text-sky-700 hover:underline"
                          onClick={() => setEditing(r)}
                        >
                          Edit
                        </button>
                        <button
                          type="button"
                          className="text-xs font-medium text-rose-700 hover:underline"
                          onClick={() => handleDelete(r)}
                        >
                          Delete
                        </button>
                      </div>
                    </td>
                  ) : null}
                </tr>
              ))}
              {rows.length === 0 ? (
                <tr>
                  <td
                    colSpan={canMutate ? 7 : 6}
                    className="px-3 py-6 text-center text-sm text-slate-500"
                  >
                    No supplier contracts match the current filter.
                  </td>
                </tr>
              ) : null}
            </tbody>
          </table>
        </div>
      ) : null}

      {creating ? (
        <SupplierContractForm
          mode="create"
          initial={null}
          onClose={() => setCreating(false)}
          onSaved={() => {
            setCreating(false);
            fetchRows();
          }}
        />
      ) : null}
      {editing ? (
        <SupplierContractForm
          mode="edit"
          initial={editing}
          onClose={() => setEditing(null)}
          onSaved={() => {
            setEditing(null);
            fetchRows();
          }}
        />
      ) : null}
    </div>
  );
}
