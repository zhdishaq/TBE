// Plan 05-02 Task 3 — B2B /search/flights RSC.
//
// Mounts the search form + <DualPricingGrid /> inside <Suspense>. The search
// itself is wired by the downstream client component in a subsequent wave;
// this RSC keeps the shell tidy and passes a mock-free empty offer list when
// no search params are present (UI-SPEC §Dual-pricing Grid — empty state).

import { Suspense } from 'react';
import { FlightSearchForm } from '@/app/(portal)/search/flights/flight-search-form';
import {
  DualPricingGrid,
  type PricedOffer,
} from '@/app/(portal)/search/flights/dual-pricing-grid';

interface SearchParams {
  from?: string;
  to?: string;
  depart?: string;
  return?: string;
  cabin?: string;
}

async function loadOffers(_params: SearchParams): Promise<PricedOffer[]> {
  // Placeholder — wave 2 will pipe through the gateway
  // /api/b2b/pricing/flights endpoint. The server-side helper keeps the
  // RSC cache-key scoped per-agency (D-33) so results never leak across
  // agency sessions.
  return [];
}

export default async function FlightsSearchPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const offers = await loadOffers(params);
  return (
    <div className="mx-auto flex max-w-6xl flex-col gap-6 px-4 py-6">
      <FlightSearchForm
        defaultFrom={params.from ?? ''}
        defaultTo={params.to ?? ''}
        defaultDepart={params.depart ?? ''}
        defaultReturn={params.return ?? ''}
      />
      <Suspense fallback={<p>Loading flights…</p>}>
        <DualPricingGrid offers={offers} />
      </Suspense>
    </div>
  );
}
