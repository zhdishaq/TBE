// Plan 05-04 Task 3 — WalletSummaryCard.
//
// Dashboard-grid wallet card showing current balance, threshold, and a
// low-balance banner when balance <= threshold. Admin-only top-up CTA
// conditional on `roles.includes('agent-admin')`. Read-only for agent /
// agent-readonly (UI-SPEC §Dashboard, D-44).

import Link from 'next/link';
import { formatMoney } from '@/lib/format-money';
import { cn } from '@/lib/utils';

interface WalletSummaryCardProps {
  balance: number;
  currency: string;
  threshold: number;
  roles: string[];
}

export function WalletSummaryCard({
  balance,
  currency,
  threshold,
  roles,
}: WalletSummaryCardProps) {
  const isAdmin = roles.includes('agent-admin');
  const isLow = balance <= threshold;
  return (
    <section
      aria-labelledby="wallet-summary-heading"
      className="rounded-lg border border-border bg-card p-6 shadow-sm"
    >
      <h2
        id="wallet-summary-heading"
        className="mb-4 text-lg font-semibold text-foreground"
      >
        Wallet
      </h2>

      <p className="mb-1 text-3xl font-semibold tabular-nums text-foreground">
        {formatMoney(balance, currency)}
      </p>
      <p className="text-sm text-muted-foreground">
        Threshold {formatMoney(threshold, currency)}
      </p>

      {isLow && (
        <div
          role="status"
          className={cn(
            'mt-4 rounded-md border border-red-300 bg-red-50 px-4 py-3 text-sm text-red-900',
            'dark:border-red-900 dark:bg-red-950/30 dark:text-red-200',
          )}
        >
          Low balance — top up to keep confirming bookings.
        </div>
      )}

      {isAdmin && (
        <Link
          href="/admin/wallet"
          className="mt-4 inline-flex h-9 items-center rounded-md bg-indigo-600 px-4 text-sm font-medium text-white hover:bg-indigo-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
        >
          Manage wallet
        </Link>
      )}
    </section>
  );
}
