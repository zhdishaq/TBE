---
gsd_state_version: 1.0
milestone: v1.0
milestone_name: milestone
status: Executing Phase 05
last_updated: "2026-04-18T07:22:21.420Z"
progress:
  total_phases: 7
  completed_phases: 4
  total_plans: 22
  completed_plans: 25
  percent: 100
---

# Project State: TBE

## Project Reference

See: .planning/PROJECT.md (updated 2026-04-12)

**Core value:** A unified booking platform where both direct customers and travel agents can search real inventory, complete bookings end-to-end, and have those bookings managed through a single backoffice — without switching systems.

**Current focus:** Phase 05 — b2b-agent-portal

## Current Status

**Milestone:** v1.0 — Full Platform
**Phase:** 05 — B2B Agent Portal — Plan 05-03 Tasks 1+2 complete (wallet top-up caps + RFC 7807 problem+json + low-balance monitor + MassTransit consumer + Keycloak B2B admin client). Task 3 (/admin/wallet portal surface) deferred to a follow-up plan.
**Last action:** Plan 05-03 executed atomically across two agent sessions as two TDD tasks (4 commits — RED/GREEN each). `1bb77a2` + `982d4a7` (Task 1 — `WalletOptions` nested shape + `WalletTopUpService` D-40 cap enforcement via `IOptionsMonitor<WalletOptions>.CurrentValue.TopUp` + idempotent `CommitTopUpAsync` using `stripe-topup-{pi.Id}` key (Pitfall 20 webhook replay) + `B2BWalletController` `/api/wallet/*` with `ContentResult { ContentType = "application/problem+json" }` on cap violations + `B2BPolicy` / `B2BAdminPolicy` + Pitfall 28 agency_id-from-JWT-only). `d8ed7f2` + `57de6f9` (Task 2 — `WalletLowBalanceMonitor : BackgroundService` with `public TickAsync` + `IPublishEndpoint` fan-out + `WalletLowBalanceConsumer : IConsumer<WalletLowBalanceDetected>` with `AgencyWallet.LastLowBalanceEmailAtUtc` cooldown defence-in-depth + `IKeycloakB2BAdminClient` service-account token cache (30s skew + `SemaphoreSlim`) resolving the intersection of `q=agency_id:X&exact=true` AND `agent-admin` role-mapping (T-05-03-11 anti-spoof) + EF migration `20260525000000_AddAgencyWallet` with `UNIQUE(AgencyId)` + `AgencyWalletRepository` Dapper impl with `MERGE WITH (HOLDLOCK)` upsert + `WalletLowBalanceEmailSender` logger-only Phase-5 MVP stub + `Program.cs` wiring for `TimeProvider.System` / `AddHttpClient<IKeycloakB2BAdminClient>` / `AddHostedService<WalletLowBalanceMonitor>` / `x.AddConsumer<WalletLowBalanceConsumer>` + `appsettings.json` `KeycloakB2B` section). 30/30 Payments.Tests facts green (23 Task 1 + 7 Task 2). B2B-07 (atomic wallet deduction) marked complete — production gate is the Phase 03-01 `UPDLOCK,ROWLOCK,HOLDLOCK` path on `payment.WalletTransactions` already consumed by Plan 05-02's saga B2B branch; Plan 05-03 adds the low-balance advisory flow on top without regressing it. 7 deviations documented in 05-03-SUMMARY.md (6 from Task 1, 1 `ReadFromJsonAsync(cancellationToken: ct)` fix in Task 2).
**Last session stop:** 2026-04-17T18:00Z — Plan 05-03 Tasks 1+2 complete; Task 3 deferred. Next: `/gsd-plan` a standalone `/admin/wallet` portal surface plan (13 new files across Next.js client/server components, Stripe Elements wrapper, route-scoped CSP narrowing, sitewide low-balance banner, RequestTopUpLink mailto, insufficient-funds-panel retrofit + Vitest suite), then `/gsd-execute-phase 05` for Plan 05-04 (agency invoice PDF + IDOR gates).

## Phase Progress

| Phase | Name | Status |
|-------|------|--------|
| 1 | Infrastructure Foundation | Complete |
| 2 | Inventory Layer & GDS Integration | Complete |
| 3 | Core Flight Booking Saga (B2C) | Complete |
| 4 | B2C Portal (Customer-Facing) | In progress — Wave 2 complete (Plans 00, 01, 02) |
| 5 | B2B Agent Portal | In progress — Plan 05-03 Tasks 1+2 complete (top-up caps + low-balance monitor); Task 3 (/admin/wallet portal surface) deferred |
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
| 05-02 | booking-saga B2B branch + pricing/markup + AgencyPriceRequested | Complete | 74c3aeb, c947af9, 90e9607, e021622, 5720842, 6ed72e5 |
| 05-03 | wallet top-up caps + low-balance monitor + Keycloak B2B admin client | Tasks 1+2 complete; Task 3 (/admin/wallet portal surface) deferred | 1bb77a2, 982d4a7, d8ed7f2, 57de6f9 |
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

## Decisions Made (Plan 05-03)

- **D-40 hot-reload via IOptionsMonitor<WalletOptions>** — `WalletTopUpService.CreateTopUpIntentAsync` reads `CurrentValue.TopUp` on every call; `WalletLowBalanceMonitor.TickAsync` reads `CurrentValue.LowBalance.PollIntervalMinutes` on every tick (guarded by `Math.Max(1, ...)`). Admins can change caps and cadence via env/config without a PaymentService restart.
- **Content-Type pinned RFC 7807** — cap violations on `POST /api/wallet/top-up/intent` return `ContentResult { ContentType = "application/problem+json", StatusCode = 400 }` with hand-serialised JSON body carrying `type = /errors/wallet-topup-out-of-range` + `allowedRange { min, max, currency }` + `requested` extensions. `ObjectResult<ProblemDetails>` is deliberately NOT used because its Content-Type is swayed by the active output formatter — the portal's content-type branching must not receive `application/json`.
- **Pitfall 28 — JWT agency_id claim is single source of truth** — `B2BWalletController` derives `agency_id` from `User.FindFirst("agency_id")` on every endpoint; body-supplied `agencyId` is never deserialised. Locked pattern for all subsequent B2B endpoints.
- **T-05-03-07 separation of concerns** — monitor publishes `WalletLowBalanceDetected`, consumer emails. Retry semantics flow through MassTransit error-queue redelivery; the monitor never sends e-mail directly.
- **T-05-03-11 anti-spoofing — Keycloak intersection** — `KeycloakB2BAdminClient.GetAgentAdminsForAgencyAsync` returns the intersection of `GET /admin/realms/tbe-b2b/users?q=agency_id:X&exact=true` AND per-user `GET /users/{id}/role-mappings/realm` where `name = agent-admin`. A user merely added to the agency without the admin role never appears in the recipient list.
- **Hysteresis re-arm flow** — `AgencyWallet.LowBalanceEmailSent` is flipped `true` by `WalletLowBalanceConsumer.Consume` on successful notify, reset `false` by (a) `WalletTopUpService.CommitTopUpAsync` on balance cross-up, and (b) `AgencyWalletRepository.SetThresholdAsync` on threshold change. Monitor query `WHERE LowBalanceEmailSent = 0 ... HAVING SUM < Threshold` — so one cross-down fires exactly one advisory per cycle.
- **1:1 wallet↔agency mapping — walletId == agencyId** — architectural reconciliation surfaced during implementation, not in PLAN.md. `IAgencyWalletRepository.ListAgenciesBelowThresholdAsync` LEFT JOINs `payment.WalletTransactions ON WalletId = AgencyId`. Keeps Phase 03-01's `IWalletRepository` (keyed by WalletId) working without an extra lookup.
- **Stub IWalletLowBalanceEmailSender for Phase-5 MVP** — logger-only `"wallet low-balance advisory (stub)"` implementation; non-throwing so the consumer can still flip `LowBalanceEmailSent = 1` in dev/test. Real SendGrid transport deferred to a follow-up plan after the cross-service advisory-template contract with NotificationService is approved.
- **Task 3 (/admin/wallet portal surface) explicitly deferred to a follow-up plan** — 13-file Next.js portal scope (RSC page + Stripe Elements + transactions table + threshold dialog + sitewide low-balance banner + RequestTopUpLink + route-scoped CSP + insufficient-funds-panel retrofit + vitest suite). Documented open acceptance criteria and remaining STRIDE threats (T-05-03-06 CSP leak, T-05-03-08, T-05-03-09 mailto session-leak) in 05-03-SUMMARY.md "Deferred Work".
- **Distinct contract — WalletLowBalanceDetected ≠ WalletLowBalance** — new `TBE.Contracts.Messages.WalletLowBalanceDetected(AgencyId, Balance, Threshold, Currency, DetectedAt)` introduced alongside the pre-existing `TBE.Contracts.Events.WalletLowBalance` (Phase 03-04, WalletId-only). A `Detected_contract_shape` guardrail xUnit fact prevents a future refactor from collapsing the two records.
- **EF migration ordering locked — 20260525000000_AddAgencyWallet** — lands after Plan 05-02's `20260520000000_AddB2BBookingColumns`; table `payment.AgencyWallets` with `UNIQUE(AgencyId)` (T-05-03-05 loud cross-tenant failure), `decimal(18,4)` money columns, `SYSUTCDATETIME()` defaults.
- **Deviations auto-fixed during execution (7 total)** — Task 1 (6): problem+json ContentResult fix, `Microsoft.AspNetCore.Mvc.Testing 8.0.11` add (`net8`-pinned), path reconciliation (no Domain project; AgencyWallet in Application layer), WalletTopUpCapsTests gravestone, NSubstitute `ClearSubstitute` hygiene, legacy-controller role-string preservation (`"agency-admin"` hyphenated vs new `B2BAdminPolicy("agent-admin")`). Task 2 (1): `ReadFromJsonAsync` uses `cancellationToken:` (positional-named), not `ct:` — fixed three call sites in `KeycloakB2BAdminClient.cs` after CS1739.

## Decisions Made (Plan 05-02)

- **D-33 claim-sourced AgencyId everywhere** — `AgentBookingsController` stamps `AgencyId` from `User.FindFirst("agency_id")`, never from the request body. `CreateAgentBookingRequest` DTO literally omits the `AgencyId` + `Channel` properties so a tampered JSON body has nothing to be parsed from (T-05-02-01 / T-05-02-08 mitigated at the type level).
- **D-34 agency-wide booking visibility** — `ListForAgencyAsync(agencyId)` filters by `AgencyId == caller.AgencyId` ONLY; never additionally by `sub`. Comment in controller cites D-34 explicitly. Verified by `AgentBookingsControllerTests.ListForAgencyAsync_returns_only_caller_agency_bookings_ignoring_sub`.
- **D-35 agent-readonly write gate** — controller short-circuits to 403 when `caller.HasRole("agent-readonly") && !caller.HasRole("agent")`. Policy-level admission allows all three roles (so readonly can still read), but the create action checks the readonly role at the handler boundary. Verified by `Readonly_role_receives_403_on_POST`.
- **D-36 frozen agency pricing snapshot** — `BookingSagaState.AgencyNetFare/MarkupAmount/GrossAmount/CommissionAmount` stamped ONCE at `AgentBookingDetailsCaptured` (before `PnrCreated`); never re-quoted after ticket issuance. Pricing re-quotes are rejected by the `DuringAny` handler (idempotent per D-40).
- **D-37 admin-only AgencyMarkupOverride** — controller returns 403 when non-admin attempts to set `AgencyMarkupOverride`. Client-side `CheckoutDetailsForm` hides the fieldset from non-admin as UX polish; the server gate is authoritative.
- **D-40 idempotency_key = BookingId.ToString()** — `WalletReserveCommand.IdempotencyKey` always equals the saga's BookingId, so retries of the same reserve never double-debit the wallet (T-05-02-04). Enforced in `BookingSaga.PnrCreated` branch.
- **D-41 commission == markup in v1** — `MarkupRulesEngine.ApplyMarkup` returns `commission = markupAmount`. Future versions can split, but v1 invariant is locked for agent-facing grids and invoice PDFs.
- **D-44 4-column dual-pricing lock** — NET / Markup / GROSS / Commission in fixed order; `tabular-nums` + `aria-label` on every price cell; Commission is the only green-coloured column; indigo-600 + 1px ring-indigo-200 selection treatment. Locked by 7 vitest facts in `dual-pricing-grid.test.tsx` + grep assertions in the plan's automated block.
- **Wallet chip polls every 30_000 ms via TanStack Query** — server prehydrates via RSC Header; client polls via `refetchInterval: 30_000, staleTime: 20_000`. Tested by source-file grep (structural guard against future refactor dropping the constant).
- **Stripe structurally absent from /checkout/confirm** — Pitfall 5 preserved for B2B; the page imports nothing from `@stripe/*`, mounts no `<Elements>`, and the CSP header for the route has no `js.stripe.com` entry. B2B is internal ledger only.
- **/checkout/success defensive guard against `?payment_intent=`** — Pitfall 6; B2B never mounts Elements so the presence of that parameter signals misrouting or tampering. RSC redirects to `/dashboard?error=unexpected_payment_intent`. The defensive guard mentions the token in source, overriding the plan's `! grep payment_intent` assertion (documented as deviation).

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

Plan Plan 05-03-Task-3 (the `/admin/wallet` portal surface — 13-file Next.js scope; see 05-03-SUMMARY.md "Deferred Work") as its own standalone plan via `/gsd-plan`, then `/gsd-execute-phase 05` to land it. In parallel, `/gsd-execute-phase 05` can also advance to Plan 05-04 (agency invoice PDF GROSS-only + IDOR gates) — 05-04 is not dependency-blocked by the deferred 05-03-Task-3 portal surface. Both consume the `B2BAdminPolicy` + `B2BPolicy` + `/api/wallet/*` controller shipped in 05-03 Tasks 1+2 and the saga B2B branch shipped in 05-02.

**Pre-deploy gates for Plan 05-03 (Tasks 1+2):**

- Apply the new EF Core migration before deploy: `20260525000000_AddAgencyWallet` (PaymentDbContext). Creates `payment.AgencyWallets` with `UNIQUE(AgencyId)`. No seed data — agency rows are inserted on first `PUT /api/wallet/threshold` or via the MERGE upsert in `AgencyWalletRepository.SetThresholdAsync`.
- Populate `KeycloakB2B:ClientSecret` env var (`KeycloakB2B__ClientSecret` in ASP.NET Core env-binding) on PaymentService. Without it the `WalletLowBalanceConsumer` cannot resolve agent-admin recipients and falls back to logging a warning while still flipping the `LowBalanceEmailSent` flag (dev/test non-regression, production-blocker for the advisory flow).
- Create a Keycloak `tbe-b2b` realm client `payment-service` with service-account enabled + `view-users` + `view-clients` + `query-users` realm-management roles. Populate `KeycloakB2B:ClientSecret` from the generated client-credential.
- `Wallet__LowBalance__DefaultThreshold` / `__EmailCooldownHours` / `__PollIntervalMinutes` env vars override the 500 GBP / 24h / 15-min defaults. `IOptionsMonitor` re-reads on every tick + every top-up call so changes apply without a restart.

**Pre-deploy gates for Plan 05-02 (still apply):**

- Apply the two EF Core migrations: `20260416000000_AddAgencyMarkupRules` (PricingDbContext) and `20260520000000_AddB2BBookingColumns` (BookingDbContext). Seed rules for test agencies ship in the pricing migration.
- Plan 05-01's `verify-audience-smoke-b2b.sh` gate still applies (unchanged).

After Phase 5, Phase 4 still has plans 04-03 / 04-04 / 04-05 staged (hotel booking, multi-product baskets, mobile E2E) — not blocked by Phase 5 but remaining backlog for the B2C portal.

## Open Human Actions

- **Plan 05-01 pre-deploy gate (blocks gateway rollout)** — Import `infra/keycloak/realm-tbe-b2b.json` into the target-env Keycloak (Realms → Add realm → Import). Populate `KEYCLOAK_B2B_ISSUER`, `KEYCLOAK_B2B_CLIENT_ID`, `KEYCLOAK_B2B_CLIENT_SECRET`, `KEYCLOAK_B2B_ADMIN_CLIENT_ID`, `KEYCLOAK_B2B_ADMIN_CLIENT_SECRET` in `src/portals/b2b-web/.env.local` (and the deployment env). Create a test `agent-admin` user with `agency_id` user attribute populated (GUID). Run `bash infra/keycloak/verify-audience-smoke-b2b.sh` from repo root — MUST exit 0 before the gateway `ValidateAudience=true` change ships to that env. Rollback: set `ValidateAudience = false` in Program.cs + redeploy.
- **Plan 05-03 pre-deploy gate (blocks low-balance advisory flow)** — Create the `payment-service` client in the `tbe-b2b` Keycloak realm with client-credentials grant type enabled and assign the realm-management roles `view-users` + `query-users` (required by `KeycloakB2BAdminClient.GetAgentAdminsForAgencyAsync`). Populate `KeycloakB2B__ClientSecret` env var on PaymentService from the generated client secret.
- **Plan 05-03 Task 3 follow-up plan** — still TODO. `/gsd-plan` a focused plan covering the 15 open acceptance criteria in 05-03-SUMMARY.md "Deferred Work" (`/admin/wallet` RSC + Stripe Elements + transactions table + threshold dialog + sitewide low-balance banner + RequestTopUpLink + route-scoped CSP narrowing + insufficient-funds-panel retrofit + vitest specs). Mitigates remaining STRIDE threats T-05-03-06 (CSP leak), T-05-03-09 (mailto session-leak).
- **Plan 05-03 prerequisite (already honoured by appsettings.json defaults)** — `Wallet__TopUp__MinAmount` / `Wallet__TopUp__MaxAmount` env vars override the £10 / £50,000 defaults.
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
