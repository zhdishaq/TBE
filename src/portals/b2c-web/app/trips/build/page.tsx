'use client';

// /trips/build — Trip Builder canvas (Plan 04-04 / PKG-01 / D-07).
//
// Side-by-side flight + hotel panels (desktop grid-cols-2; mobile stacks)
// above a sticky <BasketFooter> that carries the "Continue to checkout"
// CTA. The page is intentionally a client component because the basket
// lives in a Zustand store hydrated from sessionStorage.
//
// Acceptance grep requires `grid-cols-2` in this file to prove the
// side-by-side layout (D-07 Side-by-side-not-stepper decision).

import { FlightPanel } from '@/components/trip-builder/flight-panel';
import { HotelPanel } from '@/components/trip-builder/hotel-panel';
import { BasketFooter } from '@/components/trip-builder/basket-footer';

export default function TripBuilderPage() {
  return (
    <main className="flex min-h-screen w-full flex-col">
      <section className="flex-1 px-6 py-8 md:px-10 lg:px-20">
        <div className="mx-auto flex w-full max-w-6xl flex-col gap-4">
          <header className="flex flex-col gap-1">
            <h1 className="text-2xl font-semibold md:text-3xl">Build your trip</h1>
            <p className="text-sm text-muted-foreground">
              Combine a flight and a hotel into a single booking with one
              charge on your statement.
            </p>
          </header>
          <div className="grid gap-4 md:grid-cols-2">
            <FlightPanel />
            <HotelPanel />
          </div>
        </div>
      </section>
      <BasketFooter />
    </main>
  );
}
