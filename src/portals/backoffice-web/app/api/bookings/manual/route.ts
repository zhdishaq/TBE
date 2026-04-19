// Plan 06-02 Task 1 (BO-02) — manual booking entry proxy (Node runtime).
//
// POST /api/bookings/manual
//   → BookingService (via gateway) POST /api/backoffice/bookings/manual
//
// Authoritative RBAC + amount validation + supplier-reference dedup
// live on the BookingService backend under BackofficeCsPolicy. This
// proxy gates ops-cs + ops-admin at the portal edge as defence-in-
// depth and forwards the Bearer token.

import { auth } from '@/lib/auth';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';
import { isOpsAdmin, isOpsCs } from '@/lib/rbac';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

export async function POST(req: Request): Promise<Response> {
  const session = await auth();
  if (!session) return new Response(null, { status: 401 });
  if (!isOpsCs(session) && !isOpsAdmin(session)) {
    return new Response(null, { status: 403 });
  }

  // Read the wizard payload and forward verbatim — Channel + Status are
  // never in this body (Pitfall 28). The backend DTO silently drops any
  // extra keys that try to set them.
  const body = await req.text();

  try {
    const upstream = await gatewayFetch('/api/backoffice/bookings/manual', {
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
