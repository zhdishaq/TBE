// GET /api/hotels/destinations — hotel destination typeahead pass-through.
//
// Mirror of `/api/airports` — the upstream `/hotels/destinations` on
// InventoryService is anonymous and rate-limited to 60 req/min/IP
// (T-04-03-07). We proxy without attaching an Authorization header —
// adding a Bearer would leak a user token to any caller who can poke at
// this endpoint.
//
// Response is cacheable at the CDN edge for 60s; identical prefixes
// within 60s cost the upstream zero hits.

import { NextRequest, NextResponse } from 'next/server';

export const runtime = 'nodejs';

const MIN_LEN = 2;
const MAX_LEN = 64;
const MAX_LIMIT = 20;

export async function GET(request: NextRequest) {
  const { searchParams } = new URL(request.url);
  const q = (searchParams.get('q') ?? '').trim();
  const limit = Number.parseInt(searchParams.get('limit') ?? '10', 10);

  if (q.length < MIN_LEN) {
    return NextResponse.json(
      { error: `Query must be at least ${MIN_LEN} characters.` },
      { status: 400 },
    );
  }
  if (q.length > MAX_LEN) {
    return NextResponse.json(
      { error: `Query must be at most ${MAX_LEN} characters.` },
      { status: 400 },
    );
  }
  const safeLimit = Math.min(
    Math.max(Number.isFinite(limit) ? limit : 10, 1),
    MAX_LIMIT,
  );

  const gatewayUrl = process.env.GATEWAY_URL;
  if (!gatewayUrl) {
    return NextResponse.json(
      { error: 'GATEWAY_URL not configured.' },
      { status: 503 },
    );
  }

  const upstreamUrl = `${gatewayUrl}/hotels/destinations?q=${encodeURIComponent(q)}&limit=${safeLimit}`;

  const upstream = await fetch(upstreamUrl, {
    method: 'GET',
    headers: { Accept: 'application/json' },
    next: { revalidate: 60 },
  });

  if (!upstream.ok) {
    return NextResponse.json(
      { error: 'Upstream destination lookup failed.', status: upstream.status },
      { status: 502 },
    );
  }

  const body = await upstream.text();

  return new NextResponse(body, {
    status: 200,
    headers: {
      'Content-Type': 'application/json; charset=utf-8',
      'Cache-Control': 'public, s-maxage=60, stale-while-revalidate=300',
    },
  });
}
