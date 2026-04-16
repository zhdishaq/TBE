// /cars/results — car-hire results page (RSC shell) (Plan 04-04 / CARB-01).
//
// Pitfall 11 compliance: Next.js 16 passes async dynamic route data as
// Promises. This route has no dynamic `params` (the URL is
// /cars/results), so only `searchParams` is async — mirrors
// /hotels/results. We await searchParams before rendering so nuqs sees
// the canonical URL-state on the initial render. The actual filter +
// sort work happens client-side in `<CarResultsPanel>` keyed by nuqs.
//
// Pitfall 15: `dynamic = "force-dynamic"` because results are
// user-specific / cached upstream — we don't want Next's RSC cache to
// stale-serve a previous user's price tier.

import type { Metadata } from 'next';
import { ResultsProviders } from '@/components/results/results-providers';
import { CarResultsPanel } from '@/components/results/car-results-panel';

export const metadata: Metadata = {
  title: 'Car hire results',
};

export const dynamic = 'force-dynamic';

export default async function CarResultsPage({
  searchParams,
}: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  // Awaited for side-effect (future: server-hydrate initial cache). Today
  // we let the client do the first fetch so the initial HTML stays cheap.
  await searchParams;

  return (
    <main className="flex w-full flex-col px-6 py-8 md:px-10 lg:px-20">
      <div className="mx-auto w-full max-w-6xl">
        <ResultsProviders>
          <CarResultsPanel />
        </ResultsProviders>
      </div>
    </main>
  );
}
