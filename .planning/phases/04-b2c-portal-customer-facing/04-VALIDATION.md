---
phase: 4
slug: b2c-portal-customer-facing
status: approved
nyquist_compliant: true
wave_0_complete: false
created: 2026-04-16
approved: 2026-04-16
---

# Phase 4 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | vitest 2.x (frontend), Playwright 1.x (E2E, wave-level only), xUnit (.NET backend) |
| **Config file** | `src/portals/b2c-web/vitest.config.ts` (Wave 0), `src/portals/b2c-web/playwright.config.ts` (Wave 0), existing `tests/*.csproj` |
| **Quick run command (per task, <120s)** | `cd src/portals/b2c-web && pnpm test --run` OR `dotnet test <proj> --filter <FullyQualifiedName>` |
| **Full suite command (per wave)** | `cd src/portals/b2c-web && pnpm test --run && pnpm typecheck && pnpm exec playwright test && cd ../.. && dotnet test` |
| **Estimated per-task runtime** | < 120s (vitest/dotnet filter) |
| **Estimated wave runtime** | ~5–8 min (adds Playwright + full dotnet test) |

---

## Sampling Rate

- **After every task commit:** Run the task's `<automated>` command (< 120s — vitest/dotnet filter only, no Playwright).
- **After every plan wave:** Run the task's `<wave_verify>` command(s) — Playwright E2E lands here.
- **Before `/gsd-verify-work`:** Full suite + `bash infra/keycloak/verify-audience-smoke.sh` must be green.
- **Max per-task feedback latency:** 120 seconds (Nyquist — unit/dotnet only; Playwright is wave-level).

---

## Per-Task Verification Map

> Populated 2026-04-16. Every task across plans 04-00..04-04 is represented. `<automated>` is the per-task <120s gate; `<wave_verify>` entries run at wave boundaries.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 4-00-01a | 04-00 | 1 | (scaffold) | T-04-00-05 | Pinned Auth.js beta; TS-enabled scaffold | build | `cd src/portals/b2c-web && pnpm install --frozen-lockfile && pnpm typecheck` | ❌ W0 | ⬜ pending |
| 4-00-01b | 04-00 | 1 | (scaffold) | T-04-00-04, T-04-00-06 | Edge-split Auth.js + Stripe CSP | build | `cd src/portals/b2c-web && pnpm typecheck && pnpm build` | ❌ W0 | ⬜ pending |
| 4-00-02  | 04-00 | 1 | B2C-05, COMP-05 | T-04-00-01 | Session cookie round-trip; mobile viewport harness | vitest + playwright-smoke | `cd src/portals/b2c-web && pnpm test && pnpm exec playwright test --project=chromium --grep "landing page renders"` | ❌ W0 | ⬜ pending |
| 4-00-03  | 04-00 | 1 | COMP-04 | T-04-00-02, T-04-00-03, T-04-00-07 | Red placeholders compile + trait-filtered; Keycloak aud=tbe-api + admin client real-token smoke | dotnet + bash | `dotnet build tests/BookingService.Tests/BookingService.Tests.csproj -warnaserror && dotnet test tests/BookingService.Tests/BookingService.Tests.csproj --filter "Category!=RedPlaceholder" --no-build` (wave: `bash infra/keycloak/verify-audience-smoke.sh`) | ❌ W0 | ⬜ pending |
| 4-01-01  | 04-01 | 2 | B2C-08, FLTB-03 | T-04-01-01, T-04-01-02 | Receipt ownership 403; PDF contains fare+YQYR+tax breakdown | dotnet TDD | `dotnet test tests/BookingService.Tests/BookingService.Tests.csproj --filter "FullyQualifiedName~Receipt\|FullyQualifiedName~QuestPdfBookingReceiptGenerator" -warnaserror` | ❌ W0 | ⬜ pending |
| 4-01-02  | 04-01 | 2 | B2C-01, B2C-02, B2C-07, NOTF-02 | T-04-01-03, T-04-01-04, T-04-01-05, T-04-01-06, T-04-01-07, T-04-01-08 | Keycloak round-trip; verify-email gate; dashboard forces dynamic; PDF streams; client-credentials admin call | vitest (task) + playwright (wave) | `cd src/portals/b2c-web && pnpm test -- --run tests/account && pnpm typecheck` (wave: `pnpm exec playwright test --grep "receipt-download\|password-reset\|register-login" --project=chromium`) | ❌ W0 | ⬜ pending |
| 4-02-01  | 04-02 | 2 | B2C-03, FLTB-01 | T-04-02-04 | IATA typeahead server-side cached; rate limit; no external per-keystroke call | vitest + dotnet | `cd src/portals/b2c-web && pnpm test --run tests/search && cd ../.. && dotnet test tests/InventoryService.Tests --filter "FullyQualifiedName~Airport"` | ❌ W0 | ⬜ pending |
| 4-02-02  | 04-02 | 2 | B2C-03, B2C-04, FLTB-02, FLTB-03 | T-04-02-05 | Results cache; client-side filter/sort; YQYR separated | vitest | `cd src/portals/b2c-web && pnpm test --run tests/results tests/search/use-flight-search.test.ts` | ❌ W0 | ⬜ pending |
| 4-02-03  | 04-02 | 2 | B2C-05, B2C-06, COMP-01 | T-04-02-01, T-04-02-02, T-04-02-03, T-04-02-06, T-04-02-07, T-04-02-08, T-04-02-09 | Stripe Elements isolation; poll-based terminal state; unverified-email gate; rate-limit | vitest (task) + playwright (wave) + W10 pre-flight | pre-flight `bash infra/keycloak/verify-audience-smoke.sh` then `cd src/portals/b2c-web && pnpm test -- --run tests/checkout && pnpm typecheck` (wave: `pnpm exec playwright test --grep "flight-booking" --project=chromium && pnpm exec playwright test --grep "flight-booking" --project=mobile`) | ❌ W0 | ⬜ pending |
| 4-03-01  | 04-03 | 2 | HOTB-04, NOTF-02 | T-04-03-01, T-04-03-02 | Voucher email idempotent; PDF generator Community-licensed; attachment delivered | dotnet TDD | `dotnet test tests/Notifications.Tests/Notifications.Tests.csproj --filter "FullyQualifiedName~HotelBookingConfirmedConsumer\|FullyQualifiedName~HotelVoucherDocument"` | ❌ W0 | ⬜ pending |
| 4-03-02  | 04-03 | 2 | HOTB-01, HOTB-02, HOTB-03, HOTB-05 | T-04-03-03, T-04-03-04 | HotelBookingsController ownership; streaming voucher pass-through (D-16 single source); server-re-price | dotnet TDD | `dotnet build src/services/BookingService/BookingService.API/BookingService.API.csproj -warnaserror && dotnet test tests/BookingService.Tests/BookingService.Tests.csproj --filter "FullyQualifiedName~HotelBookingsController"` | ❌ W0 | ⬜ pending |
| 4-03-03  | 04-03 | 2 | HOTB-01, HOTB-02, HOTB-03, B2C-05 | T-04-03-05 | Hotel search/detail UI; mobile ≤5 steps; force-dynamic results | vitest (task) + playwright (wave) | `cd src/portals/b2c-web && pnpm typecheck && pnpm test --run --reporter=verbose tests/hotel-card.test.tsx tests/hotel-search-form.test.tsx` (wave: `pnpm exec playwright test --project=mobile --grep "hotel-booking"`) | ❌ W0 | ⬜ pending |
| 4-04-01  | 04-04 | 3 | PKG-02, PKG-03 | T-04-04-01, T-04-04-02, T-04-04-03, T-04-04-04, T-04-04-07, T-04-04-08, T-04-04-10, T-04-04-11 | Single combined PaymentIntent (D-08); sequential partial captures (D-10); partial-failure release-remainder (D-09); idempotency keys deterministic | dotnet TDD | `dotnet build src/services/BookingService/BookingService.API/BookingService.API.csproj -warnaserror && dotnet test tests/BookingService.Tests/BookingService.Tests.csproj --filter "FullyQualifiedName~BasketsController\|FullyQualifiedName~BasketPaymentOrchestrator"` | ❌ W0 | ⬜ pending |
| 4-04-02  | 04-04 | 3 | PKG-03, CARB-03, NOTF-02 | T-04-04-06, T-04-04-09 | Single basket email (full + partial per D-09 single-statement copy); car voucher idempotent | dotnet TDD | `dotnet test tests/Notifications.Tests/Notifications.Tests.csproj --filter "FullyQualifiedName~BasketConfirmedConsumer\|FullyQualifiedName~CarBookingConfirmedConsumer"` | ❌ W0 | ⬜ pending |
| 4-04-03a | 04-04 | 3 | CARB-01, CARB-02, CARB-03 | T-04-04-09 | Car/transfer search UI; CarBookingsController ownership; car migration | vitest + dotnet (task) + playwright (wave) | `cd src/portals/b2c-web && pnpm typecheck && pnpm test --run tests/car-card.test.tsx && cd ../.. && dotnet test tests/BookingService.Tests/BookingService.Tests.csproj --filter "FullyQualifiedName~CarBookingsController"` (wave: `cd src/portals/b2c-web && pnpm exec playwright test --project=mobile --grep "car-search"`) | ❌ W0 | ⬜ pending |
| 4-04-03b | 04-04 | 3 | PKG-01, PKG-02, PKG-04 | T-04-04-01, T-04-04-11, T-04-04-12 | Trip Builder side-by-side (D-07); ONE PaymentElement + ONE confirmPayment (D-08); independent cancellation policies rendered; unified `?ref=kind-id` (B5) | vitest (task) + playwright (wave) | `cd src/portals/b2c-web && pnpm typecheck && pnpm test --run tests/checkout-ref.test.ts tests/basket-footer.test.tsx tests/combined-payment-form.test.tsx` (wave: `pnpm exec playwright test --project=mobile --grep "trip-builder"`) | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky · File Exists: ❌ W0 = Wave 0 creates file, then downstream plan turns it green.*

---

## Wave 0 Requirements

- [ ] `src/portals/b2c-web/vitest.config.ts` + `package.json` test scripts — vitest harness for B2C portal (Task 4-00-02)
- [ ] `src/portals/b2c-web/playwright.config.ts` + `e2e/` scaffolding — E2E harness with Stripe test keys (Task 4-00-02)
- [ ] `src/portals/b2c-web/tests/setup.ts` + MSW handlers — mock backend during unit tests (Task 4-00-02)
- [ ] Confirm Auth.js v5 beta + Next 16 + nuqs 2.8 mutual compatibility smoke test passes (Task 4-00-02, opt-in via `TEST_KC_USER`) — resolves RESEARCH Open Question 7
- [ ] Stripe test-mode keys + webhook signing secret available in `.env.test`
- [ ] Keycloak realm export committed (`infra/keycloak/realm-tbe-b2c.json`) + real-token smoke script green (`bash infra/keycloak/verify-audience-smoke.sh`) — Task 4-00-03 (W10)
- [ ] Red placeholder test files compile with `Trait("Category","RedPlaceholder")`; `dotnet test --filter "Category!=RedPlaceholder"` baseline is green (Task 4-00-03)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Confirmation email arrives within 60s | NOTF-02 | SendGrid delivery latency requires real inbox | UAT script: book in test mode, time email arrival in dedicated test mailbox |
| Mobile responsive flow ≤5 steps | B2C-05 | Step-count + viewport judgment | UAT: complete booking on iPhone 12 viewport, count screens (flight, hotel, trip-builder) |
| PDF receipt rendering quality | B2C-08 | Visual review | UAT: download PDF, verify itinerary + fare breakdown correct |
| Hotel voucher PDF correctness | HOTB-04 | Visual review of supplier-format voucher | UAT: book hotel, open voucher PDF, confirm fields |
| D-08 single-charge statement | PKG-02 | Requires real Stripe test statement inspection | UAT: complete combined checkout → Stripe Dashboard → Payments → verify ONE charge for basket total |
| D-09 partial-failure statement entry | PKG-02, D-09 | Requires injected hotel failure + statement inspection | UAT: force HotelBookingFailed → verify single statement entry for flight subtotal + single email per basket |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify (every task has a < 120s automated gate; Playwright promoted to wave_verify per W8)
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 120s (per-task gate excludes Playwright)
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved 2026-04-16
