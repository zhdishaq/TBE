// Plan 05-01 Task 2 — /admin/agents RSC page (agent-admin only).
//
// Role gate (defence in depth):
//   1. `middleware.ts` already redirects non-admin agents to /dashboard
//      before this RSC runs (softer UX than 403 per 05-UI-SPEC §Portal
//      Differentiation).
//   2. This RSC double-checks `session.roles?.includes('agent-admin')`
//      and re-issues redirect('/dashboard') if the middleware ever
//      regresses.
//   3. The GET /api/agents route handler enforces the same guard on
//      client-side refetches.
//
// Initial data fetch happens here (RSC, server-side) so the admin sees
// the sub-agent list on first paint without a client round-trip. The
// `<SubAgentList>` client component then owns invalidate + refetch via
// TanStack Query.

import { redirect } from 'next/navigation';
import { auth } from '@/lib/auth';
import { listAgencyUsers } from '@/lib/keycloak-b2b-admin';
import { SubAgentList } from '@/components/admin/sub-agent-list';

export const dynamic = 'force-dynamic';

export default async function AdminAgentsPage() {
  const session = await auth();
  if (!session?.user?.agency_id) redirect('/login');
  // D-32 / B2BAdminPolicy — non-admin agents never see this surface.
  if (!session.roles?.includes('agent-admin')) redirect('/dashboard');

  const initialUsers = await listAgencyUsers({
    agencyId: session.user.agency_id,
  });

  return (
    <main className="mx-auto flex max-w-6xl flex-col gap-6 px-6 py-8">
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-semibold text-foreground">Sub-agents</h1>
        <p className="text-sm text-muted-foreground">
          Create, list, and deactivate sub-agents under your agency.
        </p>
      </div>
      <SubAgentList initialUsers={initialUsers} />
    </main>
  );
}
