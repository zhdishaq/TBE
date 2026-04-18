// Plan 05-04 Task 3 — GET /api/bookings/[id]/e-ticket.pdf proxy.
//
// Streams the ticketed e-ticket PDF from BookingService. Same Pitfall
// 11 + Pitfall 14 guidance as the invoice route. Backend returns 404
// when the booking hasn't been ticketed yet (no TicketNumber) — we
// propagate that status so the DocumentsPanel can render a "not
// available" state.

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
    const upstream = await gatewayFetch(`/api/b2b/tickets/${id}.pdf`);
    if (!upstream.ok) return new Response(null, { status: upstream.status });
    return new Response(upstream.body, {
      status: 200,
      headers: {
        'Content-Type': 'application/pdf',
        'Content-Disposition':
          upstream.headers.get('Content-Disposition') ??
          `inline; filename="e-ticket-${id}.pdf"`,
      },
    });
  } catch (err) {
    if (err instanceof UnauthenticatedError) {
      return new Response(null, { status: 401 });
    }
    return new Response(null, { status: 502 });
  }
}
