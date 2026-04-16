/**
 * Public IATA typeahead pass-through (CONTEXT D-18, T-04-02-04).
 *
 * The upstream `/airports` endpoint on SearchService is anonymous and
 * rate-limited to 60 req/min/IP. This route proxies through the gateway
 * without attaching an `Authorization` header — we do NOT call
 * `gatewayFetch` here because the endpoint is public and adding a Bearer
 * would leak a user token to any caller who can poke at /api/airports.
 *
 * The response is cacheable at the CDN edge for 60s; the plan's
 * acceptance criterion is that identical prefixes within 60s cost the
 * upstream zero hits.
 */

import { NextRequest, NextResponse } from 'next/server';

export const runtime = 'nodejs';

const MIN_LEN = 2;
const MAX_LEN = 8;
const MAX_LIMIT = 20;

export async function GET(request: NextRequest) {
  const { searchParams } = new URL(request.url);
  const q = (searchParams.get('q') ?? '').trim();
  const limit = Number.parseInt(searchParams.get('limit') ?? '10', 10);

  // Mirror the upstream validation so we can reject abuse at the edge without
  // consuming the upstream rate-limit budget.
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
  const safeLimit = Math.min(Math.max(Number.isFinite(limit) ? limit : 10, 1), MAX_LIMIT);

  const gatewayUrl = process.env.GATEWAY_URL;
  if (!gatewayUrl) {
    return NextResponse.json(
      { error: 'GATEWAY_URL not configured.' },
      { status: 503 },
    );
  }

  const upstreamUrl =
    `${gatewayUrl}/airports?q=${encodeURIComponent(q)}&limit=${safeLimit}`;

  // `next: { revalidate: 60 }` opts the server-side fetch into Next's
  // request-deduped data cache. Combined with the s-maxage header below we
  // get both first-party (ISR) and third-party (CDN) caching for 60s.
  const upstream = await fetch(upstreamUrl, {
    method: 'GET',
    headers: { Accept: 'application/json' },
    next: { revalidate: 60 },
  });

  if (!upstream.ok) {
    return NextResponse.json(
      { error: 'Upstream airport lookup failed.', status: upstream.status },
      { status: 502 },
    );
  }

  const body = await upstream.text();

  return new NextResponse(body, {
    status: 200,
    headers: {
      'Content-Type': 'application/json; charset=utf-8',
      'Cache-Control': 'public, s-maxage=60, stale-while-revalidate=300',
      // Preserve attribution from upstream if present, else inject it.
      'X-Data-Attribution':
        upstream.headers.get('X-Data-Attribution') ??
        'Airport data by OpenFlights (openflights.org), licensed under CC-BY-SA 3.0',
    },
  });
}
