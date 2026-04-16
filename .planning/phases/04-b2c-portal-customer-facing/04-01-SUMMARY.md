---
phase: 04-b2c-portal-customer-facing
plan: 01
subsystem: b2c-portal
tags: [nextjs-16, auth-js-v5, keycloak, questpdf, ef-core, rsc, stream-through, pitfall-8, pitfall-11, pitfall-14]

# Dependency graph
requires:
  - phase: 03-booking-service
    provides: BookingSagaState (rowversion optimistic concurrency, CurrentState enum), BookingsController (ownership check pattern), EF Core migrations (hand-authored convention), JWT bearer config
  - phase: 04-00
    provides: src/portals/b2c-web scaffold (Auth.js v5 edge-split, gatewayFetch, Playwright/Vitest harness), tbe-b2c realm + tbe-b2c-admin service client

provides:
  - BookingService /bookings/{id}/receipt.pdf endpoint with ownership check (T-04-01-01) + backoffice-staff role bypass
  - BookingService /customers/me/bookings convenience route (D-17) resolving customerId from JWT sub
  - QuestPdfBookingReceiptGenerator with fare/YQ-YR/tax separation per FLTB-03 (D-15)
  - Three persisted decimal columns on BookingSagaState (BaseFareAmount, SurchargeAmount, TaxAmount) + hand-authored migration 20260500000000_AddReceiptFareBreakdown
  - B2C auth pages (login/register/password-reset/verify-email) as thin Keycloak wrappers (D-04, D-06)
  - Customer dashboard RSC with Upcoming/Past partitioning (D-17) + Radix-based DashboardTabs Client Component
  - Booking detail page with Download receipt CTA
  - Stream-through /api/bookings/[id]/receipt.pdf proxy (Pitfall 14 + T-04-01-07)
  - Keycloak admin resend-verification route via tbe-b2c-admin service client (Pitfall 8 + T-04-01-04)
  - Intl-based formatMoney (Pitfall 13) + formatDate + formatDuration
  - types/api.ts DTO shim with forward-compat departureDate/productType fields
  - Ambient UI module declarations so `.jsx` starterKit components type-check without forcing every caller to pass every variant prop

affects:
  - 04-02 (search surfaces — will consume formatMoney, BookingDtoPublic, and the established RSC → gatewayFetch pattern)
  - 04-03 (trip builder — will extend DashboardTabs/BookingRow for the basket flow)
  - 04-04 (checkout — will gate on session.email_verified and POST /api/auth/resend-verification on the payment step modal)

# Tech tracking
tech-stack:
  added:
    - "PdfPig 0.1.10 (BookingService.Tests — extract text from QuestPDF FlateDecode streams so fare-breakdown assertions survive compression)"
    - "QuestPDF 2026.2.4 (BookingService.Infrastructure — LicenseType.Community with TODO(prod) Commercial upgrade marker)"
  patterns:
    - "IDOR mitigation pattern: booking.UserId != userId && !User.IsInRole(\"backoffice-staff\") — mirrored from BookingsController; applied verbatim in ReceiptsController"
    - "RSC data read + redirect pattern: const session = await auth(); if (!session) redirect(/login?callbackUrl=...)"
    - "Pass-through streaming pattern: new Response(upstream.body, {...}) — never await upstream.arrayBuffer() for files"
    - "Service-account token caching pattern: in-process Map entry with 30s expiry skew, silent refresh on call"
    - "Forward-compat DTO shim pattern: optional fields on types/api.ts that downstream plans populate on the backend, with sensible frontend fallbacks"
    - "Ambient module shim pattern (types/ui.d.ts): wrap .jsx starterKit components so callers don't have to pass every variant prop for TS to compile"

key-files:
  created:
    - src/services/BookingService/BookingService.Application/Pdf/IBookingReceiptPdfGenerator.cs
    - src/services/BookingService/BookingService.Infrastructure/Pdf/QuestPdfBookingReceiptGenerator.cs
    - src/services/BookingService/BookingService.API/Controllers/ReceiptsController.cs
    - src/services/BookingService/BookingService.Infrastructure/Migrations/20260500000000_AddReceiptFareBreakdown.cs
    - src/portals/b2c-web/app/(public)/login/page.tsx
    - src/portals/b2c-web/app/(public)/register/page.tsx
    - src/portals/b2c-web/app/(public)/password-reset/page.tsx
    - src/portals/b2c-web/app/(public)/verify-email/page.tsx
    - src/portals/b2c-web/app/bookings/page.tsx
    - src/portals/b2c-web/app/bookings/[id]/page.tsx
    - src/portals/b2c-web/app/api/bookings/[id]/receipt.pdf/route.ts
    - src/portals/b2c-web/app/api/auth/resend-verification/route.ts
    - src/portals/b2c-web/components/account/empty-state.tsx
    - src/portals/b2c-web/components/account/booking-row.tsx
    - src/portals/b2c-web/components/account/dashboard-tabs.tsx
    - src/portals/b2c-web/lib/keycloak-admin.ts
    - src/portals/b2c-web/lib/formatters.ts
    - src/portals/b2c-web/types/api.ts
    - src/portals/b2c-web/types/ui.d.ts
    - src/portals/b2c-web/tests/account/empty-state.test.tsx
    - src/portals/b2c-web/tests/account/dashboard-tabs.test.tsx
    - src/portals/b2c-web/e2e/register-login.spec.ts
    - src/portals/b2c-web/e2e/password-reset.spec.ts
    - src/portals/b2c-web/e2e/receipt-download.spec.ts
    - tests/BookingService.Tests/ReceiptsControllerTests.cs (rewrote 04-00 placeholder)
    - tests/BookingService.Tests/QuestPdfBookingReceiptGeneratorTests.cs (rewrote 04-00 placeholder)
  modified:
    - src/services/BookingService/BookingService.Application/Saga/BookingSagaState.cs
    - src/services/BookingService/BookingService.Infrastructure/Configurations/BookingSagaStateMap.cs
    - src/services/BookingService/BookingService.Infrastructure/BookingService.Infrastructure.csproj
    - src/services/BookingService/BookingService.API/Program.cs
    - src/services/BookingService/BookingService.API/Controllers/BookingsController.cs
    - tests/BookingService.Tests/BookingService.Tests.csproj
    - src/portals/b2c-web/lib/auth.ts
    - src/portals/b2c-web/types/auth.d.ts

key-decisions:
  - "D-17 simplification shipped: /customers/me/bookings resolves the caller's customerId from ClaimTypes.NameIdentifier ?? sub and delegates to the existing ListForCustomerAsync instead of the portal constructing sub itself. The explicit /customers/{customerId}/bookings route is preserved for backoffice-staff."
  - "FLTB-03 fare breakdown persisted on BookingSagaState (BaseFareAmount, SurchargeAmount, TaxAmount decimal(18,4) NOT NULL DEFAULT 0) rather than derived per-request. Rationale: receipts are regenerated in the future (audits, reprints) and must not re-query GDS pricing."
  - "Migration 20260500000000_AddReceiptFareBreakdown is hand-authored (no ModelSnapshot diff) — established convention from 03-01 Deviation #2. Three ALTER TABLE ADD COLUMN statements, no FK or index changes."
  - "QuestPDF text content verified via PdfPig 0.1.10 in tests, not naive ASCII substring search — QuestPDF FlateDecode-compresses content streams so byte-level search would silently miss the real text."
  - "Resend-verification runs on Node runtime only (not Edge). lib/keycloak-admin.ts is guarded against browser imports via `if (typeof window !== 'undefined') throw`. The service-account token is cached in-process with a 30s expiry skew and NEVER logged (Pitfall 8 + T-04-01-04)."
  - "Auth.js v5 session.user.id is now wired to the Keycloak `sub` claim via an explicit token.sub preservation in the jwt callback. This is the key the Admin API addresses; losing it would break resend-verification silently."
  - "Frontend DashboardBooking type has optional departureDate + productType fields. Backend BookingDtoPublic does not yet carry them; the dashboard partitioner falls back to createdAt, and BookingRow infers the product icon from the reference prefix. Plans 04-02/04-03 will populate these on the backend side."

patterns-established:
  - "RSC empty-state handoff: RSC fetches, Client Component ({ upcoming, past }) renders; zero-state copy lives in the Client Component so it's always grepped verbatim from the rendered source."
  - "Callback-URL open-redirect defence: always run a same-origin guard on any callbackUrl before passing it to signIn() or redirect(), even though Auth.js v5 also validates by default."
  - "PDF endpoint content verification: use PdfPig `PdfDocument.Open(bytes)` + `page.Text` to assert rendered text, because QuestPDF compresses content streams."

requirements-completed: [B2C-01, B2C-02, B2C-07, B2C-08, NOTF-02]

# Metrics
duration: ~55min
completed: 2026-04-16
---

# Phase 04 Plan 01: B2C Account Surfaces + Receipts Summary

**Ship the end-to-end B2C account flow: Keycloak-wrapped login/register/password-reset/verify-email routes, a customer dashboard RSC reading `/customers/me/bookings` with Upcoming/Past tabs, a working Download-receipt CTA backed by a QuestPDF FLTB-03 fare-breakdown generator, and a resend-verification Keycloak Admin API path via the `tbe-b2c-admin` service client.**

## Performance

- **Duration:** ~55 min (Task 1 ~30 min, Task 2 ~25 min)
- **Started:** 2026-04-16T17:20:00Z
- **Completed:** 2026-04-16T18:04:00Z
- **Tasks:** 2 / 2
- **Files modified:** 9
- **Files created:** 25

## Accomplishments

- **Receipt pipeline, end-to-end.** BookingService gained `ReceiptsController` with the verbatim IDOR check from `BookingsController` (T-04-01-01) plus a QuestPDF generator that renders PNR, ticket number, and a three-line fare breakdown (base / YQ-YR surcharges / taxes) per FLTB-03. The b2c-web proxy streams the upstream body (Pitfall 14) so a 10 MB PDF never buffers in Next.js memory (T-04-01-07).
- **All Keycloak identity surfaces owned by Keycloak.** `/login` kicks off `signIn('keycloak')` with a same-origin callbackUrl guard; `/register` / `/password-reset` are RSC redirects to Keycloak's hosted pages; `/verify-email` re-triggers `signIn` to refresh the `email_verified` claim. No usernames, passwords or reset-email prompts are reimplemented in the portal (D-04, D-06, T-04-01-03).
- **Resend-verification works without blowing up the user's B2C token.** `lib/keycloak-admin.ts` mints a `tbe-b2c-admin` service-account token via `client_credentials`, caches it in-process with a 30s expiry skew, and calls `PUT /admin/realms/tbe-b2c/users/{sub}/send-verify-email` (Pitfall 8, T-04-01-04). The route handler is Node-runtime-only and the module is guarded against accidental browser imports.
- **Dashboard RSC partitions correctly.** `/bookings` reads `/customers/me/bookings` via `gatewayFetch`, partitions bookings into Upcoming (ascending by date) and Past (descending), and hands them to the client-side `DashboardTabs`. UI-SPEC empty-state copy is rendered verbatim in both tabs; the Upcoming tab's CTA links to `/` per the Copywriting Contract.
- **Stack drift avoided.** Three forward-looking fields (`BaseFareAmount`, `SurchargeAmount`, `TaxAmount`) were added to `BookingSagaState` with a hand-authored migration; the DI container registers `IBookingReceiptPdfGenerator`; `/customers/me/bookings` resolves the caller's `sub` server-side. Downstream plans (04-02 onwards) pick up the convention without rework.

## Task Commits

1. **Task 1 RED:** `test(04-01): ReceiptsController ownership + QuestPdfBookingReceiptGenerator` — `785fee3`
2. **Task 1 GREEN:** `feat(04-01): B2C-08 receipt endpoint + QuestPDF generator + /customers/me/bookings` — `2343996`
3. **Task 2 RED:** `test(04-01): account dashboard + auth round-trip + receipt download` — `9c6c8eb`
4. **Task 2 GREEN:** `feat(04-01): B2C-01/02/07/08 account surfaces + receipt download + resend-verification` — `00cfb20`

Task 1 also triggered an auto-fix on the test assertions — PdfPig was added to `BookingService.Tests.csproj` so PDF text assertions run against extracted text rather than FlateDecode-compressed bytes. See Deviations below.

**Plan metadata commit:** (appended after SUMMARY.md is finalised.)

## Files Created / Modified

### Backend

- `src/services/BookingService/BookingService.Application/Pdf/IBookingReceiptPdfGenerator.cs` — single-method abstraction (`GenerateAsync(state, ct) → byte[]`). Isolates the Infrastructure dependency so tests can swap in an NSubstitute fake.
- `src/services/BookingService/BookingService.Infrastructure/Pdf/QuestPdfBookingReceiptGenerator.cs` — QuestPDF implementation. Static ctor sets `LicenseType.Community` with a `TODO(prod)` marker for the commercial-licence switch. Document shape mirrors `QuestPdfETicketGenerator` (header / content / footer with `Margin(30)`).
- `src/services/BookingService/BookingService.API/Controllers/ReceiptsController.cs` — `[Authorize]` + `[Route("bookings")]`; `GetReceiptAsync` resolves userId from `NameIdentifier ?? sub`, returns `NotFound` / `Forbid` / `File(bytes, "application/pdf", "receipt-{ref}.pdf")`.
- `src/services/BookingService/BookingService.Infrastructure/Migrations/20260500000000_AddReceiptFareBreakdown.cs` — hand-authored migration: three `decimal(18,4) NOT NULL DEFAULT 0` columns on `Saga.BookingSagaState`.
- `src/services/BookingService/BookingService.Application/Saga/BookingSagaState.cs` — added three decimal properties with XML-doc references to FLTB-03.
- `src/services/BookingService/BookingService.Infrastructure/Configurations/BookingSagaStateMap.cs` — three `HasColumnType("decimal(18,4)")` entries.
- `src/services/BookingService/BookingService.API/Program.cs` — `AddScoped<IBookingReceiptPdfGenerator, QuestPdfBookingReceiptGenerator>()`.
- `src/services/BookingService/BookingService.API/Controllers/BookingsController.cs` — new `[HttpGet("/customers/me/bookings")] ListForMeAsync` delegating to the existing `ListForCustomerAsync` after resolving `customerId` from JWT.
- `tests/BookingService.Tests/ReceiptsControllerTests.cs` — four real tests (owner → 200 + pdf, non-owner → 403, backoffice-staff → 200, unknown id → 404), EF InMemory + NSubstitute.
- `tests/BookingService.Tests/QuestPdfBookingReceiptGeneratorTests.cs` — three tests asserting PDF magic bytes, fare/YQ-YR/tax breakdown, and PNR / ticket / reference presence in the extracted text.
- `tests/BookingService.Tests/BookingService.Tests.csproj` — added PdfPig package reference.

### Frontend

- `src/portals/b2c-web/app/(public)/login/page.tsx` — Client Component calling `signIn('keycloak')` with a same-origin-validated callbackUrl; Forgot password + Register links.
- `src/portals/b2c-web/app/(public)/register/page.tsx` — RSC redirect to `{issuer}/protocol/openid-connect/registrations`.
- `src/portals/b2c-web/app/(public)/password-reset/page.tsx` — RSC redirect to `{issuer}/login-actions/reset-credentials`.
- `src/portals/b2c-web/app/(public)/verify-email/page.tsx` — Client Component with Continue CTA re-triggering signIn.
- `src/portals/b2c-web/app/bookings/page.tsx` — RSC with `dynamic = 'force-dynamic'`; partitions `/customers/me/bookings` response into Upcoming/Past; renders `DashboardTabs`.
- `src/portals/b2c-web/app/bookings/[id]/page.tsx` — RSC with awaited `params` Promise; Download-receipt CTA.
- `src/portals/b2c-web/app/api/bookings/[id]/receipt.pdf/route.ts` — Node-runtime stream-through proxy: `new Response(upstream.body, ...)`.
- `src/portals/b2c-web/app/api/auth/resend-verification/route.ts` — Node-runtime POST; 401 when unauthenticated, 502 on upstream failure, 202 on success.
- `src/portals/b2c-web/components/account/empty-state.tsx` — shared shell; copy is always passed in as props.
- `src/portals/b2c-web/components/account/booking-row.tsx` — product icon + reference + date + money + status dot + chevron.
- `src/portals/b2c-web/components/account/dashboard-tabs.tsx` — Client Component with Upcoming/Past tabs; verbatim UI-SPEC empty-state copy per tab.
- `src/portals/b2c-web/lib/keycloak-admin.ts` — `getServiceAccountToken` (client_credentials + 30s-skew cache) + `sendVerifyEmail`. Browser-import guard at module top.
- `src/portals/b2c-web/lib/formatters.ts` — `formatMoney(Intl)`, `formatDate`, `formatDuration`.
- `src/portals/b2c-web/lib/auth.ts` — preserves `token.sub` on initial sign-in and hydrates `session.user.id`.
- `src/portals/b2c-web/types/auth.d.ts` — Session module augmentation adds `user.id`.
- `src/portals/b2c-web/types/api.ts` — `BookingDtoPublic` + optional `departureDate` / `productType`.
- `src/portals/b2c-web/types/ui.d.ts` — ambient shim for starterKit `.jsx` components.
- `src/portals/b2c-web/tests/account/empty-state.test.tsx` — 4 tests (heading+body, optional action link, past-bookings copy, no link when no action).
- `src/portals/b2c-web/tests/account/dashboard-tabs.test.tsx` — 5 tests (Upcoming default, counts, verbatim empty states, Past-tab click, booking row).
- `src/portals/b2c-web/e2e/register-login.spec.ts` — Playwright round-trip asserting UI-SPEC empty-state copy after Keycloak sign-in (opt-in).
- `src/portals/b2c-web/e2e/password-reset.spec.ts` — asserts "Forgot password?" link lands on `{issuer}/login-actions/reset-credentials` (opt-in).
- `src/portals/b2c-web/e2e/receipt-download.spec.ts` — asserts `application/pdf` + magic bytes on `/api/bookings/:id/receipt.pdf` (opt-in).

## TDD Gate Compliance

- **Plan-level TDD:** the plan is `type: execute`, not `type: tdd` — per-task TDD gates apply (each `type="auto" tdd="true"` task), not a single plan-wide RED/GREEN cycle.
- **Task 1 RED:** `785fee3` contains test-only changes to `ReceiptsControllerTests.cs` and `QuestPdfBookingReceiptGeneratorTests.cs`; `dotnet test` failed with 4 compile errors on the missing controller + interface + namespaces.
- **Task 1 GREEN:** `2343996` adds the controller, interface, generator, migration, and saga-state extension. `dotnet test --filter "FullyQualifiedName~Receipt|FullyQualifiedName~QuestPdfBookingReceiptGenerator"` → 7/7 passed.
- **Task 2 RED:** `9c6c8eb` adds `tests/account/dashboard-tabs.test.tsx`, `tests/account/empty-state.test.tsx`, and three Playwright specs. `pnpm test -- --run tests/account` failed with two module-resolution errors on the missing components.
- **Task 2 GREEN:** `00cfb20` adds the auth pages, dashboard RSCs, components, keycloak-admin helper, formatters, types, and ambient shims. `pnpm test -- --run tests/account` → 10/10 passed; `pnpm typecheck` → clean.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 — Bug] QuestPDF content-stream assertions initially failed with naive byte-search**
- **Found during:** Task 1 GREEN phase verification.
- **Issue:** Initial implementation of `QuestPdfBookingReceiptGeneratorTests` asserted that rendered text (`"Base fare"`, `"PNR123"`, etc.) appeared as ASCII substrings of the PDF bytes. QuestPDF FlateDecode-compresses content streams, so the substring was never present and every test failed despite the PDF being correct.
- **Fix:** Added `PdfPig 0.1.10` to `tests/BookingService.Tests/BookingService.Tests.csproj` and rewrote the tests to use `PdfDocument.Open(bytes)` + `page.Text` extraction. The assertions now compare decompressed text, which is the right thing to assert. No production code changed.
- **Files modified:** `tests/BookingService.Tests/BookingService.Tests.csproj` (+1 PackageReference), `tests/BookingService.Tests/QuestPdfBookingReceiptGeneratorTests.cs` (rewrote `ExtractText` helper).
- **Commit:** included in `2343996`.

**2. [Rule 3 — Blocking] TypeScript typecheck failed on starterKit `.jsx` components**
- **Found during:** Task 2 GREEN verification (`pnpm typecheck`).
- **Issue:** `tsc` with `allowJs: true` + `checkJs: false` still synthesises parameter types from `.jsx` function signatures. Because the Metronic 9 starterKit components use defaulted destructured props (`function Button({ variant = 'primary', className, ... }) {}`), every destructured identifier was inferred as required. Every call site in the new pages (`<Button>...</Button>`, `<Tabs>...</Tabs>`) failed with "missing properties: selected, variant, shape, appearance, ...".
- **Fix:** Added `src/portals/b2c-web/types/ui.d.ts` with ambient `declare module` shims for the three starterKit modules actually imported in this plan (`@/components/ui/button`, `@/components/ui/tabs`, `@/components/ui/sonner`, `@/components/ui/tooltip`). Each export is typed as `ComponentType<AnyProps>` where `AnyProps` accepts `children + className + [key: string]: unknown`. This preserves the Metronic contract (components are prop-bag transparent) without rewriting the components themselves — matching Pitfall 17's "ship .jsx untouched" rule.
- **Files modified:** `src/portals/b2c-web/types/ui.d.ts` (new).
- **Commit:** included in `00cfb20`.

**3. [Rule 3 — Blocking] `server-only` package not installed**
- **Found during:** Task 2 GREEN, writing `lib/keycloak-admin.ts`.
- **Issue:** The file initially started with `import 'server-only';` to prevent accidental client-bundle inclusion (the recommended Next.js pattern). The package is not installed in `src/portals/b2c-web/package.json` and adding a new dependency mid-plan is out of scope.
- **Fix:** Replaced the import with an equivalent runtime guard: `if (typeof window !== 'undefined') throw new Error(...)` at module scope. Node and Edge runtimes both leave `window` undefined, so the guard fires only if the module is ever pulled into a client bundle — same semantic as `server-only`, one less dependency.
- **Files modified:** `src/portals/b2c-web/lib/keycloak-admin.ts`.
- **Commit:** included in `00cfb20`.

### User-Visible Design Choices (none — everything in the plan was executed literally)

- D-17 convenience route shipped exactly as specified: `/customers/me/bookings` delegates to `ListForCustomerAsync` after resolving `customerId` from `ClaimTypes.NameIdentifier ?? sub`. The original `/customers/{customerId}/bookings` is preserved for backoffice-staff.

### Open Items / Not in Scope

- **`departureDate` and `productType` on `BookingDtoPublic`.** The dashboard partitioner uses these fields, but `BookingDtoPublic` does not carry them yet. Frontend falls back to `createdAt` and the reference prefix respectively. Plans 04-02 (flight checkout) and 04-03 (hotel checkout) must add these columns when they populate actual departure dates into the saga state. Logged in `.planning/phases/04-b2c-portal-customer-facing/deferred-items.md` (see below).
- **Live Keycloak e2e runs.** All three Playwright specs (`register-login`, `password-reset`, `receipt-download`) self-skip unless `TEST_KC_USER` / `KEYCLOAK_ISSUER` / `TEST_BOOKING_ID` are set. The current CI agent does not expose a live Keycloak. Opt-in runs are expected on the integration runner that 04-00 will provision.
- **`tbe-b2c-admin` client secret provisioning.** The plan's `user_setup` block requires `KEYCLOAK_B2C_ADMIN_CLIENT_ID` / `KEYCLOAK_B2C_ADMIN_CLIENT_SECRET`. The realm JSON (`infra/keycloak/realm-tbe-b2c.json`) already declares the client with serviceAccountsEnabled; an operator must pull the real secret from the Keycloak admin console into `.env`. This matches the open blocker from 04-00's continue-here note.

## Verification

- **Backend:** `dotnet test tests/BookingService.Tests/BookingService.Tests.csproj --filter "FullyQualifiedName~Receipt|FullyQualifiedName~QuestPdfBookingReceiptGenerator"` → **7 passed, 0 failed**.
- **Frontend unit:** `cd src/portals/b2c-web && pnpm test` → **10 passed / 3 files (smoke + account)**.
- **Frontend typecheck:** `pnpm typecheck` → clean, no errors.
- **Acceptance greps (plan §Task 1 + Task 2 acceptance_criteria):**
  - `grep -c 'class ReceiptsController' ReceiptsController.cs` → 1.
  - `grep -c 'LicenseType.Community' QuestPdfBookingReceiptGenerator.cs` → 1.
  - `grep -c 'TODO(prod)'` → 1.
  - `grep -c "IBookingReceiptPdfGenerator, QuestPdfBookingReceiptGenerator" Program.cs` → 1.
  - `grep -cE 'HttpGet\("\/customers\/me\/bookings"\)' BookingsController.cs` → 1.
  - `grep -c "signIn('keycloak'" login/page.tsx` → 1.
  - `grep -c "No upcoming trips" dashboard-tabs.tsx` → 1.
  - `grep -c "Your booking history will show here once you have completed a trip" account/` → 1.
  - `grep -c "export const dynamic = 'force-dynamic'" bookings/page.tsx` → 1.
  - `grep -c "await params" bookings/[id]/page.tsx` → 1.
  - `grep -c "new Response(upstream.body" receipt.pdf/route.ts` → 2 (file contains both a direct call and a docstring reference).
  - `grep -c "send-verify-email" keycloak-admin.ts` → 3 (function name, comment, fetch URL).
  - `grep -c "client_credentials" keycloak-admin.ts` → 3 (grant_type, docstring, comment).
  - `grep -c "Intl.NumberFormat" formatters.ts` → 2 (money + date).

## Threat Flags

No new security surface introduced outside the plan's `<threat_model>`. The mitigations for T-04-01-01 through T-04-01-08 are in place (see acceptance greps above); T-04-01-09 remains `accept` — Keycloak's own rate limiting governs verify-email brute force.

## Known Stubs

- **`app/bookings/[id]/page.tsx` itinerary section** renders only the fields present in `BookingDtoPublic` (reference, PNR, ticket, date, total). Per-product itinerary detail (origin/destination airports for flights, property name + check-in/check-out for hotels) is intentionally stubbed — plan 04-02 (flight checkout) and 04-03 (hotel checkout) will populate the saga state with those fields, and a follow-on UI plan will render them. The Download-receipt CTA is fully wired end-to-end.

## Self-Check: PASSED

File existence:
- `src/services/BookingService/BookingService.API/Controllers/ReceiptsController.cs` → FOUND
- `src/services/BookingService/BookingService.Infrastructure/Pdf/QuestPdfBookingReceiptGenerator.cs` → FOUND
- `src/portals/b2c-web/app/bookings/page.tsx` → FOUND
- `src/portals/b2c-web/app/bookings/[id]/page.tsx` → FOUND
- `src/portals/b2c-web/app/api/bookings/[id]/receipt.pdf/route.ts` → FOUND
- `src/portals/b2c-web/app/api/auth/resend-verification/route.ts` → FOUND
- `src/portals/b2c-web/lib/keycloak-admin.ts` → FOUND
- `src/portals/b2c-web/components/account/dashboard-tabs.tsx` → FOUND

Commits:
- `785fee3` → FOUND (Task 1 RED)
- `2343996` → FOUND (Task 1 GREEN)
- `9c6c8eb` → FOUND (Task 2 RED)
- `00cfb20` → FOUND (Task 2 GREEN)
