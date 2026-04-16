// Voucher pass-through route (HOTB-04, D-16, Pitfall 14).
//
// Forwards GET /api/hotels/{id}/voucher.pdf to BookingService's
// `/hotel-bookings/{id}/voucher.pdf`, which in turn pipes through
// NotificationService (D-16 — single source of truth for the PDF).
//
// Pitfall 11: `params` is a Promise in Next.js 16 — await before use.
// Pitfall 14: stream the upstream body via `new Response(upstream.body, …)`.
//             Never `await upstream.arrayBuffer()` here — a multi-MB PDF
//             would otherwise buffer in Node memory per request.
//
// T-04-03-08 (Spoofing): `gatewayFetch` is server-only and injects the
// session's Bearer from Auth.js — we NEVER read a token from the query
// string or request body.

import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

interface RouteContext {
  params: Promise<{ offerId: string }>;
}

// URL label says "offerId" to line up with the file-system segment of the
// app/hotels/[offerId] hierarchy, but callers pass the *booking id* once
// the customer has a confirmed booking — there is no per-offer voucher.
export async function GET(_request: Request, { params }: RouteContext) {
  const { offerId: hotelBookingId } = await params;

  let upstream: Response;
  try {
    upstream = await gatewayFetch(
      `/hotel-bookings/${encodeURIComponent(hotelBookingId)}/voucher.pdf`,
    );
  } catch (err) {
    if (err instanceof UnauthenticatedError) {
      return new Response(null, { status: 401 });
    }
    throw err;
  }

  if (!upstream.ok) {
    return new Response(null, { status: upstream.status });
  }

  // Pitfall 14 — stream through, never buffer.
  return new Response(upstream.body, {
    headers: {
      'content-type':
        upstream.headers.get('content-type') ?? 'application/pdf',
      'content-disposition':
        upstream.headers.get('content-disposition') ??
        `attachment; filename="voucher-${hotelBookingId}.pdf"`,
    },
  });
}
