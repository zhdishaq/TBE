// Plan 05-05 Task 4 — TransactionsTable.
//
// D-44 page-number pagination (NOT cursor / NOT useInfiniteQuery). Size
// options [20, 50, 100] defaulting to 20; changing size MUST reset page to 1.
// Row tint follows SignedAmount: negative → bg-red-50 (Release/Commit debits);
// positive → bg-green-50 (TopUp credits). Money cells have `tabular-nums` +
// row height h-11 per D-44 compact styling.

'use client';

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { formatMoney } from '@/lib/format-money';

interface LedgerItem {
  id: string;
  occurredAt: string;
  type: string;
  description: string;
  signedAmount: number;
  currency: string;
}

interface LedgerPayload {
  items: LedgerItem[];
  totalPages: number;
  total: number;
}

export function TransactionsTable(): React.ReactElement {
  const [page, setPage] = useState(1);
  const [size, setSize] = useState(20);

  const { data, isLoading } = useQuery<LedgerPayload>({
    queryKey: ['wallet', 'transactions', { page, size }],
    queryFn: async () => {
      const r = await fetch(`/api/wallet/transactions?page=${page}&size=${size}`);
      if (!r.ok) throw new Error(`transactions ${r.status}`);
      return (await r.json()) as LedgerPayload;
    },
  });

  // Always render the pager even while loading so pagination controls
  // remain actionable during refetches (the test clicks Next immediately
  // after the first fetch resolves — we must not swap the whole tree out
  // for a "Loading..." placeholder).
  const items = data?.items ?? [];
  const totalPages = data?.totalPages ?? 1;

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between text-sm">
        <div>
          <label htmlFor="txn-size" className="mr-2 text-muted-foreground">
            Rows per page
          </label>
          <select
            id="txn-size"
            value={size}
            onChange={(e) => {
              setSize(Number(e.target.value));
              setPage(1);
            }}
            className="h-9 rounded-md border px-2 text-sm"
          >
            <option value={20}>20</option>
            <option value={50}>50</option>
            <option value={100}>100</option>
          </select>
        </div>
        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={() => setPage((p) => Math.max(1, p - 1))}
            disabled={page <= 1}
            className="h-9 rounded-md border px-3 text-sm disabled:opacity-50"
          >
            Previous
          </button>
          <span className="tabular-nums text-muted-foreground">
            Page {page} of {totalPages}
          </span>
          <button
            type="button"
            onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
            disabled={page >= totalPages}
            className="h-9 rounded-md border px-3 text-sm disabled:opacity-50"
          >
            Next
          </button>
        </div>
      </div>

      {items.length === 0 ? (
        <div className="rounded-md border p-6 text-center text-sm text-muted-foreground">
          No transactions yet
        </div>
      ) : (
        <table className="w-full text-sm">
          <thead>
            <tr className="h-11 border-b text-left text-muted-foreground">
              <th className="px-3 font-medium">When</th>
              <th className="px-3 font-medium">Type</th>
              <th className="px-3 font-medium">Description</th>
              <th className="px-3 text-right font-medium">Amount</th>
            </tr>
          </thead>
          <tbody>
            {items.map((it) => (
              <tr
                key={it.id}
                data-txn-id={it.id}
                className={`h-11 border-b ${
                  it.signedAmount < 0 ? 'bg-red-50' : 'bg-green-50'
                }`}
              >
                <td className="px-3 tabular-nums">
                  {new Date(it.occurredAt).toLocaleString('en-GB')}
                </td>
                <td className="px-3">{it.type}</td>
                <td className="px-3">{it.description}</td>
                <td className="px-3 text-right tabular-nums">
                  {formatMoney(it.signedAmount, it.currency)}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
