// Plan 05-05 Task 2 — POST /api/wallet/top-up/intent.
//
// Mirrors the /api/wallet/balance route handler shipped in 05-02 with an
// additional admin-role gate. Bearer is attached server-side via
// gatewayFetch (never read by the browser). Body-supplied `agencyId` is
// stripped before forwarding (defence-in-depth behind the backend's
// Pitfall 28 claim-only guard). Upstream problem+json is streamed through
// with the Content-Type header preserved so the top-up form can parse
// `allowedRange.min/max/currency` and render the D-40 cap error inline.

import { auth } from '@/lib/auth';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

export async function POST(req: Request): Promise<Response> {
  const session = await auth();
  const agencyId = (session?.user as { agency_id?: string } | undefined)
    ?.agency_id;
  if (!session || !agencyId) {
    return new Response(null, { status: 403 });
  }
  // B2BAdminPolicy parity — backend is authoritative, portal gates for UX.
  const roles = (session as { roles?: string[] } | undefined)?.roles ?? [];
  if (!roles.includes('agent-admin')) {
    return new Response(null, { status: 403 });
  }

  // Read + sanitise body (strip any client-supplied agencyId).
  let raw: Record<string, unknown> = {};
  try {
    const parsed = (await req.json()) as unknown;
    if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
      raw = { ...(parsed as Record<string, unknown>) };
    }
  } catch {
    raw = {};
  }
  delete raw.agencyId;
  delete raw.AgencyId;

  try {
    const upstream = await gatewayFetch('/api/b2b/wallet/top-up/intent', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify(raw),
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
