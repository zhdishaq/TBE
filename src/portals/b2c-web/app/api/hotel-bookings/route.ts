// POST /api/hotel-bookings — thin pass-through to BookingService (HOTB-03).
//
// The detail page's "Book room" CTA hits this route; it forwards to
// `/hotel-bookings` on the gateway with the caller's Keycloak bearer
// (D-05). On 202 Accepted, the client-side button router.push-es to
// /checkout/details?hotelBookingId={id} where the shared 04-02 checkout
// flow takes over.

import { NextRequest, NextResponse } from 'next/server';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

export async function POST(request: NextRequest) {
  const body = await request.text();

  try {
    const upstream = await gatewayFetch('/hotel-bookings', {
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
    return NextResponse.json(
      { error: 'Upstream hotel booking failed.' },
      { status: 502 },
    );
  }
}
