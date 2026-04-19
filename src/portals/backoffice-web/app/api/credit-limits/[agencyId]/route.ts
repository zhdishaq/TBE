// Plan 06-04 Task 2 / CRM-02 / D-61 — credit-limit PATCH proxy (Node runtime).
//
// PATCH /api/credit-limits/{agencyId}
//   → gateway /api/backoffice/payments/agencies/{agencyId}/credit-limit
//   → PaymentService AgencyCreditLimitController PATCH
//
// Authoritative RBAC lives on the backend controller under the
// "Backoffice" JWT scheme + BackofficeFinancePolicy. The Next.js
// session layer short-circuits non-finance roles with 403 so we don't
// round-trip an obviously-unauthorised request.

import { auth } from '@/lib/auth';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';
import { hasAnyRole } from '@/lib/rbac';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

export async function PATCH(
  req: Request,
  { params }: { params: Promise<{ agencyId: string }> },
): Promise<Response> {
  const session = await auth();
  if (!session) return new Response(null, { status: 401 });
  if (!hasAnyRole(session, ['ops-finance', 'ops-admin'])) {
    return new Response(null, { status: 403 });
  }

  const { agencyId } = await params;
  // Let the server be the authority on body shape; we forward it as-is.
  const body = await req.text();

  const upstreamPath = `/api/backoffice/payments/agencies/${encodeURIComponent(agencyId)}/credit-limit`;

  try {
    const upstream = await gatewayFetch(upstreamPath, {
      method: 'PATCH',
      headers: { 'content-type': 'application/json' },
      body,
    });
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
