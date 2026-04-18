// Plan 05-05 Task 2 — PUT /api/wallet/threshold.
//
// T-05-05-03 IDOR mitigation (portal layer): body-supplied `agencyId` is
// structurally stripped before forwarding. Pitfall 28 backend guard is
// authoritative — the server reads `agency_id` from the JWT claim only.
// Problem+json 400 (below-£50 / above-£10 000) is streamed through with
// Content-Type preserved so the threshold dialog can render the range
// error inline.

import { auth } from '@/lib/auth';
import { gatewayFetch, UnauthenticatedError } from '@/lib/api-client';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

export async function PUT(req: Request): Promise<Response> {
  const session = await auth();
  const agencyId = (session?.user as { agency_id?: string } | undefined)
    ?.agency_id;
  if (!session || !agencyId) {
    return new Response(null, { status: 403 });
  }
  const roles = (session as { roles?: string[] } | undefined)?.roles ?? [];
  if (!roles.includes('agent-admin')) {
    return new Response(null, { status: 403 });
  }

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
    const upstream = await gatewayFetch('/api/b2b/wallet/threshold', {
      method: 'PUT',
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
