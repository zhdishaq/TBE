// Plan 05-04 Task 3 — /api/bookings GET proxy.
//
// Thin server-proxy in front of the BookingService /api/b2b/agent/bookings
// list endpoint. Forwards the incoming query-string (page, size, client,
// pnr, status, from, to) and injects the caller's access token via
// gatewayFetch. 403 when the session has no agency_id (D-33 / Pitfall 26).

import { NextRequest } from 'next/server';
import { auth } from '@/lib/auth';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

export async function GET(req: NextRequest) {
  const session = await auth();
  const agencyId = (session?.user as { agency_id?: string } | undefined)?.agency_id;
  if (!session || !agencyId) {
    return new Response(null, { status: 403 });
  }
  const qs = req.nextUrl.searchParams.toString();
  try {
    const r = await gatewayFetch(
      qs ? `/api/b2b/agent/bookings?${qs}` : '/api/b2b/agent/bookings',
    );
    if (!r.ok) return new Response(null, { status: r.status });
    const body = await r.json();
    return Response.json(body);
  } catch (err) {
    if (err instanceof UnauthenticatedError) {
      return new Response(null, { status: 401 });
    }
    return new Response(null, { status: 502 });
  }
}
