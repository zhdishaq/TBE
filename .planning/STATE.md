---
gsd_state_version: 1.0
milestone: v1.0
milestone_name: milestone
status: Executing Phase 04
last_updated: "2026-04-16T14:39:14.519Z"
progress:
  total_phases: 7
  completed_phases: 3
  total_plans: 17
  completed_plans: 17
  percent: 100
---

# Project State: TBE

## Project Reference

See: .planning/PROJECT.md (updated 2026-04-12)

**Core value:** A unified booking platform where both direct customers and travel agents can search real inventory, complete bookings end-to-end, and have those bookings managed through a single backoffice — without switching systems.

**Current focus:** Phase 04 — b2c-portal-customer-facing

## Current Status

**Milestone:** v1.0 — Full Platform
**Phase:** 04 — B2C Portal (Customer-Facing) — Wave 0 scaffold complete
**Last action:** Phase 04 Plan 00 executed (Wave 0 scaffold) — b2c-web Next.js 16 portal scaffolded, Auth.js v5 edge-split wired, Vitest + Playwright harness in place, BookingService.Tests + Notifications.Tests red placeholders committed with Trait filter, Keycloak tbe-b2c realm patch + audience smoke script landed. Commits: 85e0be9, 553477e, 326c91d, 5b3999d.
**Last session stop:** 2026-04-16T14:38Z — Completed 04-00-PLAN.md (Wave 0).

## Phase Progress

| Phase | Name | Status |
|-------|------|--------|
| 1 | Infrastructure Foundation | Complete |
| 2 | Inventory Layer & GDS Integration | Complete |
| 3 | Core Flight Booking Saga (B2C) | Complete |
| 4 | B2C Portal (Customer-Facing) | In progress — Wave 0 complete (Plan 00) |
| 5 | B2B Agent Portal | Not started |
| 6 | Backoffice & CRM | Not started |
| 7 | Hardening & Go-Live | Not started |

## Phase 04 Plan Progress

| Plan | Name | Status | Commits |
|------|------|--------|---------|
| 04-00 | b2c-portal-scaffold (Wave 0) | Complete | 85e0be9, 553477e, 326c91d, 5b3999d |
| 04-01 | receipts (BookingService + QuestPDF) | Red placeholders staged | — |
| 04-02 | stripe-payment-intent | Pending | — |
| 04-03 | hotel-booking-confirmation-email | Red placeholders staged | — |
| 04-04 | baskets-multi-product | Red placeholders staged | — |
| 04-05 | b2c-e2e-mobile-coverage | Pending | — |

## Decisions Made (Plan 04-00)

- **Edge-safe Auth.js split** — `auth.config.ts` (no Node-only refresh logic) consumed by `middleware.ts`; full session/refresh implementation lives in `lib/auth.ts` for the Node runtime (Pitfall 3, D-01/D-02).
- **CSP whitelisting Stripe at the portal layer** — `next.config.mjs` headers include `js.stripe.com` in `script-src`, `frame-src`, and `connect-src` so 04-02 PaymentIntent flows work without per-page header overrides (Pitfall 16).
- **D-05 server-side Bearer forwarding via `gatewayFetch`** — Single helper in `lib/api-client.ts` reads the session and adds `Authorization: Bearer ${access_token}`; refuses calls without a session.
- **Red placeholders tagged with xUnit `Trait("Category","RedPlaceholder")`** — CI baseline `dotnet test` filters them out via `Category!=RedPlaceholder` so Wave 0 ships green while reserving the test contracts for downstream plans.
- **Keycloak realm patched, not replaced (D-14)** — `infra/keycloak/realm-tbe-b2c.json` is a delta layered on top of the existing Phase 1 realm export; documented import + manual fallback in `infra/keycloak/README.md`.

## Next Action

Run `/gsd-execute-plan 04-01` to implement BookingService receipts + QuestPDF generator (red placeholders already in place from 04-00).

## Open Human Actions

- Provision Keycloak `tbe-b2c-admin` service client and populate `KEYCLOAK_B2C_ADMIN_CLIENT_ID` / `KEYCLOAK_B2C_ADMIN_CLIENT_SECRET`. Until then `verify-audience-smoke.sh` exits with code 2 (env var unset) — **blocks 04-02/04-03 verification, not 04-01 execution**.
- Populate `STRIPE_SECRET_KEY` / `STRIPE_PUBLISHABLE_KEY` in `.env.test` before running Plan 04-02 e2e specs.

## Key Reminders

- Apply for GDS production credentials (Amadeus/Sabre/Galileo) NOW — takes 4-8 weeks
- Amadeus Self-Service REST credentials are same-day — use for Phase 1-2 development
- Never capture Stripe payment before a confirmed GDS ticket number exists
- Keycloak, not Duende IdentityServer (Duende requires paid license)
- YARP, not Ocelot (Ocelot is unmaintained)

---
*Initialized: 2026-04-12*
