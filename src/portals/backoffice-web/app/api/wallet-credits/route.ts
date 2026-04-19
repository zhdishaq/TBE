// Plan 06-01 Task 6 — D-39 wallet-credits list + open proxy (Node runtime).
//
// GET  /api/wallet-credits?status=...&page=...  (ops-finance + ops-admin)
// POST /api/wallet-credits                      (ops-finance + ops-admin)
//   body { agencyId, amount, currency, reasonCode, linkedBookingId?, notes }
//
// Authoritative role gate: WalletCreditRequestsController on the
// backend. Self-approval guard is NOT relevant here (open, not approve).
// Amount and ReasonCode validation happen at the backend with RFC-7807
// problem+json responses that the client dialog surfaces inline.

import { auth } from '@/lib/auth';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';
import { hasAnyRole } from '@/lib/rbac';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

export async function GET(req: Request): Promise<Response> {
  const session = await auth();
  if (!session) return new Response(null, { status: 401 });
  if (!hasAnyRole(session, ['ops-finance', 'ops-admin'])) {
    return new Response(null, { status: 403 });
  }

  const incoming = new URL(req.url);
  const upstreamPath = `/api/backoffice/wallet-credits${incoming.search}`;

  try {
    const upstream = await gatewayFetch(upstreamPath, { method: 'GET' });
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

export async function POST(req: Request): Promise<Response> {
  const session = await auth();
  if (!session) return new Response(null, { status: 401 });
  if (!hasAnyRole(session, ['ops-finance', 'ops-admin'])) {
    return new Response(null, { status: 403 });
  }

  const body = await req.text();

  try {
    const upstream = await gatewayFetch('/api/backoffice/wallet-credits', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body,
    });
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
