// /hotels — dedicated hotel search landing (HOTB-01). Public (no auth required).
//
// RSC renders the form (client island) inside a hero. The form itself
// owns no search state; that lives in the URL once the user submits.
// Mirrors /flights/page.tsx structurally so the two product landings
// feel symmetrical (UI-SPEC §Hero Consistency).

import { HotelSearchForm } from '@/components/search/hotel-search-form';

export const metadata = {
  title: 'Search hotels',
};

export default function HotelsSearchPage() {
  return (
    <main className="flex w-full flex-col">
      <section className="bg-gradient-to-b from-blue-50 to-background px-6 py-14 md:px-10 lg:px-20">
        <div className="mx-auto flex max-w-4xl flex-col gap-4">
          <h1 className="text-2xl font-semibold md:text-3xl">Find your next stay</h1>
          <p className="text-sm text-muted-foreground">
            Compare real-time availability across hundreds of properties worldwide.
          </p>
          <HotelSearchForm className="mt-4" />
        </div>
      </section>
    </main>
  );
}
