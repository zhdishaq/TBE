// Plan 06-02 Task 2 (BO-07) — supplier contracts list + create proxy
// (Node runtime).
//
// GET  /api/suppliers?productType=...&status=...&q=...&page=...
//      (any ops-read|cs|finance|admin)
// POST /api/suppliers
//      (ops-finance + ops-admin)
//
// Authoritative RBAC + validation lives on BackofficeService
// SupplierContractsController under the "Backoffice" JWT scheme.

import { auth } from '@/lib/auth';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';
import { hasAnyRole, isOpsRead } from '@/lib/rbac';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

export async function GET(req: Request): Promise<Response> {
  const session = await auth();
  if (!session) return new Response(null, { status: 401 });
  if (!isOpsRead(session)) return new Response(null, { status: 403 });

  const incoming = new URL(req.url);
  const upstreamPath = `/api/backoffice/supplier-contracts${incoming.search}`;

  try {
    const upstream = await gatewayFetch(upstreamPath, { method: 'GET' });
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

export async function POST(req: Request): Promise<Response> {
  const session = await auth();
  if (!session) return new Response(null, { status: 401 });
  if (!hasAnyRole(session, ['ops-finance', 'ops-admin'])) {
    return new Response(null, { status: 403 });
  }

  const body = await req.text();

  try {
    const upstream = await gatewayFetch('/api/backoffice/supplier-contracts', {
      method: 'POST',
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
