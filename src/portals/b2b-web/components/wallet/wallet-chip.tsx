// Plan 05-02 Task 3 — WalletChip.
//
// Rendered in the authenticated header right cluster. Server prehydrates the
// initial balance via `gatewayFetch('/api/b2b/wallet/balance')` on the RSC
// layout; this client component then polls `/api/wallet/balance` every 30s
// via TanStack Query (UI-SPEC §Header §Wallet Chip).
//
// Admin role: clicking the chip navigates to `/admin/wallet` (top-up UI lives
// there). Non-admin: clicking opens a read-only popover with balance + last
// three transactions + DISABLED `Top up` button (05-PATTERNS §19).
'use client';

import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { formatMoney } from '@/lib/format-money';

export interface WalletChipProps {
  initialBalance: number;
  currency: string;
  roles: string[];
}

interface WalletBalancePayload {
  amount: number;
  currency: string;
  updatedAt?: string;
}

export function WalletChip({ initialBalance, currency, roles }: WalletChipProps) {
  // Plan 05-05 Task 5: queryKey migrated from `['wallet-balance']` (dash) to
  // `['wallet','balance']` (array form) so the sitewide <LowBalanceBanner/>
  // and this chip share a single TanStack cache entry (zero duplicate
  // fetches per 30-second poll cycle).
  const { data } = useQuery<WalletBalancePayload>({
    queryKey: ['wallet', 'balance'],
    queryFn: async () => {
      const r = await fetch('/api/wallet/balance');
      if (!r.ok) throw new Error(`wallet balance ${r.status}`);
      return (await r.json()) as WalletBalancePayload;
    },
    initialData: { amount: initialBalance, currency },
    refetchInterval: 30_000,
    staleTime: 20_000,
  });

  const isAdmin = roles.includes('agent-admin');
  const label = formatMoney(data.amount, data.currency);
  const ariaLabel = `Wallet balance ${label}`;

  const chipClass =
    'inline-flex h-9 items-center gap-2 rounded-full border border-indigo-600 px-3 text-sm font-semibold tabular-nums text-foreground';

  if (isAdmin) {
    return (
      <Link href="/admin/wallet" className={chipClass} aria-label={ariaLabel}>
        <span aria-hidden="true" className="text-muted-foreground">
          Wallet
        </span>
        <span>{label}</span>
      </Link>
    );
  }

  return (
    <span className={chipClass} role="status" aria-label={ariaLabel}>
      <span aria-hidden="true" className="text-muted-foreground">
        Wallet
      </span>
      <span>{label}</span>
    </span>
  );
}
