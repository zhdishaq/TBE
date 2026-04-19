// Plan 06-01 Task 4 — BO-10 DLQ resolve proxy (Node runtime).
//
// ops-admin only. Body is forwarded verbatim (a single-field
// { reason: string } envelope) — the upstream DlqController enforces the
// 1..500 char length bound via [StringLength(500, MinimumLength = 1)]
// and emits ValidationProblemDetails if the reason is out of range.

import { auth } from '@/lib/auth';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';
import { isOpsAdmin } from '@/lib/rbac';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

export async function POST(
  req: Request,
  ctx: { params: Promise<{ id: string }> },
): Promise<Response> {
  const session = await auth();
  if (!session) return new Response(null, { status: 401 });
  if (!isOpsAdmin(session)) return new Response(null, { status: 403 });

  const { id } = await ctx.params;
  if (!/^[0-9a-f-]{32,36}$/i.test(id)) {
    return new Response(null, { status: 400 });
  }

  let raw: Record<string, unknown> = {};
  try {
    const parsed = (await req.json()) as unknown;
    if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
      raw = parsed as Record<string, unknown>;
    }
  } catch {
    raw = {};
  }

  try {
    const upstream = await gatewayFetch(
      `/api/backoffice/dlq/${id}/resolve`,
      {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify(raw),
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
