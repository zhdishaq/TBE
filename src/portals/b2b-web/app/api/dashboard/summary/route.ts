// Plan 05-04 Task 4 — /api/dashboard/summary.
//
// Thin RSC server-proxy that feeds the portal's dashboard cards (TTL alerts,
// wallet summary, recent bookings). Same fail-closed posture as the wallet
// balance route (Pitfall 26 / D-33):
//   - 403 when there is no session OR no `agency_id` claim
//   - 401 when the upstream call rejects the access token (gatewayFetch throws)
//   - 502 on any other upstream/IO error — UI falls back to empty state
//
// The backend endpoint `/api/b2b/dashboard/summary` is agency-scoped via the
// access-token's agency_id claim (D-34). We never pass an agency_id query
// parameter from the browser — that would be trivially spoofable.

import { auth } from '@/lib/auth';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

export async function GET() {
  const session = await auth();
  const agencyId = (session?.user as { agency_id?: string } | undefined)?.agency_id;
  if (!session || !agencyId) {
    return new Response(null, { status: 403 });
  }
  try {
    const r = await gatewayFetch('/api/b2b/dashboard/summary');
    if (!r.ok) {
      return new Response(null, { status: r.status });
    }
    const body = await r.json();
    return Response.json(body);
  } catch (err) {
    if (err instanceof UnauthenticatedError) {
      return new Response(null, { status: 401 });
    }
    return new Response(null, { status: 502 });
  }
}
