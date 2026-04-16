// POST /api/car-bookings — thin pass-through to BookingService (Plan 04-04 / CARB-03).
//
// The detail page's "Book car" CTA hits this route; it forwards to
// `/car-bookings` on the gateway with the caller's Keycloak bearer
// (D-05). On 202 Accepted, the client-side button router.push-es to
// /checkout/details?ref=car-{id} where the shared checkout flow takes
// over. Structurally identical to /api/hotel-bookings.

import { NextRequest, NextResponse } from 'next/server';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

export async function POST(request: NextRequest) {
  const body = await request.text();

  try {
    const upstream = await gatewayFetch('/car-bookings', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body,
    });
    const payload = await upstream.text();
    // Gateway returns `{ bookingId, status }` via AcceptedAtAction — forward
    // the body verbatim so the client button can pluck bookingId and route
    // to /checkout/details?ref=car-{id}.
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
      { error: 'Upstream car booking failed.' },
      { status: 502 },
    );
  }
}
