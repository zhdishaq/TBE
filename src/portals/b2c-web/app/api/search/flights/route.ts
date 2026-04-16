// POST /api/search/flights — client → Next.js → gateway pass-through.
//
// The browser calls this route from `use-flight-search`. It uses
// `gatewayFetch` so the Keycloak access_token is attached server-side
// (D-05) — the browser never sees it.
//
// Anonymous browsing is allowed by CONTEXT; gatewayFetch throws
// UnauthenticatedError when no session is present, which we map to 401.
// For anonymous searches we fall back to a direct fetch without
// Authorization so the browse flow works pre-login.

import { NextRequest, NextResponse } from 'next/server';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';

export const runtime = 'nodejs';

export async function POST(request: NextRequest) {
  const body = await request.text();

  try {
    const upstream = await gatewayFetch('/search/flights', {
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
      // Anonymous fallback — hit the gateway without a token. The gateway
      // must allow /search/flights for anonymous callers (policy set in
      // the YARP config in Phase 1).
      const gatewayUrl = process.env.GATEWAY_URL;
      if (!gatewayUrl) {
        return NextResponse.json({ error: 'GATEWAY_URL not set' }, { status: 503 });
      }
      const upstream = await fetch(`${gatewayUrl}/search/flights`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body,
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
      { error: 'Upstream search failed.' },
      { status: 502 },
    );
  }
}
