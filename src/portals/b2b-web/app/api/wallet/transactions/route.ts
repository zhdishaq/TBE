// Plan 05-05 Task 2 — GET /api/wallet/transactions.
//
// D-44 page-number pagination: forwards `?page=&size=` query params
// verbatim (NOT cursor-based — page-number is the only mode). Default
// page=1 size=20 if the client omits them. Agent-admin only — the
// transactions ledger is a financial record the backend gates on
// B2BAdminPolicy; we mirror the gate here for UX consistency.

import { auth } from '@/lib/auth';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

export async function GET(req: Request): Promise<Response> {
  const session = await auth();
  const agencyId = (session?.user as { agency_id?: string } | undefined)
    ?.agency_id;
  if (!session || !agencyId) {
    return new Response(null, { status: 403 });
  }
  const roles = (session as { roles?: string[] } | undefined)?.roles ?? [];
  if (!roles.includes('agent-admin')) {
    return new Response(null, { status: 403 });
  }

  const { searchParams } = new URL(req.url);
  const page = searchParams.get('page') ?? '1';
  const size = searchParams.get('size') ?? '20';
  const target = `/api/b2b/wallet/transactions?page=${encodeURIComponent(page)}&size=${encodeURIComponent(size)}`;

  try {
    const upstream = await gatewayFetch(target);
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
