// Plan 05-01 Task 1 — authenticated /dashboard placeholder.
//
// The full 2-column dashboard (TTL alerts + wallet card + recent
// bookings + quick links) lands in Plan 05-04. Plan 05-01 ships a
// minimal placeholder so the header + nav chrome render end-to-end and
// the login round-trip has a legitimate landing page.

export default function DashboardPage() {
  return (
    <main className="mx-auto flex max-w-4xl flex-col gap-4 px-6 py-10">
      <h1 className="text-2xl font-semibold text-foreground">Dashboard</h1>
      <p className="text-sm text-muted-foreground">
        Your agency activity lands here once Plan 05-04 wires the booking
        and TTL cards. Use the nav to visit the search and booking
        surfaces in the meantime.
      </p>
    </main>
  );
}
