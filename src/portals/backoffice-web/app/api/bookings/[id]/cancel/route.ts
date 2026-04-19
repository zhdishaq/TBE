// Plan 06-01 Task 6 — BO-03 open-cancellation proxy (Node runtime).
//
// POST /api/bookings/{id}/cancel  body { reasonCode, reason }
//   → forwards to BackofficeService /api/backoffice/bookings/{id}/cancel.
//
// Authoritative role gate: BackofficeService StaffBookingActionsController
// [Authorize(Policy = "BackofficeCsPolicy")]. This handler is a thin
// reverse-proxy over gatewayFetch — same pattern as DLQ route handler.

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
  if (!hasAnyRole(session, ['ops-cs', 'ops-admin'])) {
    return new Response(null, { status: 403 });
  }

  const { id } = await context.params;
  const body = await req.text();

  try {
    const upstream = await gatewayFetch(
      `/api/backoffice/bookings/${encodeURIComponent(id)}/cancel`,
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
