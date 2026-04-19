// Plan 06-01 Task 4 — BO-09 DLQ list proxy (Node runtime).
//
// Thin reverse-proxy to the gateway — carries the caller's Keycloak
// access token in the Authorization header via gatewayFetch. Roles are
// authoritatively enforced at the BackofficeService DlqController
// [Authorize(Policy = "BackofficeReadPolicy")] — we additionally gate
// at the portal layer to fail-fast for middleware-bypass scenarios
// (direct route-handler invocation with a stale session).
//
// Upstream stream is re-wrapped via `new Response(upstream.body, …)` so
// large envelopes (up to 1 MB per row, 20 rows/page) don't buffer into
// Node memory (Pitfall 11 + 14).

import { auth } from '@/lib/auth';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';
import { isOpsRead } from '@/lib/rbac';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

export async function GET(req: Request): Promise<Response> {
  const session = await auth();
  if (!session) return new Response(null, { status: 401 });
  if (!isOpsRead(session)) return new Response(null, { status: 403 });

  const incoming = new URL(req.url);
  // Forward the query string verbatim (status / from / to / page / pageSize).
  const upstreamPath = `/api/backoffice/dlq${incoming.search}`;

  try {
    const upstream = await gatewayFetch(upstreamPath, { method: 'GET' });
    return new Response(upstream.body, {
      status: upstream.status,
      headers: {
        'content-type':
          upstream.headers.get('content-type') ?? 'application/json',
      },
    });
  } catch (err) {
    if (err instanceof UnauthenticatedError) {
      return new Response(null, { status: 401 });
    }
    return new Response(null, { status: 502 });
  }
}
