// GET /api/hotels/search — client → Next.js → gateway pass-through (HOTB-01).
//
// The browser calls this route from `use-hotel-search`. It uses
// `gatewayFetch` so the Keycloak access_token is attached server-side
// (D-05). For anonymous searches we fall back to a no-Authorization
// fetch so the browse flow works pre-login (same contract as the
// flight search pass-through in `/api/search/flights`).

import { NextRequest, NextResponse } from 'next/server';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

export async function GET(request: NextRequest) {
  const { searchParams } = new URL(request.url);
  const forward = searchParams.toString();

  try {
    const upstream = await gatewayFetch(`/hotels/search?${forward}`);
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
      const gatewayUrl = process.env.GATEWAY_URL;
      if (!gatewayUrl) {
        return NextResponse.json({ error: 'GATEWAY_URL not set' }, { status: 503 });
      }
      const upstream = await fetch(`${gatewayUrl}/hotels/search?${forward}`, {
        method: 'GET',
        headers: { Accept: 'application/json' },
        cache: 'no-store',
      });
      const payload = await upstream.text();
      return new NextResponse(payload, {
        status: upstream.status,
        headers: {
          'Content-Type': upstream.headers.get('Content-Type') ?? 'application/json',
          'Cache-Control': 'no-store',
        },
      });
    }
    return NextResponse.json(
      { error: 'Upstream hotel search failed.' },
      { status: 502 },
    );
  }
}
