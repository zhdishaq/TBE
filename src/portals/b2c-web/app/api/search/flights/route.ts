// POST /api/search/flights — client → Next.js → gateway pass-through.
//
// The browser calls this route from `use-flight-search`. It uses
// `gatewayFetch` so the Keycloak access_token is attached server-side
// (D-05) — the browser never sees it.
//
// Anonymous browsing is allowed by CONTEXT; gatewayFetch throws
// UnauthenticatedError when no session is present, which we map to a
// direct anonymous fetch so the browse flow works pre-login.
//
// CONTRACT TRANSFORM: The client hook uses the b2c-web internal naming
// convention (`from`, `to`, `dep`, `ret`, `cabin`, `infantsLap`,
// `infantsSeat`) so that all UI code and URL query params stay
// consistent. The backend contract (FlightSearchRequest in
// TBE.Contracts) uses the airline-industry naming (`origin`,
// `destination`, `departureDate`, `returnDate`, `travelClass`,
// `infants`). This route is the single bridge between those two naming
// worlds — both for the request payload and for the response shape.

import { NextRequest, NextResponse } from 'next/server';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';

export const runtime = 'nodejs';

// ─── Client-side payload (what use-flight-search sends) ────────────────────
interface ClientSearchPayload {
  from: string;
  to: string;
  dep: string;
  ret?: string | null;
  adults: number;
  children: number;
  infantsLap: number;
  infantsSeat: number;
  cabin: 'economy' | 'premium' | 'business' | 'first' | string;
  nonStop?: boolean;
  currencyCode?: string;
  maxResults?: number;
}

// ─── Backend contract (FlightSearchRequest) ───────────────────────────────
interface UpstreamSearchPayload {
  origin: string;
  destination: string;
  departureDate: string;
  returnDate: string | null;
  adults: number;
  children: number;
  infants: number;
  travelClass: string;
  nonStop: boolean;
  currencyCode: string;
  maxResults: number;
}

// ─── Backend response (UnifiedFlightOffer) ────────────────────────────────
interface UnifiedFlightOffer {
  offerId: string;
  source: string;
  cabinClass: string;
  expiresAt: string;
  segments: Array<{
    departureAirport: string;
    arrivalAirport: string;
    departureAt: string;
    arrivalAt: string;
    carrierCode: string;
    flightNumber: string;
    durationMinutes: number;
    aircraftCode?: string;
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

// ─── Client-side response shape (what use-flight-search expects) ──────────
interface FlightOffer {
  offerId: string;
  airline: { code: string; name: string };
  segments: Array<{
    from: string;
    to: string;
    departure: string;
    arrival: string;
    flightNumber: string;
    cabin: string;
  }>;
  stops: number;
  durationMinutes: number;
  price: {
    base: number;
    yqYr: number;
    taxes: number;
    total: number;
    currency: string;
  };
  baggage?: { included: number; unit: 'kg' | 'pcs' } | null;
  expiresAt: string;
}

const CABIN_MAP: Record<string, string> = {
  economy: 'ECONOMY',
  premium: 'PREMIUM_ECONOMY',
  business: 'BUSINESS',
  first: 'FIRST',
};

const REVERSE_CABIN_MAP: Record<string, string> = {
  ECONOMY: 'economy',
  PREMIUM_ECONOMY: 'premium',
  BUSINESS: 'business',
  FIRST: 'first',
};

const AIRLINE_NAMES: Record<string, string> = {
  SV: 'Saudia',
  EK: 'Emirates',
  FZ: 'flydubai',
  GF: 'Gulf Air',
  WY: 'Oman Air',
  RJ: 'Royal Jordanian',
  MS: 'EgyptAir',
  KU: 'Kuwait Airways',
  XY: 'flynas',
  J9: 'Jazeera Airways',
  PK: 'Pakistan International Airlines',
  EY: 'Etihad Airways',
  QR: 'Qatar Airways',
  TK: 'Turkish Airlines',
  BA: 'British Airways',
  LH: 'Lufthansa',
  AF: 'Air France',
};

function transformPayload(input: ClientSearchPayload): UpstreamSearchPayload {
  return {
    origin:        input.from?.toUpperCase() ?? '',
    destination:   input.to?.toUpperCase() ?? '',
    departureDate: input.dep,
    returnDate:    input.ret ?? null,
    adults:        Number(input.adults ?? 1),
    children:      Number(input.children ?? 0),
    infants:       Number(input.infantsLap ?? 0) + Number(input.infantsSeat ?? 0),
    travelClass:   CABIN_MAP[(input.cabin ?? 'economy').toLowerCase()] ?? 'ECONOMY',
    nonStop:       Boolean(input.nonStop ?? false),
    currencyCode:  input.currencyCode ?? 'SAR',
    maxResults:    Number(input.maxResults ?? 50),
  };
}

function transformOffer(o: UnifiedFlightOffer): FlightOffer {
  const surcharges = o.price.surcharges.reduce((sum, s) => sum + s.amount, 0);
  const taxes      = o.price.taxes.reduce((sum, t) => sum + t.amount, 0);
  // Prefer the priced gross from PricingService when available; otherwise
  // fall back to the raw aggregate so the UI still renders something.
  const total = o.price.grossSellingPrice ?? (o.price.base + surcharges + taxes);

  const carrierCode = o.segments[0]?.carrierCode ?? '??';
  const totalDuration = o.segments.reduce((sum, s) => sum + s.durationMinutes, 0);

  return {
    offerId: o.offerId,
    airline: {
      code: carrierCode,
      name: AIRLINE_NAMES[carrierCode] ?? carrierCode,
    },
    segments: o.segments.map((s) => ({
      from:         s.departureAirport,
      to:           s.arrivalAirport,
      departure:    s.departureAt,
      arrival:      s.arrivalAt,
      flightNumber: s.flightNumber,
      cabin:        REVERSE_CABIN_MAP[o.cabinClass] ?? 'economy',
    })),
    stops:           Math.max(0, o.segments.length - 1),
    durationMinutes: totalDuration,
    price: {
      base:     Math.round(o.price.base * 100) / 100,
      yqYr:     Math.round(surcharges * 100) / 100,
      taxes:    Math.round(taxes * 100) / 100,
      total:    Math.round(total * 100) / 100,
      currency: o.price.currency,
    },
    baggage:   null,
    expiresAt: o.expiresAt,
  };
}

export async function POST(request: NextRequest) {
  let upstreamBody: string;
  try {
    const raw = (await request.json()) as ClientSearchPayload;
    if (!raw.from || !raw.to || !raw.dep) {
      return NextResponse.json(
        { error: 'Missing required fields: from, to, dep.' },
        { status: 400 },
      );
    }
    upstreamBody = JSON.stringify(transformPayload(raw));
  } catch {
    return NextResponse.json({ error: 'Invalid JSON body.' }, { status: 400 });
  }

  const forwardAndTransform = async (upstreamResponse: Response) => {
    if (!upstreamResponse.ok) {
      const errorText = await upstreamResponse.text();
      return NextResponse.json(
        { error: errorText || 'Upstream error' },
        { status: upstreamResponse.status },
      );
    }
    const offers = (await upstreamResponse.json()) as UnifiedFlightOffer[];
    const transformed = offers.map(transformOffer);
    return NextResponse.json(transformed, {
      headers: { 'Cache-Control': 'no-store' },
    });
  };

  try {
    const upstream = await gatewayFetch('/api/b2c/search/flights', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: upstreamBody,
    });
    return await forwardAndTransform(upstream);
  } catch (err) {
    if (err instanceof UnauthenticatedError) {
      const gatewayUrl = process.env.GATEWAY_URL;
      if (!gatewayUrl) {
        return NextResponse.json({ error: 'GATEWAY_URL not set' }, { status: 503 });
      }
      const upstream = await fetch(`${gatewayUrl}/api/b2c/search/flights`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: upstreamBody,
        cache: 'no-store',
      });
      return await forwardAndTransform(upstream);
    }
    return NextResponse.json(
      { error: 'Upstream search failed.' },
      { status: 502 },
    );
  }
}
