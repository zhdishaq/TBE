// /flights — dedicated search landing. Public (no auth required).
//
// RSC renders the form (client island) inside a hero. The form itself
// owns no search state; that lives in the URL once the user submits.

import { FlightSearchForm } from '@/components/search/flight-search-form';

export const metadata = {
  title: 'Search flights',
};

export default function FlightsSearchPage() {
  return (
    <main className="flex w-full flex-col">
      <section className="bg-gradient-to-b from-blue-50 to-background px-6 py-14 md:px-10 lg:px-20">
        <div className="mx-auto flex max-w-4xl flex-col gap-4">
          <h1 className="text-2xl font-semibold md:text-3xl">Find your next flight</h1>
          <p className="text-sm text-muted-foreground">
            Enter your route and dates to compare real-time fares.
          </p>
          <FlightSearchForm className="mt-4" />
        </div>
      </section>
    </main>
  );
}
