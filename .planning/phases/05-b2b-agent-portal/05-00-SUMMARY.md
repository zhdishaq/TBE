---
phase: 05-b2b-agent-portal
plan: 00
subsystem: infra
tags: [b2b, portal, scaffold, keycloak, vitest, playwright, xunit, red-placeholder, wave-0, csp]

# Dependency graph
requires:
  - phase: 04-passengers-extras
    provides: "src/portals/b2c-web/ scaffold + Auth.js v5 split config + Keycloak admin SA pattern + Stripe memoisation"
provides:
  - "src/portals/b2b-web/ Next.js 16.1.6 portal fork on port 3001 with indigo-600 accent + <AgentPortalBadge /> (D-42)"
  - "Per-route CSP: Stripe allowed only on /admin/wallet/* (standardCsp omits js.stripe.com entirely — T-05-00-03)"
  - "Auth.js v5 config forked with KEYCLOAK_B2B_* env vars and cookie __Secure-tbe-b2b.session-token (Pitfall 19)"
  - "TBE.Contracts.Enums.Channel { B2C=0, B2B=1 } (shared contract) — unblocks Plan 05-02 saga branch"
  - "infra/keycloak/realm-tbe-b2b.json (realm tbe-b2b + tbe-agent-portal client + tbe-b2b-admin SA + 3 roles + audience/agency-id mappers)"
  - "infra/keycloak/verify-audience-smoke-b2b.sh (fails-closed on unset env; exit codes 0/1/2 documented)"
  - "Vitest + Playwright harness under b2b-web (smoke.test.tsx green; e2e/smoke.spec.ts + e2e/fixtures/auth.ts skeletons)"
  - "22 red-placeholder xUnit tests (Trait Category=RedPlaceholder) across BookingService/Pricing/Payments/Notifications — baseline CI green via filter Category!=RedPlaceholder"
  - "VALIDATION.md Per-Task Verification Map populated with 28 rows (6 Wave 0 infra + 22 red placeholders); wave_0_complete=true; nyquist_compliant=true"
affects: [05-01-agent-onboarding, 05-02-wallet-saga, 05-03-wallet-topup, 05-04-agency-invoice]

# Tech tracking
tech-stack:
  added:
    - "Vitest 3.x + @testing-library/jest-dom (portal unit tests)"
    - "Playwright 1.49.x (portal e2e)"
    - "MassTransit.TestFramework 9.1.0 (Pricing.Tests)"
    - "EF Core InMemory 9.0.1 (Pricing.Tests)"
  patterns:
    - "Red-placeholder test pattern: [Trait(\"Category\",\"RedPlaceholder\")] + Assert.Fail(\"MISSING — Plan XX-YY Task Z ...\") — compiles but fails, excluded from baseline CI"
    - "Per-route CSP via Next.js matcher (walletCsp on /admin/wallet/:path*, standardCsp on /:path*) — tighter than b2c (T-05-00-03)"
    - "Auth.js v5 cookie-per-portal naming (__Secure-tbe-b2b.session-token) — prevents cross-portal session leakage (Pitfall 19)"
    - "Portal isolation via separate Node processes (b2c:3000 / b2b:3001) with distinct Keycloak realms (D-32 no OIDC brokering)"

key-files:
  created:
    - "src/portals/b2b-web/** (112 files, 22984 insertions; 77 components/ui/** byte-for-byte equal to b2c-web)"
    - "src/portals/b2b-web/components/layout/agent-portal-badge.tsx (indigo-600 outline pill, aria-label='Agent portal')"
    - "src/portals/b2b-web/next.config.mjs (per-route CSP; walletCsp isolates Stripe)"
    - "src/portals/b2b-web/lib/keycloak-b2b-admin.ts (SA token cache, Node-only guard; NO createSubAgent yet — Plan 01)"
    - "src/portals/b2b-web/vitest.config.ts + tests/setup.ts + tests/smoke.test.tsx (green in 5.13s)"
    - "src/portals/b2b-web/playwright.config.ts + e2e/smoke.spec.ts + e2e/fixtures/auth.ts (skeleton)"
    - "src/shared/TBE.Contracts/Enums/Channel.cs (enum Channel : int { B2C=0, B2B=1 })"
    - "infra/keycloak/realm-tbe-b2b.json (realm delta; tbe-agent-portal client redirectUri http://localhost:3001/*)"
    - "infra/keycloak/verify-audience-smoke-b2b.sh (mode 100755; exit 2 verified on unset env)"
    - "tests/BookingService.Tests/AgentBookingsControllerTests.cs (4 red: D-34, Pitfall 26, IDOR, D-37)"
    - "tests/BookingService.Tests/BookingSagaB2BChannelTests.cs (2 red: Channel default, BookingInitiated contract)"
    - "tests/BookingService.Tests/BookingSagaB2BBranchTests.cs (3 red: B2B WalletReserve, B2C Authorize, D-39 pre-ticket compensation)"
    - "tests/Pricing.Tests/PricingService.Tests.csproj (new project)"
    - "tests/Pricing.Tests/ApplyMarkupTests.cs (3 red: D-36 override, D-36 fallback, D-41 v1)"
    - "tests/Pricing.Tests/AgencyPriceRequestedConsumerTests.cs (1 red: Pitfall 23 server-side markup)"
    - "tests/Payments.Tests/WalletTopUpCapsTests.cs (3 red: D-40 min/max + problem+json shape)"
    - "tests/Notifications.Tests/AgencyInvoiceDocumentTests.cs (3 red: D-43 GROSS only + substring negative + conditional VAT)"
    - "tests/Notifications.Tests/AgencyInvoiceControllerTests.cs (3 red: Pitfall 26 403, Pitfall 28 401, application/pdf shape)"
  modified:
    - "infra/keycloak/README.md (appended ## tbe-b2b realm section)"
    - ".planning/phases/05-b2b-agent-portal/05-VALIDATION.md (28 rows populated; wave_0_complete=true)"

key-decisions:
  - "Portal scaffolding approach: full-fork of b2c-web (no shared runtime package yet) — keeps Phase 05 blast radius contained; shared UI lives in byte-identical components/ui/"
  - "Test harness split: Vitest (portal unit) + Playwright (portal e2e) + xUnit red placeholders (backend) — all three runnable in CI via filter `Category!=RedPlaceholder`"
  - "Red placeholders compile against the shared TBE.Contracts.Enums.Channel enum but use `_ = Channel.B2C;` + Assert.Fail bodies so runtime type conflicts in BookingSagaState.Channel (still string at Wave 0) never trip"
  - "PricingService.Tests is a NEW xUnit project (no existing test project under services/PricingService/) — standalone, no .sln entry needed (repo has no .sln)"
  - "Chose `tests/Payments.Tests/` (existing directory; RootNamespace=Payments.Tests) over `tests/PaymentService.Tests/` referenced in plan — Rule 3 deviation, used actual path"

patterns-established:
  - "Red-placeholder: compiles + appears in `dotnet test --filter Category=RedPlaceholder` + fails with structured MISSING message pointing at owning plan+task"
  - "Per-route CSP: Next.js headers() returning [{ source: '/admin/wallet/:path*', headers: walletCsp }, { source: '/:path*', headers: standardCsp }] — order matters, narrow route first"
  - "Keycloak smoke script exit-code contract: 0=pass, 1=audience mismatch, 2=env unset (fails-closed; documented in README tables)"

requirements-completed: []  # Wave 0 is an enablement plan — owns no B2B-NN requirement directly; unblocks all of B2B-01..B2B-10

# Metrics
duration: ~4h (including planning re-read + hook retries)
completed: 2026-04-17
---

# Phase 05 Plan 00: Wave 0 — B2B Portal Scaffold + Red Placeholder Harness Summary

**Forked b2c-web into b2b-web with realm/port/CSP deltas, staged the tbe-b2b Keycloak realm delta, seeded 22 red-placeholder xUnit tests across four services, and populated the VALIDATION map — Wave 0 unblocks Plans 05-01..05-04 without breaking the CI baseline.**

## Performance

- **Duration:** ~4h wall-clock (including planner re-reads and hook retries)
- **Started:** 2026-04-17T13:25:00Z (after /gsd-execute-phase init)
- **Completed:** 2026-04-17T14:17:00Z
- **Tasks:** 3/3 complete
- **Files created:** 121 (portal 112 + enum 1 + keycloak 2 + xUnit 8 incl. new .csproj + VALIDATION map update)
- **Files modified:** 2 (infra/keycloak/README.md, 05-VALIDATION.md)
- **Lines inserted:** ~23,800 across three commits

## Accomplishments

- **b2b-web portal boots cleanly on :3001** with Next 16.1.6 Turbopack (9.5s compile) and serves a placeholder landing page rendering `<AgentPortalBadge />` (indigo-600 outline pill, aria-label="Agent portal"). `pnpm --filter tbe-b2b-web test` passes the smoke test in 5.13s.
- **UI components are byte-for-byte identical** between b2b-web and b2c-web: `diff -r src/portals/b2c-web/components/ui src/portals/b2b-web/components/ui` exits 0 across 77 files. Pitfall 17 / T-05-00-06 mitigation verified.
- **CSP hardened vs b2c**: `next.config.mjs` uses per-route headers — `walletCsp` permits `js.stripe.com` on `/admin/wallet/:path*` only; `standardCsp` on `/:path*` omits Stripe entirely. T-05-00-03 satisfied.
- **Keycloak tbe-b2b realm delta staged** as a patch file (not a full export) with tbe-agent-portal client (redirectUri http://localhost:3001/*), tbe-api-audience mapper, agency-id-attribute mapper (multivalued=false per D-33), tbe-b2b-admin service account, and three realm roles (agent, agent-admin, agent-readonly). Smoke script `verify-audience-smoke-b2b.sh` fails-closed on unset env (exit=2 verified).
- **Shared Channel enum** (`src/shared/TBE.Contracts/Enums/Channel.cs`) compiles into TBE.Contracts.dll and is referenced from Wave 0 red placeholders via `using TBE.Contracts.Enums;`.
- **22 red-placeholder xUnit tests** compile and run, distributed across BookingService.Tests (9), PricingService.Tests (4, new project), Payments.Tests (3), Notifications.Tests (6). All have `[Trait("Category","RedPlaceholder")]` and structured MISSING messages pointing at the owning plan/task. Baseline CI remains green.
- **VALIDATION.md Per-Task Verification Map** populated with 28 rows (6 Wave 0 infra + 22 red placeholders), each mapping to a concrete `dotnet test --filter` or pnpm command. `wave_0_complete: true` and `nyquist_compliant: true` flipped in frontmatter.

## Task Commits

Each task was committed atomically:

1. **Task 1: Fork b2c-web scaffold → b2b-web with realm/port/CSP deltas** — `8b8a376` (feat)
   - 112 files, +22,984 lines
2. **Task 3: Stage tbe-b2b realm delta + audience smoke + Channel enum** — `d5d1f35` (feat)
   - 4 files, +249 lines
   - _Note: Tasks 2 and 3 committed out of numerical order. Task 3 depends only on b2c-web patterns (already available after Task 1); committing it before finishing all 9 xUnit files meant the Channel enum was available for the red placeholders that `using TBE.Contracts.Enums;`._
3. **Task 2: Install test harness + 22 red placeholders + VALIDATION map update** — `64ff67c` (test)
   - 16 files, +532 lines, -17 lines

**Plan metadata:** _to be added via final docs commit (STATE.md + ROADMAP.md + 05-00-SUMMARY.md)_

## Files Created/Modified

See frontmatter `key-files.created` and `key-files.modified` for the authoritative list.

**Volume by commit:**

| Commit | Files | Insertions | Focus |
|--------|-------|------------|-------|
| `8b8a376` | 112 | +22,984 | Portal runtime scaffold |
| `d5d1f35` | 4 | +249 | Keycloak + contracts |
| `64ff67c` | 16 | +532 (-17) | Test harness + VALIDATION |

## Red-Placeholder Coverage

| Service | File | Red Facts | Owning Plan |
|---------|------|-----------|-------------|
| BookingService | AgentBookingsControllerTests.cs | 4 | 05-01 |
| BookingService | BookingSagaB2BChannelTests.cs | 2 | 05-02 |
| BookingService | BookingSagaB2BBranchTests.cs | 3 | 05-02 |
| Pricing | ApplyMarkupTests.cs | 3 | 05-02 |
| Pricing | AgencyPriceRequestedConsumerTests.cs | 1 | 05-02 |
| Payments | WalletTopUpCapsTests.cs | 3 | 05-03 |
| Notifications | AgencyInvoiceDocumentTests.cs | 3 | 05-04 |
| Notifications | AgencyInvoiceControllerTests.cs | 3 | 05-04 |
| **TOTAL** | **8 files** | **22 facts** | — |

**Verification:**
- `dotnet test ... --filter "Category=RedPlaceholder"` → 22 failing (9 + 4 + 3 + 6 across the four .csproj projects)
- `dotnet test ... --filter "Category!=RedPlaceholder&Category!=Integration"` → 26 + 0 (new project) + 13 + 12 = **51 passing, 0 failing** (baseline green)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Directory naming] Used `tests/Payments.Tests/` instead of `tests/PaymentService.Tests/`**
- **Found during:** Task 2 (xUnit test file placement)
- **Issue:** Plan frontmatter and acceptance criteria reference `tests/PaymentService.Tests/WalletTopUp*Tests.cs`, but the actual existing test project directory is `tests/Payments.Tests/` with `RootNamespace=Payments.Tests` and existing siblings (e.g., `StripeWebhookHandlerTests.cs`).
- **Fix:** Placed `WalletTopUpCapsTests.cs` at `tests/Payments.Tests/WalletTopUpCapsTests.cs` with `namespace Payments.Tests;` to keep the existing convention. VALIDATION.md rows reference this actual path.
- **Files modified:** `tests/Payments.Tests/WalletTopUpCapsTests.cs`
- **Commit:** `64ff67c`

**2. [Rule 1 - Bug] Fixed XML-comment double-dash in PricingService.Tests.csproj**
- **Found during:** Task 2 (first build of new Pricing.Tests project)
- **Issue:** MSBuild error `MSB4025: An XML comment cannot contain '--'` — an XML comment inside the .csproj used a literal string `Category!=RedPlaceholder` and `dotnet test --filter`, which contain `--` and broke the comment.
- **Fix:** Rewrote the comment to avoid `--` (`"(dotnet test with Category!=RedPlaceholder filter)"` split across lines).
- **Files modified:** `tests/Pricing.Tests/PricingService.Tests.csproj`
- **Commit:** `64ff67c`

**3. [Rule 3 - Namespace collision] BookingSagaState.Channel type conflict**
- **Found during:** Task 2 (designing BookingSagaB2BChannelTests)
- **Issue:** Existing `BookingSagaState.Channel` is a `string` property; importing `TBE.Contracts.Enums.Channel` directly into the test class would create an unqualified name conflict.
- **Fix:** Because tests are red placeholders whose bodies only need to COMPILE (not run), added `_ = Channel.B2C;` to reference the enum so `using TBE.Contracts.Enums;` is not removed by analyzers, and kept the body as `Assert.Fail(...)`. The namespace conflict never executes at runtime.
- **Files modified:** `tests/BookingService.Tests/BookingSagaB2BChannelTests.cs`
- **Commit:** `64ff67c`

### Grep Contract Adjustments

**4. [Rule 3 - Grep false positive] Reworded docstring to avoid matching `! grep -q "export.*createSubAgent"`**
- **Found during:** Task 1 (post-scaffold acceptance check)
- **Issue:** The acceptance criterion string itself contained `export.*createSubAgent` inside a comment, causing the grep to match its own docstring instead of real code.
- **Fix:** Reworded the inline comment in `keycloak-b2b-admin.ts` from quoting the pattern to the plain-English "NO sub-agent creation helper yet — Plan 05-01 Task 2 adds it."
- **Files modified:** `src/portals/b2b-web/lib/keycloak-b2b-admin.ts`
- **Commit:** `8b8a376`

### Commit Ordering

Tasks 2 and 3 were committed out of numerical order (Task 1 → Task 3 → Task 2). Task 3 had no dependency on Task 2's test-harness work, and committing the Channel enum first meant the xUnit red placeholders in Task 2 could `using TBE.Contracts.Enums;` and compile in a single pass. Not a deviation from plan intent — plan does not mandate strict commit order for Wave 0.

## Authentication Gates

None encountered during execution. All three tasks completed autonomously. **Human actions deferred to Plan 05-01**:

- Import `infra/keycloak/realm-tbe-b2b.json` into local Keycloak (Keycloak admin → Realms → Add realm → Import)
- Populate `KEYCLOAK_B2B_*` env vars in `src/portals/b2b-web/.env.local` from the admin UI
- Run `bash infra/keycloak/verify-audience-smoke-b2b.sh` to confirm `aud=tbe-api` on an access token (exit 0 = ready for Plan 01)
- Populate `agency_id` attribute on a test agent user (Keycloak admin → Users → Attributes)

These are documented in `infra/keycloak/README.md` under `## tbe-b2b realm (Phase 5 Plan 05-00)`.

## Known Stubs

- **`<AgentPortalBadge />` is rendered from `app/layout.tsx` placeholder header** — the landing page (`app/page.tsx`) currently shows a minimal "TBE Agent Portal" heading + sign-in prompt. Wiring the real dashboard lives in Plan 05-01 (agent onboarding) and Plan 05-02 (booking list). **Intentional stub.**
- **`src/portals/b2b-web/e2e/fixtures/auth.ts`** — `signInAsAgent / signInAsAgentAdmin / signInAsAgentReadonly` all throw `"Wave 0 skeleton — implemented by Plan 05-01 Task 5"`. **Intentional stub** (Playwright e2e tests assume these fixtures exist; Plan 05-01 wires them to real Keycloak login flow).
- **`src/portals/b2b-web/lib/keycloak-b2b-admin.ts`** — exports `getServiceAccountToken()` + `adminApiBase()` + `__resetServiceAccountTokenCacheForTests()` only. **No `createSubAgent` helper yet** — intentionally deferred to Plan 05-01 Task 2 (plan explicitly reserves this path).
- **All 22 red-placeholder xUnit tests** fail with `Assert.Fail("MISSING — Plan 05-0X Task Y ...")`. **Intentional stubs** (this IS the deliverable — they become green as Plans 01–04 implement the behaviour).

## Threat Flags

None introduced. All new surface area (b2b portal :3001, tbe-b2b Keycloak realm, wallet top-up endpoint shape, agency-scoped invoice PDF endpoint) is either covered by the existing `05-CONTEXT.md` threat model or explicitly deferred to the owning plan's `<threat_model>` block (Plan 05-01 agency_id claim gating, Plan 05-03 wallet double-spend, Plan 05-04 invoice IDOR).

## TDD Gate Compliance

Plan 05-00 is `type: execute`, NOT `type: tdd`. The red-placeholder pattern used here is a DELIBERATE RED phase for Plans 01–04 — `test(...)` commit 64ff67c is the RED gate for the phase as a whole. Each owning plan (05-01..05-04) is responsible for the GREEN gate on its respective placeholders.

## Open Items for Next Plan (05-01)

- [ ] Import `infra/keycloak/realm-tbe-b2b.json` into local Keycloak
- [ ] Run `verify-audience-smoke-b2b.sh` (expect exit 0)
- [ ] Populate `KEYCLOAK_B2B_*` env vars in `.env.local`
- [ ] Add `createSubAgent()` helper to `src/portals/b2b-web/lib/keycloak-b2b-admin.ts`
- [ ] Implement `AgentBookingsController` → turn the 4 red placeholders in BookingService.Tests/AgentBookingsControllerTests.cs green
- [ ] Wire real Keycloak login flow into `src/portals/b2b-web/e2e/fixtures/auth.ts`

## Self-Check: PASSED

**Files verified present on disk:**
- `src/portals/b2b-web/package.json` — FOUND
- `src/portals/b2b-web/components/layout/agent-portal-badge.tsx` — FOUND
- `src/portals/b2b-web/vitest.config.ts` — FOUND
- `src/portals/b2b-web/playwright.config.ts` — FOUND
- `src/shared/TBE.Contracts/Enums/Channel.cs` — FOUND
- `infra/keycloak/realm-tbe-b2b.json` — FOUND
- `infra/keycloak/verify-audience-smoke-b2b.sh` — FOUND (mode 100755)
- `tests/BookingService.Tests/AgentBookingsControllerTests.cs` — FOUND (4 Facts)
- `tests/BookingService.Tests/BookingSagaB2BChannelTests.cs` — FOUND (2 Facts)
- `tests/BookingService.Tests/BookingSagaB2BBranchTests.cs` — FOUND (3 Facts)
- `tests/Pricing.Tests/PricingService.Tests.csproj` — FOUND (builds clean)
- `tests/Pricing.Tests/ApplyMarkupTests.cs` — FOUND (3 Facts)
- `tests/Pricing.Tests/AgencyPriceRequestedConsumerTests.cs` — FOUND (1 Fact)
- `tests/Payments.Tests/WalletTopUpCapsTests.cs` — FOUND (3 Facts)
- `tests/Notifications.Tests/AgencyInvoiceDocumentTests.cs` — FOUND (3 Facts)
- `tests/Notifications.Tests/AgencyInvoiceControllerTests.cs` — FOUND (3 Facts)

**Commits verified in git log:**
- `8b8a376` — FOUND (feat(05-00): fork b2c-web scaffold into b2b-web with realm/port/CSP deltas)
- `d5d1f35` — FOUND (feat(05-00): stage tbe-b2b realm delta + audience smoke + Channel enum)
- `64ff67c` — FOUND (test(05-00): install test harness and stage 22 red-placeholder tests)

**Build/test verification:**
- `dotnet build` across all 4 test .csproj — PASSED (0 Warning, 0 Error each)
- `dotnet test ... --filter "Category!=RedPlaceholder&Category!=Integration"` — 26 + 13 + 12 = 51 passing (BookingService/Payments/Notifications), Pricing.Tests has no non-red tests
- `dotnet test ... --filter "Category=RedPlaceholder"` — 9 + 4 + 3 + 6 = 22 failing (all four projects)
- `pnpm --filter tbe-b2b-web test` — smoke.test.tsx GREEN in 5.13s
- `diff -r src/portals/b2c-web/components/ui src/portals/b2b-web/components/ui` — exit 0 (77 files identical)
- `bash infra/keycloak/verify-audience-smoke-b2b.sh` (env unset) — exit 2 (fails-closed as designed)

Wave 0 deliverables satisfy every `must_haves.truths` bullet in 05-00-PLAN.md.
