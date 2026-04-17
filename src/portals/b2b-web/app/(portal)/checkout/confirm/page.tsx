// Plan 05-02 Task 3 -- /checkout/confirm RSC.
//
// Server-renders the wallet-gated checkout. The balance comparison happens
// SERVER-side (never client) so a forged client balance can never elevate a
// non-admin into the DebitSummary branch. Rules:
//   - balance >= gross -> render <DebitSummary /> (client CTA POSTs to
//     /api/b2b/bookings; on 202 -> /checkout/success?booking=...)
//   - balance <  gross -> render <InsufficientFundsPanel /> and NOT render
//     DebitSummary or the Confirm CTA (UI-SPEC `/checkout/confirm`).
//
// Pitfall 5 / T-05-02-05: this file MUST NOT import any Stripe symbol, and
// the CSP for the route MUST NOT whitelist js.stripe.com. B2B is internal
// ledger only -- the whole Stripe flow is structurally excluded from
// agent portal checkout.

import { redirect } from 'next/navigation';
import { auth } from '@/lib/auth';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';
import { DebitSummary } from '@/components/checkout/debit-summary';
import { InsufficientFundsPanel } from '@/components/checkout/insufficient-funds-panel';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

interface PageProps {
  searchParams: Promise<{
    offer?: string;
    gross?: string;
    currency?: string;
  }>;
}

interface WalletBalancePayload {
  amount: number;
  currency: string;
}

async function loadBalance(): Promise<WalletBalancePayload | null> {
  try {
    const r = await gatewayFetch('/api/b2b/wallet/balance');
    if (!r.ok) return null;
    return (await r.json()) as WalletBalancePayload;
  } catch (err) {
    if (err instanceof UnauthenticatedError) return null;
    return null;
  }
}

export default async function CheckoutConfirmPage({ searchParams }: PageProps) {
  const session = await auth();
  if (!session) redirect('/login');
  const roles = session.roles ?? [];
  const adminEmail = (session.user as { agency_admin_email?: string } | undefined)
    ?.agency_admin_email;

  const sp = await searchParams;
  const gross = Number.parseFloat(sp.gross ?? '0');
  const currency = sp.currency ?? 'GBP';
  const offerId = sp.offer ?? '';

  const balance = await loadBalance();
  const balanceAmount = balance?.amount ?? 0;
  const hasFunds = balanceAmount >= gross;

  return (
    <section className="mx-auto flex w-full max-w-3xl flex-col gap-6 px-4 py-6">
      <header className="flex flex-col gap-1">
        <h1 className="text-xl font-semibold">Confirm booking</h1>
        <p className="text-sm text-muted-foreground">
          Offer {offerId || '(not selected)'}. The wallet-debit completes the
          sale -- no card capture is required.
        </p>
      </header>
      {hasFunds ? (
        <DebitSummary
          gross={gross}
          currency={currency}
          balance={balanceAmount}
          onConfirm="/api/b2b/bookings"
          payload={{ offerId }}
          roles={roles}
          adminEmail={adminEmail}
        />
      ) : (
        <InsufficientFundsPanel
          gross={gross}
          balance={balanceAmount}
          currency={currency}
          roles={roles}
          adminEmail={adminEmail}
        />
      )}
    </section>
  );
}
