// Plan 05-01 Task 2 — sub-agent deactivate route handler.
//
// T-05-01-05 (cross-tenant deactivation): the lib helper reads the
// target user's `agency_id` attribute first and throws `CrossTenantError`
// if it does not match the caller's session agency_id. This handler
// catches that error, logs a structured WARN line (grep-friendly for
// the SOC playbook), and returns HTTP 403 without echoing Keycloak
// internals.
//
// Pitfall 11 (Next.js 16): dynamic route `params` is a Promise and MUST
// be awaited. Forgetting to await yields `undefined` on `.id` and the
// helper goes on to call Keycloak with the literal string 'undefined'.
//
// Runtime: Node only (same reason as POST /api/agents).

import { auth } from '@/lib/auth';
import {
  deactivateUser,
  CrossTenantError,
} from '@/lib/keycloak-b2b-admin';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

/**
 * PATCH /api/agents/[id]/deactivate
 *
 * 403 — no session, non-admin caller, or cross-tenant attempt
 * 502 — Keycloak error (sanitised)
 * 204 — deactivation succeeded
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
    await deactivateUser({
      callerAgencyId: session.user.agency_id,
      userId: id,
    });
    // eslint-disable-next-line no-console
    console.info(
      `AGT-DEACTIVATE agency=${session.user.agency_id} user=${id} by=${session.user.id ?? 'unknown'}`,
    );
  } catch (err) {
    if (err instanceof CrossTenantError) {
      // T-05-01-05 structured warn. `err.message` is the sanitised
      // "caller=… target_owner=… user=…" line the lib assembles —
      // no user input beyond the ids it already holds.
      // eslint-disable-next-line no-console
      console.warn(err.message);
      return new Response(null, { status: 403 });
    }
    // eslint-disable-next-line no-console
    console.error('[agents.deactivate] 502', err);
    return new Response(null, { status: 502 });
  }
  return new Response(null, { status: 204 });
}
