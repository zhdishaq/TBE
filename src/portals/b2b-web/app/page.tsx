// Phase 5 Plan 05-00 — B2B Agent Portal placeholder landing page.
//
// Source: 05-00-PLAN action step 14.
//
// Wave 0 deliberately ships nothing beyond a signed-out placeholder. The
// authenticated /dashboard, /bookings, /search, /admin/* routes are built
// in Plans 05-01..05-04. The only requirement here is that an unauth'd
// visitor to `/` sees a page with visible "Agent portal" copy so the
// Playwright smoke spec in Task 2 can assert on it.

export default function HomePage() {
  return (
    <main className="mx-auto flex max-w-2xl flex-col gap-4 px-6 py-14">
      <h1 className="text-3xl font-semibold">TBE Agent Portal</h1>
      <p className="text-base text-muted-foreground">
        Sign in to continue. This portal is for accredited travel agents only —
        direct customers should use the consumer site.
      </p>
    </main>
  );
}
