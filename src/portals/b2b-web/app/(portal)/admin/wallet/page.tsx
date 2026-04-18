// Plan 05-05 Task 4 — /admin/wallet RSC page.
//
// TanStack Query HydrationBoundary + dehydrate pattern (05-PATTERNS §19):
//   - Server-side prefetches three queries (balance, threshold, transactions
//     page 1 size 20) via gatewayFetch so the client's initial render has a
//     warm cache (no fetch-on-mount flicker).
//   - Role guard: `agent-admin` only; non-admin → redirect(/forbidden). The
//     backend is authoritative (B2BAdminPolicy on PUT/POST) — this guard is
//     for UX polish.
//   - Hydrates cache keys ['wallet','balance'], ['wallet','threshold'],
//     ['wallet','transactions', {page:1,size:20}] so TransactionsTable +
//     ThresholdDialog + LowBalanceBanner see the same cache.
//
// Compact three-section layout (UI-SPEC §11 screen 7): top-up form on the
// left, threshold editor on the right, transactions table below spanning
// both columns.

import { redirect } from 'next/navigation';
import {
  HydrationBoundary,
  QueryClient,
  dehydrate,
} from '@tanstack/react-query';
import { auth } from '@/lib/auth';
import { gatewayFetch } from '@/lib/api-client';
import { TopUpForm } from './top-up-form';
import { ThresholdDialog } from './threshold-dialog';
import { TransactionsTable } from './transactions-table';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

async function fetchBalance(): Promise<{ amount: number; currency: string } | null> {
  try {
    const r = await gatewayFetch('/api/b2b/wallet/balance');
    if (!r.ok) return null;
    return (await r.json()) as { amount: number; currency: string };
  } catch {
    return null;
  }
}

async function fetchThreshold(): Promise<{ threshold: number; currency: string }> {
  try {
    const r = await gatewayFetch('/api/b2b/wallet/threshold');
    if (!r.ok) return { threshold: 500, currency: 'GBP' };
    return (await r.json()) as { threshold: number; currency: string };
  } catch {
    return { threshold: 500, currency: 'GBP' };
  }
}

async function fetchTransactions(): Promise<{ items: unknown[]; totalPages: number; total: number }> {
  try {
    const r = await gatewayFetch('/api/b2b/wallet/transactions?page=1&size=20');
    if (!r.ok) return { items: [], totalPages: 0, total: 0 };
    return (await r.json()) as { items: unknown[]; totalPages: number; total: number };
  } catch {
    return { items: [], totalPages: 0, total: 0 };
  }
}

export default async function AdminWalletPage() {
  const session = await auth();
  const roles = (session as { roles?: string[] } | undefined)?.roles ?? [];
  if (!roles.includes('agent-admin')) {
    redirect('/forbidden');
  }

  const queryClient = new QueryClient();
  await Promise.all([
    queryClient.prefetchQuery({ queryKey: ['wallet', 'balance'], queryFn: fetchBalance }),
    queryClient.prefetchQuery({ queryKey: ['wallet', 'threshold'], queryFn: fetchThreshold }),
    queryClient.prefetchQuery({
      queryKey: ['wallet', 'transactions', { page: 1, size: 20 }],
      queryFn: fetchTransactions,
    }),
  ]);

  return (
    <HydrationBoundary state={dehydrate(queryClient)}>
      <div className="space-y-6 p-6">
        <header className="flex items-center justify-between">
          <h1 className="text-2xl font-semibold">Wallet admin</h1>
        </header>

        <section className="grid gap-6 lg:grid-cols-2">
          <div className="rounded-lg border p-4">
            <h2 className="mb-3 text-sm font-semibold text-muted-foreground">
              Top up
            </h2>
            <TopUpForm />
          </div>
          <div className="rounded-lg border p-4">
            <h2 className="mb-3 text-sm font-semibold text-muted-foreground">
              Low-balance threshold
            </h2>
            <ThresholdDialog />
          </div>
        </section>

        <section className="rounded-lg border p-4">
          <h2 className="mb-3 text-sm font-semibold text-muted-foreground">
            Transactions
          </h2>
          <TransactionsTable />
        </section>
      </div>
    </HydrationBoundary>
  );
}
