// /cars — dedicated car-hire search landing (Plan 04-04 / CARB-01). Public (no auth required).
//
// RSC renders the form (client island) inside a hero. The form itself owns
// no search state; that lives in the URL once the user submits. Mirrors
// /hotels/page.tsx structurally so the three product landings (flights,
// hotels, cars) feel symmetrical (UI-SPEC §Hero Consistency).

import { CarSearchForm } from '@/components/search/car-search-form';

export const metadata = {
  title: 'Search car hire',
};

export default function CarsSearchPage() {
  return (
    <main className="flex w-full flex-col">
      <section className="bg-gradient-to-b from-blue-50 to-background px-6 py-14 md:px-10 lg:px-20">
        <div className="mx-auto flex max-w-4xl flex-col gap-4">
          <h1 className="text-2xl font-semibold md:text-3xl">Find your car hire</h1>
          <p className="text-sm text-muted-foreground">
            Compare rates across vendors, transmissions, and categories in real time.
          </p>
          <CarSearchForm className="mt-4" />
        </div>
      </section>
    </main>
  );
}
