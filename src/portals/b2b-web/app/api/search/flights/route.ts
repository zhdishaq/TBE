// POST /api/search/flights — B2B browser → Next.js → gateway pass-through.
//
// The B2B agent portal flight search route. Unlike B2C this route is
// strictly authenticated — agents must be logged in (D-32 / D-33) and
// the gateway enforces the B2BPolicy authorization policy on
// /api/b2b/search/flights.
//
// The agency_id claim is stamped server-side from the JWT and used by
// the pricing service to apply per-agency markup (Plan 05-02 T-05-02-01).

import { NextRequest, NextResponse } from 'next/server';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';

export const runtime = 'nodejs';

export async function POST(request: NextRequest) {
  const body = await request.text();

  try {
    const upstream = await gatewayFetch('/api/b2b/search/flights', {
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
      // B2B is strictly authenticated — no anonymous fallback (D-32).
      return NextResponse.json(
        { error: 'Authentication required.' },
        { status: 401 },
      );
    }
    return NextResponse.json(
      { error: 'Upstream search failed.' },
      { status: 502 },
    );
  }
}
