// /flights/[offerId] — fare-rule drawer (UI-SPEC §Fare Rules).
//
// Shows baggage allowance + change fees + cancellation terms. The
// Continue button advances to /checkout/details carrying the offerId.
//
// Pitfall 11: `params` is a Promise in Next.js 16 dynamic routes; we
// await it before use.

import Link from 'next/link';
import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Fare rules',
};

interface FareRules {
  baggage: { included: number; unit: string };
  changes: { allowed: boolean; fee?: number; currency?: string };
  cancellation: { allowed: boolean; refundable: boolean };
  notes: string[];
}

async function loadFareRules(offerId: string): Promise<FareRules | null> {
  const gateway = process.env.GATEWAY_URL;
  if (!gateway) return null;
  try {
    const r = await fetch(`${gateway}/offers/${encodeURIComponent(offerId)}/rules`, {
      cache: 'no-store',
    });
    if (!r.ok) return null;
    return (await r.json()) as FareRules;
  } catch {
    return null;
  }
}

export default async function OfferDetailsPage({
  params,
}: {
  params: Promise<{ offerId: string }>;
}) {
  const { offerId } = await params;
  const rules = await loadFareRules(offerId);

  return (
    <main className="flex w-full flex-col px-6 py-8 md:px-10 lg:px-20">
      <div className="mx-auto w-full max-w-3xl">
        <h1 className="text-2xl font-semibold">Fare details</h1>
        <p className="text-sm text-muted-foreground">Offer ID: {offerId}</p>

        <section className="mt-6 flex flex-col gap-4">
          {!rules && (
            <div className="rounded-md border border-dashed border-border p-4 text-sm text-muted-foreground">
              Fare rules will appear here once the upstream pricing detail
              endpoint is wired. Continuing to checkout locks your fare.
            </div>
          )}
          {rules && (
            <>
              <div>
                <h2 className="text-sm font-semibold">Baggage</h2>
                <p className="text-sm">
                  {rules.baggage.included} {rules.baggage.unit} checked baggage included.
                </p>
              </div>
              <div>
                <h2 className="text-sm font-semibold">Changes</h2>
                <p className="text-sm">
                  {rules.changes.allowed
                    ? `Changes allowed ${
                        rules.changes.fee ? `for a ${rules.changes.currency ?? ''} ${rules.changes.fee} fee` : 'free of charge'
                      }.`
                    : 'Not changeable after booking.'}
                </p>
              </div>
              <div>
                <h2 className="text-sm font-semibold">Cancellation</h2>
                <p className="text-sm">
                  {rules.cancellation.allowed
                    ? rules.cancellation.refundable
                      ? 'Refundable if cancelled before departure.'
                      : 'Cancellable but non-refundable.'
                    : 'Non-cancellable.'}
                </p>
              </div>
              {rules.notes.length > 0 && (
                <ul className="list-disc ps-5 text-xs text-muted-foreground">
                  {rules.notes.map((n, i) => (
                    <li key={i}>{n}</li>
                  ))}
                </ul>
              )}
            </>
          )}
        </section>

        <div className="mt-8 flex items-center justify-end">
          <Link
            href={`/checkout/details?offerId=${encodeURIComponent(offerId)}`}
            className="inline-flex items-center gap-1.5 rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
          >
            Continue
          </Link>
        </div>
      </div>
    </main>
  );
}
