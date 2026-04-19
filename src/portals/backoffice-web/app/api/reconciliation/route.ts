// Plan 06-02 Task 3 (BO-06) — reconciliation list proxy (Node runtime).
//
// GET /api/reconciliation?status=...&discrepancyType=...&severity=...
//   → PaymentService /api/backoffice/reconciliation
//
// Authoritative RBAC lives on the backend ReconciliationController
// under the "Backoffice" JWT scheme. Any ops-* role can read; resolve
// is gated separately on the [id]/resolve handler.

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
  const upstreamPath = `/api/backoffice/reconciliation${incoming.search}`;

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
