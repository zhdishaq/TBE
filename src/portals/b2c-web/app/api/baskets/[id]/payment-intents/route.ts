// POST /api/baskets/[id]/payment-intents — thin pass-through (Plan 04-04 / D-08).
//
// Returns `{ clientSecret }` for the ONE combined PaymentIntent per
// basket. A `flightClientSecret`/`hotelClientSecret` pair is the
// forbidden two-PI shape.

import { NextRequest, NextResponse } from 'next/server';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

export async function POST(
  _request: NextRequest,
  context: { params: Promise<{ id: string }> },
) {
  const { id } = await context.params;
  try {
    const upstream = await gatewayFetch(
      `/baskets/${encodeURIComponent(id)}/payment-intents`,
      { method: 'POST' },
    );
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
    return NextResponse.json(
      { error: 'Upstream basket payment-intent init failed.' },
      { status: 502 },
    );
  }
}
