---
gsd_state_version: 1.0
milestone: v1.0
milestone_name: milestone
status: Executing Phase 04
last_updated: "2026-04-16T17:23:47.163Z"
progress:
  total_phases: 7
  completed_phases: 3
  total_plans: 17
  completed_plans: 19
  percent: 100
---

# Project State: TBE

## Project Reference

See: .planning/PROJECT.md (updated 2026-04-12)

**Core value:** A unified booking platform where both direct customers and travel agents can search real inventory, complete bookings end-to-end, and have those bookings managed through a single backoffice — without switching systems.

**Current focus:** Phase 04 — b2c-portal-customer-facing

## Current Status

**Milestone:** v1.0 — Full Platform
**Phase:** 04 — B2C Portal (Customer-Facing) — Wave 2 (Plans 04-00, 04-01, 04-02) complete
**Last action:** Phase 04 Plan 02 executed (flight product end-to-end). InventoryService seeded 7,698 OpenFlights airports into Redis via `IataAirportSeeder` BackgroundService + `GET /airports` public rate-limited typeahead. b2c-web shipped nuqs+TanStack Query search surfaces (14 URL params, client-side filter/sort with zero refetch per D-12), the Stripe PaymentElement checkout flow (details → payment → processing poll → success), memoised `loadStripe` (Pitfall 5), email-verify gate at `/checkout/payment` (D-06, Pitfall 7), and 2000ms/90s poll-driven success (Pitfall 6 / D-12). Commits: 90ced55, 9f2ca48, 2bb74cc, 125dd93, 2438b2d.
**Last session stop:** 2026-04-16T16:55Z — Completed 04-02-PLAN.md (Wave 2 — flight product end-to-end).

## Phase Progress

| Phase | Name | Status |
|-------|------|--------|
| 1 | Infrastructure Foundation | Complete |
| 2 | Inventory Layer & GDS Integration | Complete |
| 3 | Core Flight Booking Saga (B2C) | Complete |
| 4 | B2C Portal (Customer-Facing) | In progress — Wave 2 complete (Plans 00, 01, 02) |
| 5 | B2B Agent Portal | Not started |
| 6 | Backoffice & CRM | Not started |
| 7 | Hardening & Go-Live | Not started |

## Phase 04 Plan Progress

| Plan | Name | Status | Commits |
|------|------|--------|---------|
| 04-00 | b2c-portal-scaffold (Wave 0) | Complete | 85e0be9, 553477e, 326c91d, 5b3999d |
| 04-01 | receipts + B2C account surfaces | Complete | 785fee3, 2343996, 9c6c8eb, 00cfb20 |
| 04-02 | flight product end-to-end (IATA + search + checkout) | Complete | 90ced55, 9f2ca48, 2bb74cc, 125dd93, 2438b2d |
| 04-03 | hotel-booking-confirmation-email | Red placeholders staged | — |
| 04-04 | baskets-multi-product | Red placeholders staged | — |
| 04-05 | b2c-e2e-mobile-coverage | Pending | — |

## Decisions Made (Plan 04-00)

- **Edge-safe Auth.js split** — `auth.config.ts` (no Node-only refresh logic) consumed by `middleware.ts`; full session/refresh implementation lives in `lib/auth.ts` for the Node runtime (Pitfall 3, D-01/D-02).
- **CSP whitelisting Stripe at the portal layer** — `next.config.mjs` headers include `js.stripe.com` in `script-src`, `frame-src`, and `connect-src` so 04-02 PaymentIntent flows work without per-page header overrides (Pitfall 16).
- **D-05 server-side Bearer forwarding via `gatewayFetch`** — Single helper in `lib/api-client.ts` reads the session and adds `Authorization: Bearer ${access_token}`; refuses calls without a session.
- **Red placeholders tagged with xUnit `Trait("Category","RedPlaceholder")`** — CI baseline `dotnet test` filters them out via `Category!=RedPlaceholder` so Wave 0 ships green while reserving the test contracts for downstream plans.
- **Keycloak realm patched, not replaced (D-14)** — `infra/keycloak/realm-tbe-b2c.json` is a delta layered on top of the existing Phase 1 realm export; documented import + manual fallback in `infra/keycloak/README.md`.

## Decisions Made (Plan 04-01)

- **D-17 `/customers/me/bookings` shipped as a delegator** — resolves `customerId` from `ClaimTypes.NameIdentifier ?? sub` and hands off to the existing `ListForCustomerAsync`; the `{customerId}` route is preserved for backoffice-staff.
- **FLTB-03 fare breakdown persisted on BookingSagaState** — added `BaseFareAmount`, `SurchargeAmount`, `TaxAmount` decimal(18,4) NOT NULL DEFAULT 0 columns via hand-authored migration `20260500000000_AddReceiptFareBreakdown` (03-01 ModelSnapshot convention). Receipts can be regenerated without re-querying GDS pricing.
- **QuestPDF test content verified via PdfPig 0.1.10** — naive ASCII substring search on the rendered bytes silently fails because QuestPDF FlateDecode-compresses content streams. Tests now extract decompressed text.
- **Resend-verification uses `tbe-b2c-admin` service-account token** — `lib/keycloak-admin.ts` caches the token in-process with a 30s expiry skew; the route handler is Node-runtime-only; the module throws on browser import; the token is never logged (Pitfall 8, T-04-01-04).
- **Auth.js v5 session.user.id wired to Keycloak `sub`** — explicit preservation in `token.sub` on initial sign-in so the Admin API can address the right user in future calls.
- **Stream-through receipt proxy** — `/api/bookings/[id]/receipt.pdf` returns `new Response(upstream.body, ...)` with an awaited `params` Promise (Pitfall 11 + 14). Never `upstream.arrayBuffer()`.
- **Ambient UI shim for starterKit `.jsx`** — `types/ui.d.ts` declares `Button`, `Tabs`, `TabsList`, etc. with a prop-bag shape so TypeScript doesn't force every caller to pass every variant prop. Preserves Pitfall 17's "ship `.jsx` untouched" rule.

## Decisions Made (Plan 04-02)

- **D-18 OpenFlights-backed IATA typeahead** — 7,698 airports seeded at InventoryService boot via `IataAirportSeeder` BackgroundService; Redis SortedSet prefix index + Hash lookup; idempotent via `iata:seed:done` flag; `FORCE_RESEED=true` env override for dev.
- **Public-anonymous AirportsController** — `[Authorize]` deliberately omitted (CONTEXT: "anonymous users can browse and search"). Anti-abuse via AspNetCore `RequireRateLimiting` fixed-window 60/min/IP + input length bounds (min 2 / max 8 chars) per T-04-02-04.
- **Pitfall 5 enforced structurally** — `<Elements>` lives ONLY in `components/checkout/payment-element-wrapper.tsx` imported only by `app/checkout/payment/page.tsx`; `loadStripe` is module-scoped and memoised (`let _p; export const getStripe = () => (_p ??= loadStripe(pk))`); when `!email_verified` the payment RSC returns `<EmailVerifyGate>` BEFORE creating a PaymentIntent or mounting `<Elements>` — stripe.js never loads for unverified users.
- **Pitfall 6 / D-12 success signalling** — `/checkout/success` is reachable ONLY via `router.push` from the processing page's poll terminal `Confirmed` branch; Stripe's return_url query (`payment_intent=…`, `redirect_status=…`) is never treated as success; the desktop e2e asserts success URL carries `booking=` (poll) and NOT `payment_intent=` (Stripe redirect).
- **D-06 / Pitfall 7 email-verify gate** — `EmailVerifyGate` is a non-dismissable dialog (no X, no backdrop close, no Esc); wired at `/checkout/payment` via RSC `auth()` session check AND reinforced in `middleware.ts` which bounces to `/checkout/verify-email` when `!email_verified`.
- **TanStack Query key excludes filters** — `['flights', from, to, dep, ret, adt, chd, infl, infs, cabin]` only; filter/sort changes never refetch (D-12 / Pitfall 11); filtered view is computed via `useMemo` over cached offers. `staleTime=90_000` matches Phase 2 Redis selection-phase TTL.
- **B2C-05 mobile 5-step budget** — stepper fixed at Search / Results / Select / Details / Payment; processing + success explicitly excluded from the step count; Playwright mobile spec uses `framenavigated` listener to count unique in-app paths with processing/success filtered out, asserts `toBeLessThanOrEqual(5)`.

## Next Action

Run `/gsd-execute-plan 04-03` to implement the hotel search + booking UI + HotelBookingSagaState + NotificationService voucher consumer (HOTB-01..05, NOTF-02 primary). Red placeholders from 04-00 are staged.

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
