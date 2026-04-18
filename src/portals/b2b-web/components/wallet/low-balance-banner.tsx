// Plan 05-05 Task 5 — LowBalanceBanner.
//
// Sitewide informational banner. Per UI-SPEC §11 lines 418 / 559 / 628 this
// is `role="status"` + `aria-live="polite"` — NOT `role="alert"`. `role=alert`
// is reserved for the blocking InsufficientFundsPanel in the checkout flow.
//
// Cache contract:
//   - `['wallet','balance']` shared with WalletChip (Task 5 also migrates
//     WalletChip from the old `['wallet-balance']` dash key to this array
//     form so both components hit the SAME TanStack cache — zero duplicate
//     fetches per 30-second poll cycle).
//   - `['wallet','threshold']` shared with the /admin/wallet hydration.
//
// Dismiss:
//   - sessionStorage only — `'lowBalanceDismissed' = '1'`. localStorage is
//     FORBIDDEN (T-05-05-05 hydration must not leak across tabs; the banner
//     must re-arm on a fresh tab).

'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { useQuery } from '@tanstack/react-query';
import { formatMoney } from '@/lib/format-money';
import { RequestTopUpLink } from './request-top-up-link';

interface WalletBalancePayload {
  amount: number;
  currency: string;
  updatedAt?: string;
}

interface WalletThresholdPayload {
  threshold: number;
  currency: string;
}

export interface LowBalanceBannerProps {
  roles: string[];
  adminEmail?: string;
}

const DISMISS_KEY = 'lowBalanceDismissed';

export function LowBalanceBanner({
  roles,
  adminEmail,
}: LowBalanceBannerProps): React.ReactElement | null {
  const [dismissed, setDismissed] = useState<boolean>(() => {
    // SSR-safe: sessionStorage is not available server-side. During the
    // first client render we read it synchronously so the banner does not
    // flash visible before the dismiss flag takes effect.
    if (typeof window === 'undefined') return false;
    return window.sessionStorage.getItem(DISMISS_KEY) === '1';
  });

  const { data: balanceData } = useQuery<WalletBalancePayload>({
    queryKey: ['wallet', 'balance'],
    queryFn: async () => {
      const r = await fetch('/api/wallet/balance');
      if (!r.ok) throw new Error(`wallet balance ${r.status}`);
      return (await r.json()) as WalletBalancePayload;
    },
    refetchInterval: 30_000,
    staleTime: 20_000,
  });

  const { data: thresholdData } = useQuery<WalletThresholdPayload>({
    queryKey: ['wallet', 'threshold'],
    queryFn: async () => {
      const r = await fetch('/api/wallet/threshold');
      if (!r.ok) throw new Error(`wallet threshold ${r.status}`);
      return (await r.json()) as WalletThresholdPayload;
    },
    staleTime: 60_000,
  });

  // Guards:
  //   - data not yet available,
  //   - balance >= threshold (hysteresis: the banner re-arms automatically
  //     when backend SetThresholdAsync resets LowBalanceEmailSent=0),
  //   - dismissed this session.
  if (!balanceData || !thresholdData) return null;
  if (balanceData.amount >= thresholdData.threshold) return null;
  if (dismissed) return null;

  const isAdmin = roles.includes('agent-admin');
  const currency = balanceData.currency || 'GBP';

  const onDismiss = () => {
    // NEVER touch localStorage here — T-05-05-05 forbids cross-tab
    // persistence of this UX preference; re-arming on new tab is the
    // intended UX per UI-SPEC §11.
    if (typeof window !== 'undefined') {
      window.sessionStorage.setItem(DISMISS_KEY, '1');
    }
    setDismissed(true);
  };

  return (
    <div
      role="status"
      aria-live="polite"
      className="flex items-center justify-between gap-3 border-b border-amber-300 bg-amber-50 px-4 py-2 text-sm text-amber-900"
    >
      <div className="flex-1">
        Your wallet balance ({formatMoney(balanceData.amount, currency)}) is
        below your alert threshold ({formatMoney(thresholdData.threshold, currency)}).
      </div>
      <div className="flex items-center gap-2">
        {isAdmin ? (
          <Link
            href="/admin/wallet"
            className="inline-flex h-8 items-center rounded-md bg-indigo-600 px-3 text-xs font-semibold text-white hover:bg-indigo-700"
          >
            Top up
          </Link>
        ) : (
          <RequestTopUpLink
            adminEmail={adminEmail}
            className="inline-flex h-8 items-center rounded-md border border-amber-300 bg-white px-3 text-xs font-semibold text-amber-900 hover:bg-amber-100"
          />
        )}
        <button
          type="button"
          onClick={onDismiss}
          className="inline-flex h-8 items-center rounded-md px-2 text-xs font-medium text-amber-900 hover:bg-amber-100"
          aria-label="Dismiss low-balance banner"
        >
          Dismiss
        </button>
      </div>
    </div>
  );
}
