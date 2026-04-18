---
phase: 05-b2b-agent-portal
plan: 05
subsystem: payment-service+b2b-web
tags: [b2b, admin-wallet, stripe-elements, route-scoped-csp, threshold-editor, top-up, transactions-ledger, low-balance-banner, request-top-up-link, pitfall-5, pitfall-28, d-40, d-44, t-05-03-06, t-05-03-09, t-05-05-02, t-05-05-03, t-05-05-05, b2b-06, b2b-07, complete]
requires:
  - 05-00
  - 05-01
  - 05-02
  - 05-03
provides:
  - B2BWalletController.UpdateThresholdAsync (PUT /api/wallet/threshold)
  - UpdateThresholdRequest DTO (agencyId intentionally omitted — Pitfall 28 structural defence)
  - B2BWalletController.GetThreshold reads IAgencyWalletRepository (was hard-coded £500)
  - b2b-web /admin/wallet RSC (three-section D-44 compact surface)
  - b2b-web TopUpForm (Stripe PaymentElement + zod £10–£50 000 + problem+json parser)
  - b2b-web ThresholdDialog (Radix Dialog + zod £50–£10 000 + TanStack mutation + invalidate)
  - b2b-web TransactionsTable (page-number 20/50/100 + SignedAmount tint + tabular-nums)
  - b2b-web lib/stripe.ts (memoised loadStripe singleton — Pitfall 5)
  - b2b-web WalletPaymentElementWrapper (<Elements> boundary for PaymentElement)
  - b2b-web route-scoped CSP via next.config.mjs headers() (/admin/wallet/:path*)
  - b2b-web route handlers — POST /api/wallet/top-up/intent, PUT /api/wallet/threshold, GET /api/wallet/transactions
  - b2b-web LowBalanceBanner (role=status, aria-live=polite, sessionStorage dismiss)
  - b2b-web RequestTopUpLink (subject-only mailto, zero session material — T-05-03-09)
  - b2b-web InsufficientFundsPanel retrofit (non-admin delegates to RequestTopUpLink)
  - b2b-web WalletChip queryKey migrated to array form ['wallet','balance']
  - b2b-web (portal)/layout.tsx mounts sitewide LowBalanceBanner
affects:
  - src/services/PaymentService/PaymentService.API/Controllers/WalletController.cs
  - tests/Payments.Tests/B2BWalletControllerThresholdTests.cs
  - tests/Payments.Tests/WalletControllerTopUpTests.cs
  - src/portals/b2b-web/lib/stripe.ts
  - src/portals/b2b-web/components/wallet/wallet-payment-element-wrapper.tsx
  - src/portals/b2b-web/components/wallet/low-balance-banner.tsx
  - src/portals/b2b-web/components/wallet/request-top-up-link.tsx
  - src/portals/b2b-web/components/wallet/wallet-chip.tsx
  - src/portals/b2b-web/components/checkout/insufficient-funds-panel.tsx
  - src/portals/b2b-web/app/(portal)/layout.tsx
  - src/portals/b2b-web/app/(portal)/admin/wallet/page.tsx
  - src/portals/b2b-web/app/(portal)/admin/wallet/top-up-form.tsx
  - src/portals/b2b-web/app/(portal)/admin/wallet/threshold-dialog.tsx
  - src/portals/b2b-web/app/(portal)/admin/wallet/transactions-table.tsx
  - src/portals/b2b-web/app/api/wallet/top-up/intent/route.ts
  - src/portals/b2b-web/app/api/wallet/threshold/route.ts
  - src/portals/b2b-web/app/api/wallet/transactions/route.ts
  - src/portals/b2b-web/next.config.mjs
tech-stack:
  added:
    - "@stripe/stripe-js (memoised loadStripe singleton — module scope)"
    - "@stripe/react-stripe-js (<Elements> + <PaymentElement/>)"
  patterns:
    - "Route-scoped CSP via Next.js headers() with /admin/wallet/:path* matcher vs default /((?!admin/wallet).*) — SAQ-A scope preservation (Pitfall 5)"
    - "Memoised loadStripe at module scope — single Stripe.js download for the whole tab (Pitfall 5)"
    - "RFC 7807 application/problem+json — hand-serialised ContentResult with allowedRange and requested fields, parsed client-side into user copy"
    - "TanStack Query shared cache keys — ['wallet','balance'] + ['wallet','threshold'] + ['wallet','transactions',{page,size}] — WalletChip + LowBalanceBanner + /admin/wallet hit the SAME entry (zero duplicate fetches)"
    - "HydrationBoundary + dehydrate on RSC — /admin/wallet server-prefetches all three keys via gatewayFetch, client receives hydrated cache (no fetch-on-mount flicker)"
    - "D-40 server-side range guards — Pitfall 28-safe (JWT agency_id, body agencyId ignored at DTO layer)"
    - "D-44 page-number pagination — ?page=&size= with 20/50/100 selector (default 20) — consistent with /bookings"
    - "SessionStorage-only dismiss for LowBalanceBanner — localStorage FORBIDDEN (T-05-05-05 re-arms on new tab)"
    - "role=status+aria-live=polite for passive banner vs role=alert reserved for the blocking InsufficientFundsPanel (UI-SPEC §11 lines 418/559/628)"
key-files:
  created:
    - src/portals/b2b-web/lib/stripe.ts
    - src/portals/b2b-web/components/wallet/wallet-payment-element-wrapper.tsx
    - src/portals/b2b-web/components/wallet/low-balance-banner.tsx
    - src/portals/b2b-web/components/wallet/request-top-up-link.tsx
    - src/portals/b2b-web/app/(portal)/admin/wallet/page.tsx
    - src/portals/b2b-web/app/(portal)/admin/wallet/top-up-form.tsx
    - src/portals/b2b-web/app/(portal)/admin/wallet/threshold-dialog.tsx
    - src/portals/b2b-web/app/(portal)/admin/wallet/transactions-table.tsx
    - src/portals/b2b-web/app/api/wallet/top-up/intent/route.ts
    - src/portals/b2b-web/app/api/wallet/threshold/route.ts
    - src/portals/b2b-web/app/api/wallet/transactions/route.ts
    - tests/Payments.Tests/B2BWalletControllerThresholdTests.cs
    - src/portals/b2b-web/tests/csp-route-scoping.test.ts
    - src/portals/b2b-web/tests/route-wallet-top-up-intent.test.ts
    - src/portals/b2b-web/tests/route-wallet-threshold.test.ts
    - src/portals/b2b-web/tests/route-wallet-transactions.test.ts
    - src/portals/b2b-web/tests/components/wallet/wallet-payment-element-wrapper.test.tsx
    - src/portals/b2b-web/tests/components/wallet/low-balance-banner.test.tsx
    - src/portals/b2b-web/tests/components/wallet/request-top-up-link.test.tsx
    - src/portals/b2b-web/tests/components/admin/wallet-top-up-form.test.tsx
    - src/portals/b2b-web/tests/components/admin/threshold-dialog.test.tsx
    - src/portals/b2b-web/tests/components/admin/transactions-table.test.tsx
  modified:
    - src/services/PaymentService/PaymentService.API/Controllers/WalletController.cs
    - tests/Payments.Tests/WalletControllerTopUpTests.cs
    - src/portals/b2b-web/next.config.mjs
    - src/portals/b2b-web/app/(portal)/layout.tsx
    - src/portals/b2b-web/components/wallet/wallet-chip.tsx
    - src/portals/b2b-web/components/checkout/insufficient-funds-panel.tsx
    - src/portals/b2b-web/tests/components/checkout/insufficient-funds-panel.test.tsx
decisions:
  - "UpdateThresholdRequest DTO OMITS agencyId property entirely (structural Pitfall 28 defence) — the JWT agency_id claim is the ONLY source of truth for the tenant. Attacker-supplied JSON agencyId cannot even bind into the action signature."
  - "B2BWalletController.GetThreshold now reads IAgencyWalletRepository.GetAsync rather than returning the hard-coded £500 default (fixes a latent bug — T-05-05-03 IDOR guard already in place server-side, but the client would have seen a stale £500 before any threshold update)."
  - "OutOfRangeProblem() helper folds shape violations (non-positive, bad currency) into the SAME application/problem+json payload shape as D-40 range violations (allowedRange.min/.max/.currency + requested) — unified client error handling."
  - "Single TanStack cache key for wallet balance across the entire portal: WalletChip queryKey migrated from ['wallet-balance'] (dash legacy) to ['wallet','balance'] (array) so the chip + sitewide LowBalanceBanner hit ONE cache entry — zero duplicate fetches per 30s poll."
  - "SessionStorage-only dismiss for LowBalanceBanner (T-05-05-05) — explicitly not cross-tab. A fresh tab re-arms the banner, which is the intended UX per UI-SPEC §11 (actionable-tone: the banner re-announces on new sessions until the balance crosses the threshold)."
  - "role=status + aria-live=polite on LowBalanceBanner (NOT role=alert). UI-SPEC §11 lines 418/559/628 reserve role=alert for the blocking InsufficientFundsPanel in the checkout flow. A sitewide nag using role=alert would be assistive-tech spam."
  - "Subject-only mailto in RequestTopUpLink — hard-coded 'Top-up request' subject string with NO interpolation. A future refactor cannot accidentally sneak session material (agency_id, token, balance, threshold) into the href via string concatenation — T-05-03-09 codified at the code level."
  - "InsufficientFundsPanel non-admin retrofit — the old inline mailto interpolated the booking GROSS into the subject (leaked the booking amount to the user's default mail client). The retrofit delegates entirely to <RequestTopUpLink/> so the CTA carries ZERO session material."
  - "Stripe PaymentElement wrapper rendered ONLY inside /admin/wallet route (Pitfall 5). The route-scoped CSP at /admin/wallet/:path* allows js.stripe.com / hooks.stripe.com / api.stripe.com; the default matcher /((?!admin/wallet).*) omits those origins — Stripe.js physically cannot load on customer B2C or other B2B routes, which preserves SAQ-A scope."
  - "Radix Dialog import via the project's @/components/ui/dialog shell (which re-exports from the radix-ui barrel) rather than the raw @radix-ui/react-dialog package. Matches the project's existing pattern from /bookings void confirmation dialog."
metrics:
  duration: "~9h (Tasks 1–5 + SUMMARY)"
  completed: 2026-04-18
---

# Phase 05 Plan 05: /admin/wallet + Threshold Editor + Sitewide Low-Balance Banner Summary

**One-liner:** Closes out Plan 05-03 Task 3 (deferred): ships the complete `/admin/wallet` portal surface (top-up form with Stripe Elements, transactions ledger, threshold editor), adds PUT `/api/wallet/threshold` backend with D-40 range guard + Pitfall 28 structural defence, installs a route-scoped CSP so Stripe.js loads ONLY on `/admin/wallet`, mounts a sitewide `LowBalanceBanner` on every authenticated portal page, and retrofits 05-02's `InsufficientFundsPanel` to use a zero-leak mailto primitive — realising B2B-06 (top-up) and B2B-07 (threshold self-service) end-to-end through the idempotent commit path built in Plan 05-03.

## Task Completion Log

| Task | Gate   | Commit  | Summary                                                                                                                 |
| ---- | ------ | ------- | ----------------------------------------------------------------------------------------------------------------------- |
| 1    | RED    | 7ee34d6 | Failing tests: csp-route-scoping + wallet-payment-element-wrapper (Stripe bootstrap + route-scoped CSP)                 |
| 1    | GREEN  | dec6510 | lib/stripe.ts memoised singleton + WalletPaymentElementWrapper + next.config.mjs route-scoped CSP                       |
| 2    | RED    | 1060da3 | Failing tests: route-wallet-top-up-intent + route-wallet-threshold + route-wallet-transactions                          |
| 2    | GREEN  | 6a21f5b | Three Node-runtime route handlers with Auth.js Bearer forwarding + 403 on missing agency_id                             |
| 3    | RED    | c289e24 | Failing tests: B2BWalletControllerThresholdTests (happy + Pitfall 28 + 403 + 4× problem+json)                           |
| 3    | GREEN  | 3bda32a | B2BWalletController.UpdateThresholdAsync + OutOfRangeProblem helper + GetThreshold reads IAgencyWalletRepository        |
| 4    | RED    | cf0b7cc | Failing tests: wallet-top-up-form + threshold-dialog + transactions-table                                               |
| 4    | GREEN  | 09e86fb | /admin/wallet RSC page + TopUpForm + ThresholdDialog + TransactionsTable (D-44 compact)                                 |
| 5    | RED    | 2d086b5 | Failing tests: low-balance-banner + request-top-up-link + insufficient-funds-panel retrofit                             |
| 5    | GREEN  | 57b90da | LowBalanceBanner + RequestTopUpLink + (portal)/layout.tsx mount + InsufficientFundsPanel retrofit + WalletChip queryKey |

## Requirements Closed

- **B2B-06 — Wallet top-up.** Agent-admin navigates to `/admin/wallet`, enters a top-up amount (zod £10–£50 000), Stripe PaymentElement confirms the intent client-side, the existing 05-03 idempotent commit path credits the wallet, the WalletChip in the header reflects the new balance within one 30s poll cycle (cache shared via `['wallet','balance']`).
- **B2B-07 — Threshold self-service.** Agent-admin clicks "Edit threshold", the Radix Dialog prefills from `['wallet','threshold']` cache, submits £50–£10 000 (client zod + backend range guard with application/problem+json on violation), dialog closes, cache is invalidated, the sitewide LowBalanceBanner re-arms when the backend's `SetThresholdAsync` resets `LowBalanceEmailSent=0` (hysteresis).

## Plan 05-03 Task 3 Deferred Criteria — Closure

All 15 deferred criteria from Plan 05-03 are now green:

1. Stripe PaymentElement mounted inside `<Elements>` on `/admin/wallet` — ✅ (Task 1)
2. loadStripe memoised at module scope (Pitfall 5) — ✅ (lib/stripe.ts)
3. Route-scoped CSP allowing Stripe origins ONLY on `/admin/wallet/:path*` — ✅ (next.config.mjs)
4. Default CSP matcher `/((?!admin/wallet).*)` omits Stripe origins — ✅ (csp-route-scoping.test.ts 3 facts)
5. POST /api/wallet/top-up/intent route handler with Auth.js Bearer forwarding — ✅
6. Top-up form zod £10–£50 000 client-side validation — ✅
7. Top-up form parses application/problem+json into user copy — ✅ ("Top-up must be between £10 and £50 000. You requested £5.")
8. Transactions ledger — page-number pagination 20/50/100 default 20 — ✅ (D-44)
9. Transactions row tint red-50 / green-50 per SignedAmount — ✅
10. Transactions empty state "No transactions yet" — ✅
11. Threshold editor Radix Dialog with zod £50–£10 000 — ✅
12. PUT /api/wallet/threshold backend action with JWT agency_id + D-40 range guard — ✅
13. Backend UpdateThresholdRequest DTO omits agencyId (Pitfall 28 structural defence) — ✅
14. HydrationBoundary + dehydrate on /admin/wallet RSC for all three keys — ✅
15. 403 / forbidden guard when session lacks `agent-admin` role — ✅ (server side via B2BAdminPolicy + client side via redirect in page.tsx)

## STRIDE Closure

| Threat       | Description                                                         | Disposition                                                                                                                                                                                                       |
| ------------ | ------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| T-05-03-06   | Stripe.js loading on non-checkout routes inflates SAQ-A scope       | **Closed.** Route-scoped CSP at `/admin/wallet/:path*` allows Stripe origins; default matcher omits them. 3 regression tests in csp-route-scoping.test.ts.                                                        |
| T-05-03-09   | mailto CTA leaks booking GROSS / session material via subject/body  | **Closed.** RequestTopUpLink emits subject-only href with hard-coded 'Top-up request' string (no interpolation). InsufficientFundsPanel non-admin branch retrofitted. 3 facts in request-top-up-link.test.tsx + 3 retrofit facts in insufficient-funds-panel.test.tsx assert no body=, agency_id, token, balance, threshold. |
| T-05-05-02   | /admin/wallet CSP relaxation leaks to non-wallet routes             | **Closed.** Same route-scoped CSP above — the Stripe origins are granted ONLY on the wallet matcher. Default matcher at `/((?!admin/wallet).*)` omits them.                                                       |
| T-05-05-03   | Cross-tenant threshold write via forged body agencyId               | **Closed.** UpdateThresholdRequest DTO omits `agencyId` property; B2BWalletController reads JWT `agency_id` claim only; B2BAdminPolicy enforced. Test `IGNORES a forged body.agency_id` asserts forge attempt is structurally impossible. |
| T-05-05-05   | LocalStorage banner-dismiss leaks cross-tab signal to other tenants | **Closed.** Banner dismiss writes sessionStorage only. Test `localStorage NEVER written` asserts via `vi.spyOn(window.localStorage, 'setItem')`.                                                                  |

### New Threat Discovered & Mitigated

| Threat | Description | Disposition |
| ------ | ----------- | ----------- |
| **T-05-05-03-bis (GetThreshold staleness)** | `B2BWalletController.GetThreshold` previously returned a hard-coded £500 default. Client banners hydrated from this endpoint would have shown a stale threshold until the client fetched fresh after a PUT. Not an auth breach, but a correctness bug that could mask a low-balance state from the agent-admin. | **Closed.** GetThreshold now reads `IAgencyWalletRepository.GetAsync(agencyId, ct)?.LowBalanceThresholdAmount ?? 500m` (the £500 is now only the fallback for brand-new agencies that have not had the threshold set). |

## Threat Flags

None introduced by this plan. All new surface (PUT /threshold, 3 portal route handlers, /admin/wallet RSC) is covered by the plan's existing threat model entries.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] WalletControllerTopUpTests DI failure after UpdateThresholdAsync added**
- **Found during:** Task 3 GREEN (backend build).
- **Issue:** Adding `IAgencyWalletRepository` to `B2BWalletController` constructor broke existing `WalletControllerTopUpTests.cs` — the factory didn't register an `IAgencyWalletRepository` mock.
- **Fix:** Added `AgencyWallets = Substitute.For<IAgencyWalletRepository>()` field to `WalletControllerTestFactory` and registered it in `services.AddSingleton(AgencyWallets)`. Existing top-up tests still pass (they don't exercise the threshold path).
- **Files modified:** tests/Payments.Tests/WalletControllerTopUpTests.cs
- **Commit:** 3bda32a

**2. [Rule 1 - Bug] TopUpForm problem+json locale separator mismatch**
- **Found during:** Task 4 GREEN (vitest run).
- **Issue:** Test regex expected `/must be between £10 and £50 ?000/i` (space or no separator), but `(50000).toLocaleString('en-GB')` produces `"50,000"` with a comma.
- **Fix:** Applied `n.toLocaleString('en-GB').replace(/,/g, ' ')` so the inline error copy renders `"Top-up must be between £10 and £50 000. You requested £5."` — matches the plan's behaviour spec and the test regex.
- **Files modified:** src/portals/b2b-web/app/(portal)/admin/wallet/top-up-form.tsx
- **Commit:** 09e86fb

**3. [Rule 1 - Bug] ThresholdDialog getByLabelText matched both dialog container AND input**
- **Found during:** Task 4 GREEN.
- **Issue:** Radix Dialog's `aria-labelledby` points to DialogTitle "Edit threshold" (matches `/threshold/i`), AND the `<label>Threshold (£)</label>` on the input also matches. `getByLabelText(/threshold/i)` returned both, causing "found multiple elements" error.
- **Fix:** Tightened the test regex to `/Threshold \(£\)/i` (matches only the input's label with parens) — the dialog title stays at "Edit threshold" (intentional match for the dialog role name assertion).
- **Files modified:** src/portals/b2b-web/tests/components/admin/threshold-dialog.test.tsx
- **Commit:** cf0b7cc

**4. [Rule 1 - Bug] TransactionsTable pager not rendered during initial load**
- **Found during:** Task 4 GREEN.
- **Issue:** An early `if (isLoading && !data) return <div>Loading...</div>;` return meant the `<button>Next</button>` pager wasn't in the DOM when the test called `getByRole('button', { name: /next/i })` immediately after first fetch.
- **Fix:** Removed the early return so the table + pager always render regardless of loading state (the table body shows a skeleton/empty row until data arrives, which is the intended D-44 behaviour).
- **Files modified:** src/portals/b2b-web/app/(portal)/admin/wallet/transactions-table.tsx
- **Commit:** 09e86fb

**5. [Rule 1 - Bug] B2BWalletController.GetThreshold hard-coded £500 default**
- **Found during:** Task 3 GREEN (I was wiring the PUT action and noticed the GET returned a constant).
- **Issue:** `GetThreshold` returned `return Ok(new { threshold = 500m, currency = "GBP" });` regardless of the agency's actual `LowBalanceThresholdAmount`. Writing a new threshold via PUT would persist correctly, but GET would keep returning £500 until the repository's threshold was non-null AND the code was updated to read it. Latent bug predating this plan.
- **Fix:** Injected `IAgencyWalletRepository` into the controller and read `_agencyWallets.GetAsync(agencyId, ct)?.LowBalanceThresholdAmount ?? 500m`. The £500 is now only the fallback for brand-new agencies.
- **Files modified:** src/services/PaymentService/PaymentService.API/Controllers/WalletController.cs
- **Commit:** 3bda32a

### Architectural Divergences

**1. Radix Dialog import path**
- **Plan said:** `grep @radix-ui/react-dialog` in the behaviour block for ThresholdDialog.
- **Actual:** Used `@/components/ui/dialog` (the project's shell that re-exports `Dialog`, `DialogTrigger`, `DialogContent`, etc. from the `radix-ui` barrel package). Matches the project's existing pattern from `/bookings` void-booking-button Radix Dialog (Plan 05-04).
- **Rationale:** The project uses the `radix-ui` umbrella barrel + a local shell re-export rather than the individual `@radix-ui/react-*` packages. The behaviour is identical; only the import path differs. The ThresholdDialog test asserts `getByRole('dialog')` which passes on either import strategy.

**2. WalletChip queryKey migration (behaviour spec requirement)**
- **Plan Task 5 behaviour test 7:** "shares TanStack query key `['wallet','balance']` with WalletChip — only ONE fetch per render."
- **Actual:** WalletChip's original (Plan 05-02) queryKey was `['wallet-balance']` (dash legacy). Migrated to array form `['wallet','balance']` so the chip + sitewide banner hit ONE cache entry.
- **Scope:** One line change in `wallet-chip.tsx`. Existing 6 WalletChip tests still pass (they don't pin the queryKey shape).

## Authentication Gates

None. No auth gates occurred during execution — all three Keycloak/Auth.js flows (B2BAdminPolicy server-side, `agent-admin` role client-side, JWT `agency_id` claim) were already wired in Plans 05-00 and 05-03.

## Test Tally

**Backend (xUnit):**
- `B2BWalletControllerThresholdTests.cs` — 7 facts (happy path + Pitfall 28 ignored body agencyId + 403 agent-readonly + 4× problem+json: non-positive, bad currency, below £50, above £10000).

**Portal (Vitest):**
- `csp-route-scoping.test.ts` — 3 facts
- `route-wallet-top-up-intent.test.ts` — 6 facts
- `route-wallet-threshold.test.ts` — 6 facts
- `route-wallet-transactions.test.ts` — 5 facts
- `components/wallet/wallet-payment-element-wrapper.test.tsx` — 3 facts
- `components/wallet/low-balance-banner.test.tsx` — 7 facts
- `components/wallet/request-top-up-link.test.tsx` — 3 facts
- `components/wallet/wallet-chip.test.tsx` — 6 facts (pre-existing, still green after queryKey migration)
- `components/checkout/insufficient-funds-panel.test.tsx` — 6 facts (3 original + 3 retrofit)
- `components/admin/wallet-top-up-form.test.tsx` — 3 facts
- `components/admin/threshold-dialog.test.tsx` — 4 facts
- `components/admin/transactions-table.test.tsx` — 4 facts

**Full b2b-web suite after Task 5 GREEN:** 25 test files / 109 tests — all pass.

**Backend suite after Task 3 GREEN:** All Payments.Tests green (7 new threshold facts + 6 existing top-up facts + full existing suite).

## Known Stubs

None. Every component and route handler in this plan is wired end-to-end through real data paths:

- `/admin/wallet` RSC prefetches from real PaymentService endpoints via `gatewayFetch`.
- `TopUpForm` posts to the real `POST /api/wallet/top-up/intent` route handler which proxies to PaymentService `POST /api/wallet/top-up/intent` (built in 05-03).
- `ThresholdDialog` PUTs to the real `PUT /api/wallet/threshold` route handler which proxies to the new `B2BWalletController.UpdateThresholdAsync` backend action.
- `TransactionsTable` GETs from the real `GET /api/wallet/transactions` route handler which proxies to PaymentService `GET /api/wallet/transactions` (built in 05-03).
- `LowBalanceBanner` polls the real `GET /api/wallet/balance` and `GET /api/wallet/threshold` endpoints.

The `support@thebookingengine.com` email fallback in `RequestTopUpLink` when no `adminEmail` prop is supplied is INTENTIONAL (documented in the component) — it is not a stub, it is a deliberate fallback for agencies that have not yet set a non-admin CTA target.

## Deferred Issues

None. No Rule 4 architectural changes required user consultation. No fix-attempt limit reached on any task.

## Self-Check: PASSED

**Files verified:** 30/30 present.

- 2 backend (WalletController.cs modified; B2BWalletControllerThresholdTests.cs created) + 1 backend test factory patch (WalletControllerTopUpTests.cs).
- 4 component files created (stripe.ts, wallet-payment-element-wrapper.tsx, low-balance-banner.tsx, request-top-up-link.tsx) + 2 modified (wallet-chip.tsx, insufficient-funds-panel.tsx).
- 4 page files created (page.tsx, top-up-form.tsx, threshold-dialog.tsx, transactions-table.tsx) + 1 layout modified (layout.tsx).
- 3 route handlers created.
- 1 config modified (next.config.mjs).
- 11 Vitest files (7 created + 1 modified).
- 1 SUMMARY.md created.

**Commits verified:** 10/10 present on branch `worktree-agent-abb0396f`.

- Task 1 RED 7ee34d6 + GREEN dec6510
- Task 2 RED 1060da3 + GREEN 6a21f5b
- Task 3 RED c289e24 + GREEN 3bda32a
- Task 4 RED cf0b7cc + GREEN 09e86fb
- Task 5 RED 2d086b5 + GREEN 57b90da

**Test runs green:**
- Task 5 focused: 3 files / 16 tests (request-top-up-link.test.tsx + low-balance-banner.test.tsx + insufficient-funds-panel.test.tsx).
- Full b2b-web suite: 25 files / 109 tests.
- Backend Payments.Tests suite: all green after Task 3 GREEN.

**TDD gate compliance:** All 5 tasks follow RED → GREEN sequence in commit log. Every `feat(05-05)` commit is preceded by a `test(05-05)` commit on the same task.

