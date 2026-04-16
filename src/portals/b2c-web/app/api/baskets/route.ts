// POST /api/baskets — thin pass-through to BookingService (Plan 04-04 / PKG-01..04).
//
// The Trip Builder basket-footer "Continue to checkout" CTA calls
// `useBasket().createServerBasket()` which posts here; we forward to
// `/baskets` on the gateway with the caller's Keycloak bearer (D-05).
// On 202 Accepted the client branches to /checkout/details?ref=basket-{id}.

import { NextRequest, NextResponse } from 'next/server';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

export async function POST(request: NextRequest) {
  const body = await request.text();
  try {
    const upstream = await gatewayFetch('/baskets', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body,
    });
    const payload = await upstream.text();
    return new NextResponse(payload, {
      status: upstream.status,
      headers: {
        'Content-Type': upstream.headers.get('Content-Type') ?? 'application/json',
        'Cache-Control': 'no-store',
      },
    });
  } catch (err) {
    if (err instanceof UnauthenticatedError) {
      return new NextResponse(null, { status: 401 });
    }
    return NextResponse.json({ error: 'Upstream basket create failed.' }, { status: 502 });
  }
}
