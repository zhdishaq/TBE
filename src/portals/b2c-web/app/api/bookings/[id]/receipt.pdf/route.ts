// Receipt pass-through route (D-15, T-04-01-07).
//
// Forwards GET /api/bookings/{id}/receipt.pdf to BookingService. The
// gateway layer supplies the Bearer; this handler never sees the user's
// credentials.
//
// Pitfall 11: `params` is a Promise in Next.js 16 — await before use.
// Pitfall 14: stream the upstream body through `new Response(upstream.body…)`.
//             Never `await upstream.arrayBuffer()` here — a 10 MB PDF would
//             otherwise buffer in Node memory per request.
//
// T-04-01-07 mitigation (large PDFs buffered): enforced by the
// stream-through shape below.

import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

interface RouteContext {
  params: Promise<{ id: string }>;
}

export async function GET(_request: Request, { params }: RouteContext) {
  const { id } = await params;

  let upstream: Response;
  try {
    upstream = await gatewayFetch(
      `/bookings/${encodeURIComponent(id)}/receipt.pdf`,
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
        `attachment; filename="receipt-${id}.pdf"`,
    },
  });
}
