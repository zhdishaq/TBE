// Plan 05-02 Task 3 — /api/wallet/balance.
//
// Node runtime so we can run `auth()` (NextAuth v5 server-side session). Reads
// the session, requires an `agency_id` claim (Pitfall 26 / D-33 single-valued),
// then forwards to the gateway route backed by Plan 04-02's wallet endpoint.
// 403 when the caller has no session or no agency_id claim (never leaks cross-
// tenant data — UI-SPEC §Header §Wallet Chip).

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
    const r = await gatewayFetch('/api/b2b/wallet/balance');
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
