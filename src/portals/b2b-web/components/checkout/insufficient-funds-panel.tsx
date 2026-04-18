// Plan 05-02 Task 3 — InsufficientFundsPanel.
//
// Rendered by /checkout/confirm when the agency wallet balance is below the
// booking GROSS. Never rendered alongside DebitSummary — the confirm RSC picks
// exactly one (UI-SPEC §Confirm Page §Insufficient Funds).
//
// D-42 / 05-UI-SPEC — `role="alert"` so assistive tech announces the gate
// immediately. Admin role gets a direct `Top up now` link to `/admin/wallet`;
// non-admin gets a `Request top-up` mailto link to the agency admin email.
//
// Plan 05-05 Task 5 retrofit — the non-admin mailto is now delegated to the
// <RequestTopUpLink/> primitive (subject-only href, no session material in
// the query string — T-05-03-09 / T-05-05-02 mitigation codified at the
// component level). The old inline mailto interpolated the booking GROSS
// amount into the subject — now the CTA carries ZERO session material.
'use client';

import Link from 'next/link';
import { formatMoney } from '@/lib/format-money';
import { RequestTopUpLink } from '@/components/wallet/request-top-up-link';

export interface InsufficientFundsPanelProps {
  gross: number;
  balance: number;
  currency: string;
  roles: string[];
  adminEmail?: string;
}

export function InsufficientFundsPanel({
  gross,
  balance,
  currency,
  roles,
  adminEmail,
}: InsufficientFundsPanelProps) {
  const isAdmin = roles.includes('agent-admin');
  const deficit = Math.max(0, gross - balance);

  return (
    <div
      role="alert"
      className="rounded-lg border border-red-200 bg-red-50 p-6 dark:border-red-800 dark:bg-red-950/40"
    >
      <h2 className="text-lg font-semibold text-red-900 dark:text-red-100">
        Insufficient wallet balance
      </h2>
      <p className="mt-2 text-sm text-red-800 dark:text-red-200">
        Your agency wallet has {formatMoney(balance, currency)}. This booking
        requires {formatMoney(gross, currency)} ({formatMoney(deficit, currency)}{' '}
        short).
      </p>
      {isAdmin ? (
        <Link
          href="/admin/wallet"
          className="mt-4 inline-flex h-9 items-center rounded-md bg-indigo-600 px-3 text-sm font-medium text-white hover:bg-indigo-700"
        >
          Top up now
        </Link>
      ) : (
        <RequestTopUpLink
          adminEmail={adminEmail}
          className="mt-4 inline-flex h-9 items-center rounded-md border border-zinc-300 px-3 text-sm font-medium hover:bg-zinc-50"
        />
      )}
    </div>
  );
}
