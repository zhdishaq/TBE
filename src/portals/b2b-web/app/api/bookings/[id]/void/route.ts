// Plan 05-04 Task 3 — POST /api/bookings/[id]/void proxy.
//
// Forwards to BookingService POST /api/b2b/agent/bookings/{id}/void under
// the B2BAdminPolicy. The backend enforces:
//   - Pitfall 28: 401 on missing agency_id claim
//   - B2BAdminPolicy: 403 for non-admin
//   - Pitfall 10: 404 on cross-tenant / unknown
//   - D-39: 409 on post-ticket (RFC 7807 problem+json)
//   - Pre-ticket success: 202 Accepted
//
// Pitfall 14 — params is a Promise.
// This proxy is intentionally permissive on status-code forwarding: it
// passes the backend's status (and RFC 7807 body for 409) through to the
// client so the UI's VoidBookingButton can branch on it.

import { auth } from '@/lib/auth';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

export async function POST(
  req: Request,
  { params }: { params: Promise<{ id: string }> },
) {
  const session = await auth();
  const agencyId = (session?.user as { agency_id?: string } | undefined)?.agency_id;
  if (!session || !agencyId || !session.roles?.includes('agent-admin')) {
    return new Response(null, { status: 403 });
  }
  const { id } = await params; // Pitfall 14
  let reason = 'portal-initiated void';
  try {
    const body = (await req.json()) as { reason?: string };
    if (typeof body?.reason === 'string' && body.reason.trim()) {
      reason = body.reason.trim();
    }
  } catch {
    // body is optional — default reason is fine.
  }
  try {
    const r = await gatewayFetch(`/api/b2b/agent/bookings/${id}/void`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ reason }),
    });
    // Forward status + body verbatim so the client can render RFC 7807 for 409.
    const contentType = r.headers.get('Content-Type') ?? 'application/json';
    const text = await r.text();
    return new Response(text || null, {
      status: r.status,
      headers: { 'Content-Type': contentType },
    });
  } catch (err) {
    if (err instanceof UnauthenticatedError) {
      return new Response(null, { status: 401 });
    }
    return new Response(null, { status: 502 });
  }
}
