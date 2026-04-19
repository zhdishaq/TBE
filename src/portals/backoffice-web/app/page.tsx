// Plan 06-01 Task 3 — backoffice placeholder landing page.
//
// The authenticated portal mount point (/dashboard, /approvals,
// /finance/wallet-credits, /operations/dlq, /bookings) lands in
// Tasks 4-7. This wave ships only a signed-out shell so the middleware
// can redirect authenticated visitors to /dashboard and the Playwright
// smoke suite (backoffice project) has a stable target for the unauth
// branch.

export default function HomePage() {
  return (
    <main className="mx-auto flex max-w-2xl flex-col gap-4 px-6 py-14">
      <h1 className="text-3xl font-semibold">TBE Backoffice Portal</h1>
      <p className="text-base text-muted-foreground">
        Sign in to continue. This portal is for TBE operations staff only —
        agents should use the B2B portal and customers should use the
        consumer site.
      </p>
    </main>
  );
}
