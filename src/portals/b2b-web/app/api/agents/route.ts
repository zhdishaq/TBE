// Plan 05-01 Task 2 — sub-agent CRUD route handler.
//
// Pitfall 28 (Phase 5 planning language): the `agency_id` tenant scope
// is read from the authenticated session (`session.user.agency_id`) and
// NEVER from the request body. The CreateBodySchema deliberately does
// not declare an `agency_id` field, and the handler passes
// `agencyId: session.user.agency_id` to createSubAgent. Any extra keys
// in the body (e.g. a forged `agency_id: 'OTHER-AGENCY'`) are dropped
// by zod's `.strip()` default before they can reach the lib layer —
// T-05-01-03 asserts this.
//
// T-05-01-06: the zod `role` enum literally omits `'agent-admin'`, so
// a caller can never assign the admin role through this endpoint.
//
// Runtime: Node. The Keycloak admin client uses in-process caching and
// Node `fetch` — Edge runtime must not be used.

import { auth } from '@/lib/auth';
import {
  createSubAgent,
  listAgencyUsers,
  DuplicateUserError,
} from '@/lib/keycloak-b2b-admin';
import { z } from 'zod';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

const CreateBodySchema = z.object({
  email: z.string().email(),
  firstName: z.string().min(1).max(100),
  lastName: z.string().min(1).max(100),
  role: z.enum(['agent', 'agent-readonly']), // T-05-01-06: agent-admin rejected
});

/**
 * POST /api/agents — Create a sub-agent under the caller's agency.
 *
 * 403 — no session OR caller is not `agent-admin`
 * 400 — body fails zod validation (missing field, bad email, or
 *       role='agent-admin' which the enum does not accept)
 * 409 — duplicate email inside Keycloak
 * 502 — any other Keycloak error (message is sanitised; see T-05-01-04)
 * 202 — accepted; verify-email is sent asynchronously
 */
export async function POST(req: Request): Promise<Response> {
  const session = await auth();
  if (
    !session?.user?.agency_id ||
    !session.roles?.includes('agent-admin')
  ) {
    return new Response(null, { status: 403 });
  }
  const body = await req.json().catch(() => null);
  const parsed = CreateBodySchema.safeParse(body);
  if (!parsed.success) {
    return Response.json(
      { error: 'invalid_body', details: parsed.error.flatten() },
      { status: 400 },
    );
  }
  try {
    await createSubAgent({
      agencyId: session.user.agency_id, // Pitfall 28 — session-derived, never body
      email: parsed.data.email,
      firstName: parsed.data.firstName,
      lastName: parsed.data.lastName,
      role: parsed.data.role,
    });
    // T-05-01-08 — structured audit line. `session.user.id` is the
    // Keycloak sub of the admin who issued the request.
    // eslint-disable-next-line no-console
    console.info(
      `AGT-CREATE agency=${session.user.agency_id} role=${parsed.data.role} by=${session.user.id ?? 'unknown'}`,
    );
  } catch (err) {
    if (err instanceof DuplicateUserError) {
      return new Response(null, { status: 409 });
    }
    // eslint-disable-next-line no-console
    console.error('[agents.create] 502', err);
    return new Response(null, { status: 502 });
  }
  return new Response(null, { status: 202 });
}

/**
 * GET /api/agents — List sub-agents under the caller's agency.
 *
 * 403 — no session OR caller is not `agent-admin`
 * 200 — `{ items: AgencyUser[] }`
 *
 * Used by the client-side sub-agent list to refetch after create /
 * deactivate success. The RSC /admin/agents page calls listAgencyUsers
 * directly for the initial render (no route round-trip on first paint).
 */
export async function GET(): Promise<Response> {
  const session = await auth();
  if (
    !session?.user?.agency_id ||
    !session.roles?.includes('agent-admin')
  ) {
    return new Response(null, { status: 403 });
  }
  try {
    const users = await listAgencyUsers({
      agencyId: session.user.agency_id,
    });
    return Response.json({ items: users });
  } catch (err) {
    // eslint-disable-next-line no-console
    console.error('[agents.list] 502', err);
    return new Response(null, { status: 502 });
  }
}
