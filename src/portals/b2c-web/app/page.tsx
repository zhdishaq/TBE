// Landing page — hero + embedded flight search form (UI-SPEC §Landing).
//
// RSC: renders zero JS by itself. The form is a client island.

import { FlightSearchForm } from '@/components/search/flight-search-form';

export default function HomePage() {
  return (
    <main className="flex w-full flex-col">
      <section className="bg-gradient-to-b from-blue-50 to-background px-6 py-14 md:px-10 lg:px-20">
        <div className="mx-auto flex max-w-6xl flex-col gap-4">
          <h1 className="text-display text-3xl font-semibold md:text-4xl">
            TBE — book your trip
          </h1>
          <p className="max-w-2xl text-base text-muted-foreground">
            Search flights from 400+ airlines, pick a fare you like, and
            pay securely. Hotels and cars are one basket away.
          </p>
          <FlightSearchForm mode="embedded" className="mt-6" />
        </div>
      </section>

      <section className="px-6 py-10 md:px-10 lg:px-20">
        <div className="mx-auto grid max-w-6xl gap-6 md:grid-cols-3">
          <article>
            <h2 className="text-lg font-semibold">Transparent fares</h2>
            <p className="text-sm text-muted-foreground">
              We show airline surcharges (YQ/YR) and taxes separately so
              you know exactly what you&apos;re paying for.
            </p>
          </article>
          <article>
            <h2 className="text-lg font-semibold">Multi-product baskets</h2>
            <p className="text-sm text-muted-foreground">
              Bundle a flight and a hotel in one payment; we handle partial
              confirmations with a single email summary.
            </p>
          </article>
          <article>
            <h2 className="text-lg font-semibold">Real customer support</h2>
            <p className="text-sm text-muted-foreground">
              Our backoffice team operates 24/7 with live booking management
              and PNR access.
            </p>
          </article>
        </div>
      </section>
    </main>
  );
}
