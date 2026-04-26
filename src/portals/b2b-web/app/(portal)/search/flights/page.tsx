// Plan 05-02 Task 3 — B2B /search/flights RSC.
//
// Mounts the search form + <DualPricingGrid /> inside <Suspense>. Calls the
// gateway through gatewayFetch for the actual flight search; results are
// transformed from UnifiedFlightOffer into PricedOffer for the dual-pricing
// grid. Per-agency markup is applied server-side by the search-service /
// pricing-service pipeline (Plan 05-02 T-05-02-01).

import { Suspense } from 'react';
import { FlightSearchForm } from '@/app/(portal)/search/flights/flight-search-form';
import {
  DualPricingGrid,
  type PricedOffer,
} from '@/app/(portal)/search/flights/dual-pricing-grid';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';

interface SearchParams {
  from?: string;
  to?: string;
  depart?: string;
  return?: string;
  cabin?: string;
  adt?: string;
  chd?: string;
  inf?: string;
}

interface UnifiedFlightOffer {
  offerId: string;
  source: string;
  cabinClass: string;
  segments: Array<{
    departureAirport: string;
    arrivalAirport: string;
    departureAt: string;
    arrivalAt: string;
    carrierCode: string;
    flightNumber: string;
    durationMinutes: number;
  }>;
  price: {
    currency: string;
    base: number;
    surcharges: Array<{ code: string; amount: number }>;
    taxes: Array<{ code: string; amount: number }>;
    grossSellingPrice?: number;
    markupApplied?: boolean;
  };
}

const CABIN_MAP: Record<string, string> = {
  economy: 'ECONOMY',
  premium: 'PREMIUM_ECONOMY',
  business: 'BUSINESS',
  first: 'FIRST',
};

async function loadOffers(params: SearchParams): Promise<PricedOffer[]> {
  // No search yet — return empty grid (UI-SPEC §empty state)
  if (!params.from || !params.to || !params.depart) return [];

  const requestBody = {
    origin: params.from.toUpperCase(),
    destination: params.to.toUpperCase(),
    departureDate: params.depart,
    returnDate: params.return ?? null,
    adults: Number(params.adt ?? 1),
    children: Number(params.chd ?? 0),
    infants: Number(params.inf ?? 0),
    travelClass: CABIN_MAP[params.cabin ?? 'economy'] ?? 'ECONOMY',
    currencyCode: 'SAR',
    maxResults: 50,
  };

  try {
    const upstream = await gatewayFetch('/api/b2b/search/flights', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(requestBody),
    });

    if (!upstream.ok) {
      console.error(`Search failed: ${upstream.status}`);
      return [];
    }

    const offers = (await upstream.json()) as UnifiedFlightOffer[];
    return offers.map(transformToPriced);
  } catch (err) {
    if (err instanceof UnauthenticatedError) {
      // B2B requires auth — caller will be redirected by middleware
      return [];
    }
    console.error('Flight search error:', err);
    return [];
  }
}

function transformToPriced(offer: UnifiedFlightOffer): PricedOffer {
  const surcharges = offer.price.surcharges.reduce((sum, s) => sum + s.amount, 0);
  const taxes      = offer.price.taxes.reduce((sum, t) => sum + t.amount, 0);
  const net        = offer.price.base + surcharges + taxes;

  // GrossSellingPrice from PricingService — fall back to 8% markup if
  // pricing-service was unavailable on this request (search-service falls
  // through to raw offers in that case)
  const gross = offer.price.grossSellingPrice ?? Math.round(net * 1.08 * 100) / 100;
  const markup = Math.round((gross - net) * 100) / 100;

  // Commission is half the markup (placeholder — Plan 05-02 will wire the
  // real per-agency commission split from the pricing service)
  const commission = Math.round(markup * 0.5 * 100) / 100;

  const firstSeg = offer.segments[0];
  const lastSeg  = offer.segments[offer.segments.length - 1];
  const totalDuration = offer.segments.reduce((sum, s) => sum + s.durationMinutes, 0);

  return {
    offerId:         offer.offerId,
    airline:         firstSeg?.carrierCode ?? '??',
    flightNumber:    firstSeg?.flightNumber ?? '',
    departAt:        firstSeg?.departureAt ?? '',
    arriveAt:        lastSeg?.arrivalAt ?? '',
    durationMinutes: totalDuration,
    stops:           Math.max(0, offer.segments.length - 1),
    net,
    markup,
    gross,
    commission,
    currency:        offer.price.currency,
  };
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
