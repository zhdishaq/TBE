// Plan 06-01 Task 6 — D-39 wallet-credit approve proxy (Node runtime).
//
// POST /api/wallet-credits/{id}/approve  body { approvalNotes }
//   → BackofficeService /api/backoffice/wallet-credits/{id}/approve.
//
// Authoritative role gate: BackofficeAdminPolicy. Self-approval is a
// backend concern (compares preferred_username claim vs RequestedBy on
// the WalletCreditRequests row). Client surfaces the 403 inline.

import { auth } from '@/lib/auth';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';
import { isOpsAdmin } from '@/lib/rbac';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

export async function POST(
  req: Request,
  context: { params: Promise<{ id: string }> },
): Promise<Response> {
  const session = await auth();
  if (!session) return new Response(null, { status: 401 });
  if (!isOpsAdmin(session)) return new Response(null, { status: 403 });

  const { id } = await context.params;
  const body = await req.text();

  try {
    const upstream = await gatewayFetch(
      `/api/backoffice/wallet-credits/${encodeURIComponent(id)}/approve`,
      {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body,
      },
    );
    return new Response(upstream.body, {
      status: upstream.status,
      headers: {
        'content-type': upstream.headers.get('content-type') ?? 'application/json',
      },
    });
  } catch (err) {
    if (err instanceof UnauthenticatedError) {
      return new Response(null, { status: 401 });
    }
    return new Response(null, { status: 502 });
  }
}
