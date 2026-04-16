// /flights/results — results page (RSC shell).
//
// Pitfall 11 compliance: Next.js 16 passes `searchParams` as a Promise
// on dynamic routes; we await it before rendering so nuqs sees the
// canonical URL-state on the initial render. The actual filter + sort
// work happens client-side in `<SearchResultsPanel>` keyed by nuqs.

import type { Metadata } from 'next';
import { ResultsProviders } from '@/components/results/results-providers';
import { SearchResultsPanel } from '@/components/results/search-results-panel';

export const metadata: Metadata = {
  title: 'Flight results',
};

export default async function FlightResultsPage({
  searchParams,
}: {
  // Next.js 16 dynamic searchParams are async (Pitfall 11).
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  // Awaited for side-effect (future: server-hydrate initial cache). Today
  // we let the client do the first fetch so the initial HTML stays cheap.
  await searchParams;

  return (
    <main className="flex w-full flex-col px-6 py-8 md:px-10 lg:px-20">
      <div className="mx-auto w-full max-w-6xl">
        <ResultsProviders>
          <SearchResultsPanel />
        </ResultsProviders>
      </div>
    </main>
  );
}
