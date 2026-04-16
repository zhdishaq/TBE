'use client';

// Dashboard Upcoming / Past tabs (D-17 + UI-SPEC §Dashboard).
//
// Client Component — Radix Tabs needs client-side state. The RSC page
// (`app/bookings/page.tsx`) partitions the server response into
// `upcoming` / `past` and hands them to this component.
//
// Copy MUST match `.planning/phases/04-b2c-portal-customer-facing/04-UI-SPEC.md`
// §Copywriting Contract verbatim. The empty-state strings are repeated
// here (not factored out) so a `grep` for the exact sentence always finds
// the rendered source.

import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { BookingRow } from '@/components/account/booking-row';
import { EmptyState } from '@/components/account/empty-state';
import type { BookingDtoPublic } from '@/types/api';

export type DashboardBooking = BookingDtoPublic;

export interface DashboardTabsProps {
  upcoming: DashboardBooking[];
  past: DashboardBooking[];
}

export function DashboardTabs({ upcoming, past }: DashboardTabsProps) {
  return (
    <Tabs defaultValue="upcoming" className="w-full">
      <TabsList>
        <TabsTrigger value="upcoming">Upcoming ({upcoming.length})</TabsTrigger>
        <TabsTrigger value="past">Past ({past.length})</TabsTrigger>
      </TabsList>

      <TabsContent value="upcoming" className="mt-6">
        {upcoming.length === 0 ? (
          <EmptyState
            heading="No upcoming trips"
            body="When you book a flight, hotel, or car, it will appear here. Search now to get started."
            action={{ href: '/', label: 'Start a search' }}
          />
        ) : (
          <ul className="flex flex-col gap-3">
            {upcoming.map((b) => (
              <li key={b.bookingId}>
                <BookingRow booking={b} />
              </li>
            ))}
          </ul>
        )}
      </TabsContent>

      <TabsContent value="past" className="mt-6">
        {past.length === 0 ? (
          <EmptyState
            heading="No past bookings yet"
            body="Your booking history will show here once you have completed a trip."
          />
        ) : (
          <ul className="flex flex-col gap-3">
            {past.map((b) => (
              <li key={b.bookingId}>
                <BookingRow booking={b} />
              </li>
            ))}
          </ul>
        )}
      </TabsContent>
    </Tabs>
  );
}
