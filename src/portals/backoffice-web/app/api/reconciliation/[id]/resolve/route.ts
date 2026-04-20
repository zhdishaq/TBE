// Plan 06-02 Task 3 (BO-06) — reconciliation resolve proxy (Node runtime).
//
// POST /api/reconciliation/[id]/resolve   (ops-finance + ops-admin)
//   body { notes }
//   → PaymentService POST /api/backoffice/reconciliation/[id]/resolve

import { auth } from '@/lib/auth';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';
import { hasAnyRole } from '@/lib/rbac';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

export async function POST(
  req: Request,
  context: { params: Promise<{ id: string }> },
): Promise<Response> {
  const session = await auth();
  if (!session) return new Response(null, { status: 401 });
  if (!hasAnyRole(session, ['ops-finance', 'ops-admin'])) {
    return new Response(null, { status: 403 });
  }

  const { id } = await context.params;
  const body = await req.text();

  try {
    const upstream = await gatewayFetch(
      `/api/backoffice/reconciliation/${encodeURIComponent(id)}/resolve`,
      {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body,
      },
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
