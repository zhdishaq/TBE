// Plan 06-01 Task 4 — BO-10 DLQ requeue proxy (Node runtime).
//
// ops-admin only. The BackofficeService DlqController enforces this via
// [Authorize(Policy = "BackofficeAdminPolicy")] — we pre-check at the
// portal layer so we can return 403 without a round-trip and so route-
// handler bypass of middleware still fails closed.
//
// Pitfall 28: upstream fail-closes with 401 ProblemDetails when the
// preferred_username claim is missing from the token. We stream the
// upstream body through so the dialog can render that message.

import { auth } from '@/lib/auth';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';
import { isOpsAdmin } from '@/lib/rbac';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

export async function POST(
  _req: Request,
  ctx: { params: Promise<{ id: string }> },
): Promise<Response> {
  const session = await auth();
  if (!session) return new Response(null, { status: 401 });
  if (!isOpsAdmin(session)) return new Response(null, { status: 403 });

  const { id } = await ctx.params;
  // Guid shape guard — cheap defence against path injection.
  if (!/^[0-9a-f-]{32,36}$/i.test(id)) {
    return new Response(null, { status: 400 });
  }

  try {
    const upstream = await gatewayFetch(
      `/api/backoffice/dlq/${id}/requeue`,
      { method: 'POST' },
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
