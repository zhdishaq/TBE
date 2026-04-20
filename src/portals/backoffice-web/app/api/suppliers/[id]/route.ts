// Plan 06-02 Task 2 (BO-07) — supplier contract detail / update / delete
// proxy (Node runtime).
//
// GET    /api/suppliers/[id]   (any ops-read|cs|finance|admin)
// PUT    /api/suppliers/[id]   (ops-finance + ops-admin)
// DELETE /api/suppliers/[id]   (ops-finance + ops-admin)  (soft-delete)

import { auth } from '@/lib/auth';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';
import { hasAnyRole, isOpsRead } from '@/lib/rbac';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

export async function GET(
  _req: Request,
  context: { params: Promise<{ id: string }> },
): Promise<Response> {
  const session = await auth();
  if (!session) return new Response(null, { status: 401 });
  if (!isOpsRead(session)) return new Response(null, { status: 403 });

  const { id } = await context.params;

  try {
    const upstream = await gatewayFetch(
      `/api/backoffice/supplier-contracts/${encodeURIComponent(id)}`,
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

export async function PUT(
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
      `/api/backoffice/supplier-contracts/${encodeURIComponent(id)}`,
      {
        method: 'PUT',
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

export async function DELETE(
  _req: Request,
  context: { params: Promise<{ id: string }> },
): Promise<Response> {
  const session = await auth();
  if (!session) return new Response(null, { status: 401 });
  if (!hasAnyRole(session, ['ops-finance', 'ops-admin'])) {
    return new Response(null, { status: 403 });
  }

  const { id } = await context.params;

  try {
    const upstream = await gatewayFetch(
      `/api/backoffice/supplier-contracts/${encodeURIComponent(id)}`,
      { method: 'DELETE' },
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
