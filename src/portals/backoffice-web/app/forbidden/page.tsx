// Plan 05-04 Task 3 — /forbidden.
//
// Generic access-denied page. Rendered when the booking-detail RSC receives a
// 404 from BookingService (which could mean "booking doesn't exist" OR "booking
// exists but belongs to another agency"). Per Pitfall 10, we MUST NOT
// distinguish those two cases in the UI — doing so would leak the existence of
// someone else's booking id to a bruteforcing attacker. Both collapse into a
// single "You don't have access" page with a link back to the bookings list.
//
// Deliberately static — no session or booking context is read here, so nothing
// can leak via timing or response shape.

import Link from 'next/link';

export const dynamic = 'force-static';

export default function ForbiddenPage() {
  return (
    <main className="mx-auto flex max-w-xl flex-col items-start gap-4 px-6 py-12">
      <h1 className="text-2xl font-semibold text-foreground">Access denied</h1>
      <p className="text-muted-foreground">
        You don&apos;t have access to this booking. It may belong to another
        agency, or it may no longer exist.
      </p>
      <p className="text-muted-foreground">
        If you think this is a mistake, please contact your agency administrator.
      </p>
      <Link
        href="/bookings"
        className="inline-flex items-center rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground hover:bg-primary/90"
      >
        Back to bookings
      </Link>
    </main>
  );
}
