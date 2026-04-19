// Plan 06-01 Task 7 (BO-01) — unified booking list proxy (Node runtime).
//
// GET /api/bookings?channel=&q=&from=&to=&page=&pageSize=
//   → BackofficeService GET /api/backoffice/bookings
//
// Authoritative RBAC + channel-parse + paging live on the backend. All
// four ops-* roles can read (BackofficeReadPolicy); this proxy merely
// forwards the query string with the Bearer token attached.

import { auth } from '@/lib/auth';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';
import { isOpsRead } from '@/lib/rbac';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

export async function GET(req: Request): Promise<Response> {
  const session = await auth();
  if (!session) return new Response(null, { status: 401 });
  if (!isOpsRead(session)) return new Response(null, { status: 403 });

  const url = new URL(req.url);
  const qs = url.search; // includes leading "?" or "" when empty

  try {
    const upstream = await gatewayFetch(
      `/api/backoffice/bookings${qs}`,
      { method: 'GET' },
    );
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
