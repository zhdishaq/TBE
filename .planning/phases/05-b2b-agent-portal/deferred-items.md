# Phase 05 — deferred items

Running log of items identified by plans in this phase but deferred to a
follow-up plan. Each bullet should be picked up by a fresh `/gsd-plan`
invocation when the phase returns to them.

## From Plan 05-03 Task 3

- [ ] `/admin/wallet` portal surface (13 files — Next.js client/server
      components, Stripe Elements wrapper, route-scoped CSP narrowing,
      sitewide low-balance banner, RequestTopUpLink mailto,
      insufficient-funds-panel retrofit + Vitest suite).

## Audit notes

- (Plan 05-04 audit note closed 2026-04-18.) `TicketingDeadlineWarning` /
  `TicketingDeadlineUrgent` are now consumed by `TicketingDeadlineConsumer`
  in `BookingService.Application`, registered in
  `BookingService.API/Program.cs`, and email fan-out resolves recipients via
  `IKeycloakB2BAdminClient` (role intersection: `agent-admin` OR `agent`).
  The running BookingService no longer accumulates `skipped_messages` on
  the default exchange.
