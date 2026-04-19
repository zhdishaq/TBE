// Plan 06-01 Task 6 — BO-03 approve-cancellation proxy (Node runtime).
//
// POST /api/bookings/{id}/cancel/approve  body { requestId, approvalReason }
//   → BackofficeService /api/backoffice/bookings/cancellations/{requestId}/approve.
//
// Authoritative role gate: BackofficeAdminPolicy on the backend. Self-
// approval is a backend concern (compares preferred_username claim on
// the token to the RequestedBy on the CancellationRequest row); the
// client dialog surfaces the 403 problem+json detail inline.

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

  // Body carries { requestId, approvalReason } — the backend route keys
  // on requestId (not bookingId) so we re-target the proxy URL at the
  // cancellations/{requestId}/approve endpoint.
  const parsed = (await req.json().catch(() => null)) as
    | { requestId?: string; approvalReason?: string }
    | null;
  const requestId = parsed?.requestId;
  const approvalReason = parsed?.approvalReason ?? '';
  if (!requestId) {
    return new Response(
      JSON.stringify({ error: 'requestId is required' }),
      { status: 400, headers: { 'content-type': 'application/json' } },
    );
  }

  try {
    const upstream = await gatewayFetch(
      `/api/backoffice/bookings/cancellations/${encodeURIComponent(requestId)}/approve`,
      {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify({ approvalReason }),
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
