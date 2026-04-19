// Plan 06-02 Task 2 (BO-07) — supplier contracts list page with
// validity-window status chip (Upcoming / Active / Expired).
//
// Route: /suppliers — any ops-* role can read; mutate is gated at the
// form/route-handler level (ops-finance + ops-admin).

import { redirect } from 'next/navigation';
import { auth } from '@/lib/auth';
import { isOpsRead, hasAnyRole } from '@/lib/rbac';
import { SupplierContractsList } from './supplier-contracts-list';

export const dynamic = 'force-dynamic';

export default async function SuppliersPage() {
  const session = await auth();
  if (!session) redirect('/login');
  if (!isOpsRead(session)) redirect('/forbidden');

  const canMutate = hasAnyRole(session, ['ops-finance', 'ops-admin']);

  return (
    <section className="flex flex-col gap-4 p-6">
      <header className="flex flex-col gap-1">
        <h1 className="text-xl font-semibold text-slate-900">
          Supplier Contracts
        </h1>
        <p className="text-sm text-slate-500">
          Negotiated net-rate + commission agreements with airline / hotel
          / car / package suppliers. Status (Upcoming / Active / Expired)
          is computed from the validity window.
        </p>
      </header>
      <SupplierContractsList canMutate={canMutate} />
    </section>
  );
}
