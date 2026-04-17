// Plan 05-02 Task 3 -- /checkout/details RSC.
//
// Loads the session server-side, extracts roles (for admin-only fieldset),
// then hands off to the client <CheckoutDetailsForm />. The offer + quote
// are scaffolded via searchParams so a deep-link from the dual-pricing grid
// (`/checkout/details?offer={offerId}`) renders the right breadcrumb --
// wave 2 wires the real gateway /api/b2b/pricing/offers/{id} fetch.

import { redirect } from 'next/navigation';
import { auth } from '@/lib/auth';
import { CheckoutDetailsForm } from '@/app/(portal)/checkout/details/checkout-details-form';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

interface PageProps {
  searchParams: Promise<{ offer?: string }>;
}

export default async function CheckoutDetailsPage({ searchParams }: PageProps) {
  const session = await auth();
  if (!session) redirect('/login');
  const roles = session.roles ?? [];
  const sp = await searchParams;
  return (
    <section className="mx-auto flex w-full max-w-3xl flex-col gap-6 px-4 py-6">
      <header className="flex flex-col gap-1">
        <h1 className="text-xl font-semibold">Passenger &amp; contact details</h1>
        <p className="text-sm text-muted-foreground">
          Offer {sp.offer ?? '(not selected)'} -- capture traveller details
          and the agency contact for confirmation emails.
        </p>
      </header>
      <CheckoutDetailsForm roles={roles} />
    </section>
  );
}
