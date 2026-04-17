// Plan 05-01 Task 2 — sub-agent reactivate route handler.
//
// Mirror of /api/agents/[id]/deactivate/route.ts with `reactivateUser`
// (flips enabled=true). The same T-05-01-05 cross-tenant guard applies
// because the lib helper shares its internals with deactivateUser.

import { auth } from '@/lib/auth';
import {
  reactivateUser,
  CrossTenantError,
} from '@/lib/keycloak-b2b-admin';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

/**
 * PATCH /api/agents/[id]/reactivate
 *
 * 403 — no session, non-admin caller, or cross-tenant attempt
 * 502 — Keycloak error (sanitised)
 * 204 — reactivation succeeded
 */
export async function PATCH(
  _req: Request,
  { params }: { params: Promise<{ id: string }> },
): Promise<Response> {
  const session = await auth();
  if (
    !session?.user?.agency_id ||
    !session.roles?.includes('agent-admin')
  ) {
    return new Response(null, { status: 403 });
  }
  const { id } = await params; // Pitfall 11 — always await
  try {
    await reactivateUser({
      callerAgencyId: session.user.agency_id,
      userId: id,
    });
    // eslint-disable-next-line no-console
    console.info(
      `AGT-REACTIVATE agency=${session.user.agency_id} user=${id} by=${session.user.id ?? 'unknown'}`,
    );
  } catch (err) {
    if (err instanceof CrossTenantError) {
      // eslint-disable-next-line no-console
      console.warn(err.message);
      return new Response(null, { status: 403 });
    }
    // eslint-disable-next-line no-console
    console.error('[agents.reactivate] 502', err);
    return new Response(null, { status: 502 });
  }
  return new Response(null, { status: 204 });
}
