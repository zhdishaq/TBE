// Plan 05-05 Task 4 RED stub. GREEN replaces with RSC + HydrationBoundary +
// dehydrate + three prefetchQuery calls + agent-admin role guard.

import { redirect } from 'next/navigation';
import { auth } from '@/lib/auth';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

export default async function AdminWalletPage() {
  const session = await auth();
  const roles = (session as { roles?: string[] } | undefined)?.roles ?? [];
  if (!roles.includes('agent-admin')) {
    redirect('/forbidden');
  }
  return <div>Admin wallet stub</div>;
}
