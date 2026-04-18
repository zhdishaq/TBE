// Plan 05-04 Task 3 — authenticated /dashboard.
//
// RSC page composing:
//   GET /api/b2b/dashboard/summary   (TTL buckets + recent bookings, backend-filtered by agency_id)
//   GET /api/b2b/wallet/balance       (wallet summary)
//
// Layout: 2-column grid per D-44 — left column = TTL alerts + recent
// bookings; right column = wallet summary + quick links.
//
// Graceful degradation: if either upstream 502s, the card renders with a
// friendly empty / unavailable message rather than crashing the whole page.

import { auth } from '@/lib/auth';
import { gatewayFetch } from '@/lib/api-client';
import { TtlAlertsCard, type TtlAlertsRow } from '@/components/dashboard/ttl-alerts-card';
import { WalletSummaryCard } from '@/components/dashboard/wallet-summary-card';
import {
  RecentBookingsCard,
  type RecentBookingRow,
} from '@/components/dashboard/recent-bookings-card';
import { QuickLinksGrid } from '@/components/dashboard/quick-links-grid';

export const dynamic = 'force-dynamic';
export const runtime = 'nodejs';

interface DashboardSummary {
  ttlWarn: TtlAlertsRow[];
  ttlUrgent: TtlAlertsRow[];
  recentBookings: RecentBookingRow[];
}

interface WalletBalance {
  amount: number;
  currency: string;
  threshold: number;
}

async function fetchSummary(): Promise<DashboardSummary> {
  try {
    const r = await gatewayFetch('/api/b2b/dashboard/summary');
    if (!r.ok) return { ttlWarn: [], ttlUrgent: [], recentBookings: [] };
    return (await r.json()) as DashboardSummary;
  } catch {
    return { ttlWarn: [], ttlUrgent: [], recentBookings: [] };
  }
}

async function fetchBalance(): Promise<WalletBalance | null> {
  try {
    const r = await gatewayFetch('/api/b2b/wallet/balance');
    if (!r.ok) return null;
    return (await r.json()) as WalletBalance;
  } catch {
    return null;
  }
}

export default async function DashboardPage() {
  const session = await auth();
  const roles = session?.roles ?? [];

  const [summary, balance] = await Promise.all([fetchSummary(), fetchBalance()]);

  return (
    <main className="mx-auto flex max-w-6xl flex-col gap-6 px-6 py-8">
      <header>
        <h1 className="text-2xl font-semibold text-foreground">Dashboard</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Agency activity, ticketing deadlines, and quick access to
          common tasks.
        </p>
      </header>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        {/* Left column */}
        <div className="flex flex-col gap-6">
          <TtlAlertsCard
            warn={summary.ttlWarn}
            urgent={summary.ttlUrgent}
          />
          <RecentBookingsCard bookings={summary.recentBookings} />
        </div>

        {/* Right column */}
        <div className="flex flex-col gap-6">
          {balance ? (
            <WalletSummaryCard
              balance={balance.amount}
              currency={balance.currency}
              threshold={balance.threshold}
              roles={roles}
            />
          ) : (
            <section className="rounded-lg border border-border bg-card p-6 text-sm text-muted-foreground">
              Wallet balance temporarily unavailable.
            </section>
          )}
          <QuickLinksGrid roles={roles} />
        </div>
      </div>
    </main>
  );
}
