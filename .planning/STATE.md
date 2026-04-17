---
gsd_state_version: 1.0
milestone: v1.0
milestone_name: milestone
status: Executing Phase 05
last_updated: "2026-04-17T15:54:44.472Z"
progress:
  total_phases: 7
  completed_phases: 4
  total_plans: 22
  completed_plans: 23
  percent: 100
---

# Project State: TBE

## Project Reference

See: .planning/PROJECT.md (updated 2026-04-12)

**Core value:** A unified booking platform where both direct customers and travel agents can search real inventory, complete bookings end-to-end, and have those bookings managed through a single backoffice — without switching systems.

**Current focus:** Phase 05 — b2b-agent-portal

## Current Status

**Milestone:** v1.0 — Full Platform
**Phase:** 05 — B2B Agent Portal — Plan 05-01 complete (agent onboarding + gateway B2B audience flip)
**Last action:** Plan 05-01 executed atomically as 3 TDD tasks (6 commits — RED/GREEN each). `162604c` + `2573d7e` (Task 1 Auth.js session + authenticated header with role-conditional Admin nav), `8911572` + `67ca061` (Task 2 sub-agent CRUD via Keycloak Admin API with Pitfall 28 server-side agency_id injection + Radix Dialog create + Radix AlertDialog deactivate + TanStack Query list), `7d6e1e9` + `e3b8a0f` (Task 3 gateway scheme renamed `B2B` → `tbe-b2b` with `ValidateAudience=true` / `Audience="tbe-api"` + OnTokenValidated `realm_access.roles` projection + `B2BPolicy`/`B2BAdminPolicy` with `AddAuthenticationSchemes("tbe-b2b")` pin + new YARP routes for `/api/b2b/wallet` and `/api/b2b/invoices` + Gateway.Tests integration suite 8/8 passing). B2B-01 + B2B-02 marked complete in REQUIREMENTS.md. 4 auto-fixed deviations documented in 05-01-SUMMARY.md (scheme rename, NuGet vulnerability bump, realm_access projection, TestServer vs WebApplicationFactory).
**Last session stop:** 2026-04-17T15:30Z — Plan 05-01 complete; next: `/gsd-execute-phase 05` for Plan 05-02 (booking-saga B2B branch + pricing/markup + AgencyPriceRequested).

## Phase Progress

| Phase | Name | Status |
|-------|------|--------|
| 1 | Infrastructure Foundation | Complete |
| 2 | Inventory Layer & GDS Integration | Complete |
| 3 | Core Flight Booking Saga (B2C) | Complete |
| 4 | B2C Portal (Customer-Facing) | In progress — Wave 2 complete (Plans 00, 01, 02) |
| 5 | B2B Agent Portal | In progress — Wave 0 complete (Plan 05-00) |
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

## Phase 05 Plan Progress

| Plan | Name | Status | Commits |
|------|------|--------|---------|
| 05-00 | b2b-agent-portal-scaffold (Wave 0) | Complete | 8b8a376, d5d1f35, 64ff67c |
| 05-01 | agent-onboarding + Keycloak admin API helper | Complete | 162604c, 2573d7e, 8911572, 67ca061, 7d6e1e9, e3b8a0f |
| 05-02 | booking-saga B2B branch + pricing/markup + AgencyPriceRequested | Pending (9 red placeholders staged) | — |
| 05-03 | wallet top-up caps + Stripe PaymentIntent | Pending (3 red placeholders staged) | — |
| 05-04 | agency invoice PDF (GROSS only) + IDOR gates | Pending (6 red placeholders staged) | — |

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

## Decisions Made (Phase 05 discuss)

- **D-32 SSO model** — Shared browser session only; no OIDC brokering or unified realm between `tbe-b2b` and `tbe-backoffice`.
- **D-33 One user = one agency** — single-valued `agency_id` claim; multi-agency OTA-groups deferred.
- **D-34 Agency-wide booking visibility for all agent roles (agent, agent-admin, agent-readonly)** — deliberate override of ROADMAP Phase 5 UAT "sub-agent sees only their own bookings"; filter by `agency_id` only, never additionally by `sub`. Planner must cite D-34 in a comment at the controller boundary.
- **D-35 `agent-readonly` = agency oversight** — read-only agency-wide view for finance/compliance; no mutations.
- **D-36 Markup schema** — `pricing.AgencyMarkupRules (AgencyId, FlatAmount, PercentOfNet, RouteClass NULL, IsActive)`; max 2 rows per agency (base + optional RouteClass override); evaluation is `override ?? base`.
- **D-37 Per-booking markup override** — agent-admin only; `BookingSagaState.AgencyMarkupOverride decimal(18,4) NULL`; enforced via `B2BAdminPolicy`.
- **D-38 Markup CRUD out of Phase 5** — seed via EF migration/SQL; backoffice UI ships in Phase 6.
- **D-39 Post-ticket refund manual in Phase 6** — Phase 5 saga only releases pre-ticket reservations; post-ticket cancel returns `409 Conflict`.
- **D-40 Top-up caps via env config** — `Wallet__TopUp__MinAmount` / `Wallet__TopUp__MaxAmount` (default £10 / £50,000); enforced in `WalletController` BEFORE creating a Stripe PaymentIntent.
- **D-41 Commission settlement out of Phase 5** — displayed only; payout pipeline deferred to Phase 6.
- **D-42 Portal differentiation** — indigo-600 primary accent (WCAG AA vs slate-50) + outline "AGENT PORTAL" wordmark badge; starterKit `.jsx` untouched (Pitfall 17).
- **D-43 Invoice PDF = GROSS only** — new `AgencyInvoiceDocument` QuestPDF generator; no NET/markup/commission rendered.
- **D-44 UI-SPEC defaults locked** — compact tables, 20/50/100 page-number pager, stricter tone, 2-col dashboard, inline Stripe top-up, dark mode, Radix AlertDialog destructive confirms — all promoted from ASSUMED to LOCKED.

## Decisions Made (Plan 05-01)

- **Gateway JWT scheme renamed `B2B` → `tbe-b2b`** — Phase 1 shipped the staged scheme as `"B2B"`; Plan 05-01 required `"tbe-b2b"` so the audience-confusion mitigation (Pitfall 4 / T-05-01-01) is grep-verifiable in CI. Policy name `"B2BPolicy"` preserved so `appsettings.json` ReverseProxy routes need no edit. B2C + Backoffice schemes left byte-identical.
- **`ValidateAudience=true` + `Audience="tbe-api"` on tbe-b2b scheme** — flipped from staged `false`. Irreversible in effect: any token without `aud=tbe-api` 401s. Pre-deploy gate = `bash infra/keycloak/verify-audience-smoke-b2b.sh` exit 0.
- **OnTokenValidated projects `realm_access.roles` → flat `roles` claims** — Keycloak emits realm roles under a JSON envelope; without projection, `B2BPolicy`'s `HasClaim("roles", ...)` assertion never matches and every authenticated agent gets 403 (silent deny-all). Projection done once in Program.cs so downstream services need no envelope parser.
- **`AddAuthenticationSchemes("tbe-b2b")` pin on B2BPolicy + B2BAdminPolicy** — prevents a B2C token (audience mismatch detected upstream, but belt-and-braces) from ever satisfying a B2B policy even if routed to `/api/b2b/*`.
- **Server-side agency_id injection everywhere (Pitfall 28)** — `POST /api/agents` zod schema has no `agency_id` field; unknown keys rejected; route handler passes `session.user.agency_id` to `createSubAgent`. Pattern locked for every subsequent B2B route handler (05-02 bookings, 05-03 wallet, 05-04 invoices).
- **Role creation constrained to {agent, agent-readonly} in v1 (T-05-01-06)** — `POST /api/agents` zod enum excludes `agent-admin`; create-sub-agent-dialog.tsx radio group matches schema; literal `"agent-admin"` absent from the create dialog source.
- **IDOR guard via typed CrossTenantError** — `setUserEnabled` asserts target user's `agency_id` attribute equals caller's session agency_id; route handlers catch `CrossTenantError` → 403 + `console.warn` audit signal (T-05-01-05).
- **Route handler `export const runtime = 'nodejs'`** — `lib/keycloak-b2b-admin.ts` throws on browser import; `KEYCLOAK_B2B_ADMIN_CLIENT_SECRET` never touches the Edge runtime (T-05-01-04). Service-account token cached in-process with 30s expiry skew (mirror of 04-01 pattern).
- **Gateway.Tests via HostBuilder + TestServer (not WebApplicationFactory<Program>)** — real Program.cs boots YARP pointing at downstream container addresses that don't exist in-test; happy-path Facts would 502. TestServer mirrors production JwtBearer + policy config exactly with minimal endpoints (`/api/b2b/bookings/me` under B2BPolicy, `/api/b2b/admin/ping` under B2BAdminPolicy) so asserts land on the auth gate only. 8/8 Facts cover no-token → 401, wrong-issuer → 401, wrong-audience → 401, per-role × per-policy matrix.
- **Session shape D-33 locked in TypeScript** — `Session.roles: string[]` top-level, `Session.user.agency_id?: string` on user; JWT interface mirrors. Declared in `types/auth.d.ts`; populated in Auth.js `jwt()` + `session()` callbacks from Keycloak claims.

## Decisions Made (Plan 05-00)

- **Portal scaffolding = full fork of b2c-web** — no shared runtime package yet; keeps Phase 05 blast radius contained. Shared UI lives in byte-identical `components/ui/` (77 files, `diff -r` exit 0 — Pitfall 17).
- **Per-route CSP isolation (T-05-00-03)** — Next.js `next.config.mjs` uses two header blocks: `walletCsp` (Stripe allowed) on `/admin/wallet/:path*`, `standardCsp` (no Stripe) on `/:path*`. Order matters — narrow route first.
- **Session cookie name `__Secure-tbe-b2b.session-token`** — per-portal cookie scoping prevents cross-portal session leakage (Pitfall 19). Paired with separate Keycloak realm `tbe-b2b` (D-32 no OIDC brokering).
- **Red-placeholder convention** — `[Trait("Category","RedPlaceholder")]` + `Assert.Fail("MISSING — Plan XX-YY Task Z ...")`. Compiles + runs under `--filter Category=RedPlaceholder`; excluded from CI baseline via `Category!=RedPlaceholder`. 22 placeholders seeded across 4 .csproj projects.
- **BookingSagaState.Channel name collision deferred** — existing `Channel` string property on saga state clashes with `TBE.Contracts.Enums.Channel`. Red placeholders use `_ = Channel.B2C;` + `Assert.Fail` body so compile succeeds without running; Plan 05-02 Task 1 migrates the column and resolves the name.
- **PricingService.Tests is a new standalone xUnit project** — no pre-existing tests under services/PricingService/. No `.sln add` required (repo has no .sln).
- **Channel enum lives in `TBE.Contracts.Enums`** — `public enum Channel : int { B2C = 0, B2B = 1 }` with explicit int32 base. Default 0 = B2C guarantees existing saga rows retain direct-customer semantics after the Plan 05-02 migration.
- **Keycloak smoke script exit-code contract locked** — `verify-audience-smoke-b2b.sh` exits 0 (valid aud=tbe-api), 1 (audience mismatch), 2 (env unset; fails-closed). Documented in `infra/keycloak/README.md` table form.

## Next Action

Run `/gsd-execute-phase 05` to execute Plan 05-02 (booking-saga B2B branch + pricing/markup + AgencyPriceRequested). Plan 05-02 consumes the B2BPolicy + Auth.js session (agency_id + roles) shipped in Plan 05-01, and will migrate `BookingSagaState.Channel` from string → `TBE.Contracts.Enums.Channel` (staged in 05-00 — name collision deferred to 05-02 Task 1). Portal-side 05-02 adds AgencyPriceRequested saga hook + dual NET/GROSS pricing UI and per-booking markup override (agent-admin only).

**Pre-deploy gate** — before the gateway change from 05-01 (Task 3 `ValidateAudience=true`) is merged to an environment, a human must execute `bash infra/keycloak/verify-audience-smoke-b2b.sh` against that env's Keycloak and get exit 0. Exit 1 (audience mismatch) or exit 2 (env unset) blocks deploy — any B2B token without `aud=tbe-api` will 401 post-deploy. Rollback path: set `ValidateAudience = false` in Program.cs + redeploy.

After Phase 5, Phase 4 still has plans 04-03 / 04-04 / 04-05 staged (hotel booking, multi-product baskets, mobile E2E) — not blocked by Phase 5 but remaining backlog for the B2C portal.

## Open Human Actions

- **Plan 05-01 pre-deploy gate (blocks gateway rollout)** — Import `infra/keycloak/realm-tbe-b2b.json` into the target-env Keycloak (Realms → Add realm → Import). Populate `KEYCLOAK_B2B_ISSUER`, `KEYCLOAK_B2B_CLIENT_ID`, `KEYCLOAK_B2B_CLIENT_SECRET`, `KEYCLOAK_B2B_ADMIN_CLIENT_ID`, `KEYCLOAK_B2B_ADMIN_CLIENT_SECRET` in `src/portals/b2b-web/.env.local` (and the deployment env). Create a test `agent-admin` user with `agency_id` user attribute populated (GUID). Run `bash infra/keycloak/verify-audience-smoke-b2b.sh` from repo root — MUST exit 0 before the gateway `ValidateAudience=true` change ships to that env. Rollback: set `ValidateAudience = false` in Program.cs + redeploy.
- **Plan 05-03 prerequisite** — Populate `Wallet__TopUp__MinAmount` / `Wallet__TopUp__MaxAmount` env vars for PaymentService (defaults apply if unset: £10 / £50,000).
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
