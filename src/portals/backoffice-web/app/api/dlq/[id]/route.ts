// Plan 06-01 Task 4 — BO-09 DLQ detail proxy (Node runtime).
//
// ops-read suffices for GET (BackofficeReadPolicy upstream). The
// envelope dialog in dlq-table.tsx calls this route to pull the full
// Payload text for the JSON viewer.

import { auth } from '@/lib/auth';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';
import { isOpsRead } from '@/lib/rbac';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

export async function GET(
  _req: Request,
  ctx: { params: Promise<{ id: string }> },
): Promise<Response> {
  const session = await auth();
  if (!session) return new Response(null, { status: 401 });
  if (!isOpsRead(session)) return new Response(null, { status: 403 });

  const { id } = await ctx.params;
  if (!/^[0-9a-f-]{32,36}$/i.test(id)) {
    return new Response(null, { status: 400 });
  }

  try {
    const upstream = await gatewayFetch(`/api/backoffice/dlq/${id}`, {
      method: 'GET',
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
