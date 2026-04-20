// Plan 06-04 Task 3 (COMP-03 / D-57) — GDPR erasure proxy (Node runtime).
//
// POST /api/crm/customers/{id}/erase  body { reason, typedEmail }
//   → BackofficeService /api/backoffice/customers/{id}/erase.
//
// Authoritative role gate: BackofficeAdminPolicy on the backend.
// Client-side check is best-effort (role-gate UI + defence in depth);
// the 202/4xx/5xx response body is forwarded verbatim so the dialog
// can surface the RFC-7807 problem+json detail inline.

import { auth } from '@/lib/auth';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';
import { isOpsAdmin } from '@/lib/rbac';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

export async function POST(
  req: Request,
  _context: { params: Promise<{ id: string }> },
): Promise<Response> {
  const session = await auth();
  if (!session) return new Response(null, { status: 401 });
  if (!isOpsAdmin(session)) return new Response(null, { status: 403 });

  const { id } = await _context.params;

  const parsed = (await req.json().catch(() => null)) as
    | { reason?: string; typedEmail?: string }
    | null;
  const reason = parsed?.reason?.trim() ?? '';
  const typedEmail = parsed?.typedEmail?.trim() ?? '';
  if (!reason || !typedEmail) {
    return new Response(
      JSON.stringify({
        type: '/errors/validation',
        title: 'validation_failed',
        detail: 'reason and typedEmail are required',
      }),
      {
        status: 400,
        headers: { 'content-type': 'application/problem+json' },
      },
    );
  }

  try {
    const upstream = await gatewayFetch(
      `/api/backoffice/customers/${encodeURIComponent(id)}/erase`,
      {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify({ reason, typedEmail }),
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
