// Plan 05-04 Task 3 — GET /api/bookings/[id]/invoice.pdf proxy.
//
// Streams the agency-invoice PDF from BookingService (D-43 GROSS-only).
//
// Pitfall 11 — MUST pass `upstream.body` (a ReadableStream) into the
// Response constructor directly. Calling `await upstream.blob()` or
// `await upstream.arrayBuffer()` buffers the whole PDF in memory and
// blocks the event loop for multi-MB files.
// Pitfall 14 — `params` is a Promise.

import { auth } from '@/lib/auth';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

export async function GET(
  _req: Request,
  { params }: { params: Promise<{ id: string }> },
) {
  const session = await auth();
  const agencyId = (session?.user as { agency_id?: string } | undefined)?.agency_id;
  if (!session || !agencyId) {
    return new Response(null, { status: 403 });
  }
  const { id } = await params; // Pitfall 14
  try {
    const upstream = await gatewayFetch(`/api/b2b/invoices/${id}.pdf`);
    if (!upstream.ok) return new Response(null, { status: upstream.status });
    // Pitfall 11 — stream-through, do NOT buffer.
    return new Response(upstream.body, {
      status: 200,
      headers: {
        'Content-Type': 'application/pdf',
        'Content-Disposition':
          upstream.headers.get('Content-Disposition') ??
          `inline; filename="invoice-${id}.pdf"`,
      },
    });
  } catch (err) {
    if (err instanceof UnauthenticatedError) {
      return new Response(null, { status: 401 });
    }
    return new Response(null, { status: 502 });
  }
}
