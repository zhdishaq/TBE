// Plan 05-05 Task 4 — TransactionsTable (page-number paginated ledger).
//
// Facts cover (from 05-05-PLAN Task 4 behavior block):
//   1. Renders 3 rows with columns [When, Type, Description, Amount]; money
//      cells have `tabular-nums` class (D-44 compact money column).
//   2. SignedAmount-aware tint — Release row bg-red-50; TopUp row bg-green-50.
//   3. Page-number pagination (D-44 line 41) — useQuery keyed by
//      ['wallet','transactions', {page, size}]; Next triggers page=2 fetch;
//      size select switching 20→50 resets page to 1 (NOT useInfiniteQuery).
//   4. Empty state literal "No transactions yet".

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { TransactionsTable } from '@/app/(portal)/admin/wallet/transactions-table';

function renderWithClient(ui: React.ReactElement) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, refetchOnMount: false } },
  });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe('TransactionsTable', () => {
  beforeEach(() => {
    vi.unstubAllGlobals();
  });

  it('renders 3 rows with tabular-nums on money column', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        headers: new Headers({ 'content-type': 'application/json' }),
        json: async () => ({
          items: [
            { id: '1', occurredAt: '2026-04-01T10:00:00Z', type: 'TopUp', description: 'Top-up via Stripe', signedAmount: 250, currency: 'GBP' },
            { id: '2', occurredAt: '2026-04-02T11:00:00Z', type: 'Release', description: 'Release hold BK-123', signedAmount: -120, currency: 'GBP' },
            { id: '3', occurredAt: '2026-04-03T12:00:00Z', type: 'Commit', description: 'Commit hold BK-456', signedAmount: -75, currency: 'GBP' },
          ],
          totalPages: 1,
          total: 3,
        }),
      }),
    );

    renderWithClient(<TransactionsTable />);

    await waitFor(() => {
      expect(screen.getAllByRole('row').length).toBeGreaterThanOrEqual(4);
    });
    const moneyCells = document.querySelectorAll('.tabular-nums');
    expect(moneyCells.length).toBeGreaterThan(0);
  });

  it('applies bg-red-50 to debit rows and bg-green-50 to credit rows (SignedAmount tint)', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        headers: new Headers({ 'content-type': 'application/json' }),
        json: async () => ({
          items: [
            { id: '1', occurredAt: '2026-04-01T10:00:00Z', type: 'TopUp', description: 'TopUp', signedAmount: 250, currency: 'GBP' },
            { id: '2', occurredAt: '2026-04-02T11:00:00Z', type: 'Release', description: 'Release', signedAmount: -120, currency: 'GBP' },
          ],
          totalPages: 1,
          total: 2,
        }),
      }),
    );

    renderWithClient(<TransactionsTable />);

    await waitFor(() => {
      const rows = document.querySelectorAll('tr[data-txn-id]');
      expect(rows.length).toBe(2);
    });
    const debitRow = document.querySelector('tr[data-txn-id="2"]');
    const creditRow = document.querySelector('tr[data-txn-id="1"]');
    expect(debitRow?.className).toMatch(/bg-red-50/);
    expect(creditRow?.className).toMatch(/bg-green-50/);
  });

  it('page-number pagination: Next advances ?page=, size change resets page to 1', async () => {
    const fetchSpy = vi.fn().mockImplementation((url: string) => {
      return Promise.resolve({
        ok: true,
        status: 200,
        headers: new Headers({ 'content-type': 'application/json' }),
        json: async () => ({
          items: [
            { id: url, occurredAt: '2026-04-01T10:00:00Z', type: 'TopUp', description: 'r', signedAmount: 1, currency: 'GBP' },
          ],
          totalPages: 5,
          total: 100,
        }),
      });
    });
    vi.stubGlobal('fetch', fetchSpy);

    renderWithClient(<TransactionsTable />);

    // Initial: ?page=1&size=20
    await waitFor(() => {
      expect(fetchSpy).toHaveBeenCalled();
    });
    const firstUrl = fetchSpy.mock.calls[0][0] as string;
    expect(firstUrl).toContain('page=1');
    expect(firstUrl).toContain('size=20');

    // Next → page=2
    await userEvent.click(screen.getByRole('button', { name: /next/i }));
    await waitFor(() => {
      const urls = fetchSpy.mock.calls.map((c) => c[0] as string);
      expect(urls.some((u) => u.includes('page=2'))).toBe(true);
    });

    // Size 20 → 50 should reset page to 1
    const sizeSelect = screen.getByLabelText(/rows per page/i) as HTMLSelectElement;
    fireEvent.change(sizeSelect, { target: { value: '50' } });
    await waitFor(() => {
      const urls = fetchSpy.mock.calls.map((c) => c[0] as string);
      expect(urls.some((u) => u.includes('page=1') && u.includes('size=50'))).toBe(true);
    });
  });

  it('renders "No transactions yet" when items is empty', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        headers: new Headers({ 'content-type': 'application/json' }),
        json: async () => ({ items: [], totalPages: 0, total: 0 }),
      }),
    );

    renderWithClient(<TransactionsTable />);

    await waitFor(() => {
      expect(screen.getByText(/no transactions yet/i)).toBeInTheDocument();
    });
  });
});
