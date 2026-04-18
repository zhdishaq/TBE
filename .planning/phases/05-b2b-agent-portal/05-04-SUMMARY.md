---
phase: 05-b2b-agent-portal
plan: 04
subsystem: booking-service+b2b-web
tags: [b2b, ttl-alerts, dashboard, bookings, void, agency-invoice, d-34, d-39, d-43, d-44, pitfall-10, pitfall-11, pitfall-14, pitfall-26, pitfall-28, complete]
requires:
  - 05-00
  - 05-01
  - 05-02
  - 05-03
provides:
  - TicketingDeadlineWarning B2B contract (24h horizon)
  - TicketingDeadlineUrgent B2B contract (2h horizon)
  - TtlMonitorHostedService B2B publish extension
  - AgencyDashboardController (GET /api/dashboard/summary)
  - AgencyInvoiceDocument (QuestPDF GROSS-only template, D-43)
  - InvoicesController (GET /api/b2b/invoices/{id}.pdf)
  - AgentBookingsController.VoidAsync (D-39 post-ticket 409, Pitfall 10 404)
  - AgentBookingsController extended filters (client, PNR, status, from/to, 20/50/100)
  - TicketingDeadlineConsumer (24h amber + 2h urgent email fan-out)
  - IKeycloakB2BAdminClient (BookingService-owned, role intersection)
  - LoggerTicketingDeadlineEmailSender (MVP stub)
  - B2BAdminPolicy registration in BookingService.API
  - b2b-web /dashboard RSC (TtlAlertsCard + WalletSummaryCard + RecentBookingsCard + QuickLinksGrid)
  - b2b-web /bookings (filters + table + pager + URL-sync debounce)
  - b2b-web /bookings/[id] (BookingStatusCard + TtlCountdown + VoidBookingButton + DocumentsPanel)
  - b2b-web /forbidden generic access-denied page (Pitfall 10)
  - b2b-web route-handler proxies (bookings list, void, invoice.pdf, e-ticket.pdf, dashboard/summary)
affects:
  - src/shared/TBE.Contracts/Messages/
  - src/services/BookingService/BookingService.API/
  - src/services/BookingService/BookingService.Application/
  - src/services/BookingService/BookingService.Infrastructure/
  - src/portals/b2b-web/app/(portal)/dashboard/
  - src/portals/b2b-web/app/(portal)/bookings/
  - src/portals/b2b-web/app/api/
  - src/portals/b2b-web/app/forbidden/
  - src/portals/b2b-web/components/bookings/
  - src/portals/b2b-web/components/dashboard/
tech-stack:
  added: []
  patterns:
    - "QuestPDF 2026.2.4 and PdfPig 0.1.10 were already present from Plan 04-01 — no new NuGet references"
    - "Existing Phase-3 TicketingDeadlineApproaching publish preserved unchanged"
    - "Parallel B2B-flavoured contracts with distinct record types (MassTransit routes separate consumers)"
    - "Claim-driven agency-wide scope (D-34 — filter by agency_id ONLY, never by sub)"
    - "Fail-closed 401 on missing agency_id claim (Pitfall 28)"
    - "Deliberate duplication of Keycloak client between PaymentService and BookingService (~150 LOC beats shared-library coupling)"
    - "xUnit [Collection(\"QuestPDF\")] serialization for QuestPDF static-license-state race on Windows"
    - "Route-handler proxies stream upstream PDFs via new Response(upstream.body, ...) — never buffer (Pitfall 11)"
    - "Next.js 16 params Promise (Pitfall 14) — always await before destructure"
    - "/forbidden redirect on cross-tenant 404 (Pitfall 10) — never leak existence"
    - "Sticky aria-live counter for TtlCountdown — survives React batched renders during fake-timer act() bursts"
key-files:
  created:
    - src/shared/TBE.Contracts/Messages/TicketingDeadlineWarning.cs
    - src/shared/TBE.Contracts/Messages/TicketingDeadlineUrgent.cs
    - src/services/BookingService/BookingService.API/Controllers/AgencyDashboardController.cs
    - src/services/BookingService/BookingService.API/Controllers/InvoicesController.cs
    - src/services/BookingService/BookingService.Application/Pdf/IAgencyInvoicePdfGenerator.cs
    - src/services/BookingService/BookingService.Application/Consumers/TicketingDeadlineConsumer.cs
    - src/services/BookingService/BookingService.Application/Consumers/ITicketingDeadlineEmailSender.cs
    - src/services/BookingService/BookingService.Application/Keycloak/IKeycloakB2BAdminClient.cs
    - src/services/BookingService/BookingService.Infrastructure/Pdf/QuestPdfAgencyInvoiceGenerator.cs
    - src/services/BookingService/BookingService.Infrastructure/Keycloak/KeycloakB2BAdminClient.cs
    - src/services/BookingService/BookingService.Infrastructure/Keycloak/KeycloakB2BAdminOptions.cs
    - src/services/BookingService/BookingService.Infrastructure/Consumers/LoggerTicketingDeadlineEmailSender.cs
    - tests/BookingService.Tests/AgencyDashboardControllerTests.cs
    - tests/BookingService.Tests/AgencyInvoiceControllerTests.cs
    - tests/BookingService.Tests/AgencyInvoiceDocumentTests.cs
    - tests/BookingService.Tests/TicketingDeadlineConsumerTests.cs
    - tests/BookingService.Tests/TicketingDeadlineMonitorB2BTests.cs
    - src/portals/b2b-web/app/(portal)/bookings/page.tsx
    - src/portals/b2b-web/app/(portal)/bookings/bookings-list-client.tsx
    - src/portals/b2b-web/app/(portal)/bookings/[id]/page.tsx
    - src/portals/b2b-web/app/api/bookings/route.ts
    - src/portals/b2b-web/app/api/bookings/[id]/void/route.ts
    - src/portals/b2b-web/app/api/bookings/[id]/invoice.pdf/route.ts
    - src/portals/b2b-web/app/api/bookings/[id]/e-ticket.pdf/route.ts
    - src/portals/b2b-web/app/api/dashboard/summary/route.ts
    - src/portals/b2b-web/app/forbidden/page.tsx
    - src/portals/b2b-web/components/bookings/status-card.tsx
    - src/portals/b2b-web/components/bookings/ttl-countdown.tsx
    - src/portals/b2b-web/components/bookings/void-booking-button.tsx
    - src/portals/b2b-web/components/bookings/documents-panel.tsx
    - src/portals/b2b-web/components/bookings/filters.tsx
    - src/portals/b2b-web/components/bookings/table.tsx
    - src/portals/b2b-web/components/bookings/pager.tsx
    - src/portals/b2b-web/components/dashboard/ttl-alerts-card.tsx
    - src/portals/b2b-web/components/dashboard/wallet-summary-card.tsx
    - src/portals/b2b-web/components/dashboard/recent-bookings-card.tsx
    - src/portals/b2b-web/components/dashboard/quick-links-grid.tsx
    - src/portals/b2b-web/tests/components/bookings/ttl-countdown.test.tsx
    - src/portals/b2b-web/tests/components/bookings/pager.test.tsx
    - src/portals/b2b-web/tests/components/bookings/void-booking-button.test.tsx
    - src/portals/b2b-web/tests/components/dashboard/dashboard-cards.test.tsx
  modified:
    - src/services/BookingService/BookingService.API/Program.cs
    - src/services/BookingService/BookingService.Infrastructure/Ttl/TtlMonitorHostedService.cs
    - src/portals/b2b-web/app/(portal)/dashboard/page.tsx
    - tests/BookingService.Tests/QuestPdfBookingReceiptGeneratorTests.cs
decisions:
  - "D-34 agency-wide scope enforced in every new controller action — filter by agency_id claim ONLY, never UserId"
  - "Pitfall 28 fail-closed 401 on missing agency_id claim — dashboard, bookings, void, invoice, e-ticket"
  - "Pitfall 10 — 404 never 403 on cross-tenant; portal redirects to /forbidden rather than notFound() to avoid leaking booking existence"
  - "Pitfall 11 — PDF route handlers stream new Response(upstream.body, ...); never .blob() or .arrayBuffer()"
  - "Pitfall 14 — all [id] / [slug] route handlers + RSC pages await the params Promise"
  - "D-39 post-ticket void returns 409 RFC 7807 /errors/post-ticket-cancel-unsupported — verbatim pass-through by the proxy"
  - "D-43 GROSS-only agency invoice — PdfPig text extraction pins absence of NET / Markup / Commission / negative-sign literals"
  - "D-44 2-col dashboard grid + 20/50/100 pager + Radix-style destructive confirm dialog"
  - "Deliberate duplication of Keycloak B2B admin client between PaymentService and BookingService — ~150 LOC; avoids shared-library coupling"
  - "TicketingDeadlineConsumer re-resolves recipients fresh from Keycloak per message (anti-spoofing) — never trust the message body"
  - "BookingService Keycloak AllowedRoles default = [agent-admin, agent] (excludes agent-readonly) — fan-out goes to people who can act"
  - "xUnit [Collection(\"QuestPDF\")] serializes PDF tests on Windows to prevent the QuestPDF static license state collision"
  - "Sticky aria-live counter (ANNOUNCE_STICKY_TICKS=2) on TtlCountdown so polite is observable after a vi.advanceTimersByTime burst"
  - "bookings-list-client uses single syncUrl({patch}) helper + 300ms debounce; resets page=1 on any filter change"
metrics:
  duration: "~4h (RED + GREEN + continuation)"
  completed_date: "2026-04-18"
  tasks_completed: "4 / 4"
  waves: "A (backend scaffolding), B (agency invoice PDF + InvoicesController), C (consumer + Keycloak client + DI), D (portal surface)"
---

# Phase 5 Plan 05-04: Agency Invoice + Booking List + Dashboard Summary + Portal Surface

Complete execution. B2B-08, B2B-09, B2B-10 closed. No further deferrals.

## One-liner

Agency invoice PDF (GROSS-only, D-43), post-ticket void gate (D-39 409), TTL deadline email fan-out (24h amber + 2h urgent), D-34 agency-scoped bookings list + dashboard summary, full b2b-web portal surface (/dashboard + /bookings + /bookings/[id] + 5 route-handler proxies) — all under Pitfall 10 / 11 / 14 / 26 / 28 guardrails.

## What shipped

### Wave A — TTL monitor B2B extension + AgencyDashboardController (B2B-08 + B2B-09 publish half)

`TtlMonitorHostedService.PollOnceAsync` now fans out two events whenever a saga with `Channel.B2B` and non-null `AgencyId` crosses either TTL horizon:

| Horizon      | Existing Phase-3 contract      | New Plan 05-04 contract    | Consumer               |
| ------------ | ------------------------------ | -------------------------- | ---------------------- |
| 24h advisory | `TicketingDeadlineApproaching` | `TicketingDeadlineWarning` | `TicketingDeadlineConsumer` |
| 2h advisory  | `TicketingDeadlineApproaching` | `TicketingDeadlineUrgent`  | `TicketingDeadlineConsumer` |

**Why two contracts, not one discriminator** — MassTransit routes by record type. A guardrail test (`Contracts_differ_in_record_type`) prevents a future refactor from collapsing them.

**Crash-safety (T-05-04-07)** — the new `publish.Publish(...)` call sits in the same `foreach` loop as the existing `s.Warn24HSent = true` / `s.Warn2HSent = true` flag flip; a single `db.SaveChangesAsync(ct)` commits both windows' state at the end. MassTransit + EF outbox (Plan 03-01) guarantees atomicity.

`AgencyDashboardController` exposes `GET /api/dashboard/summary` with a single DTO replacing four chained portal lookups. Every `.Where(s => s.AgencyId == agencyId)` clause explicitly does NOT append `&& s.UserId == sub` — `GetSummaryAsync_scopes_by_agency_id_only_not_sub_D34` seeds two agencies and asserts no cross-leak.

### Wave B — AgencyInvoiceDocument + InvoicesController (B2B-08 invoice half)

`QuestPdfAgencyInvoiceGenerator` renders the agency invoice PDF with `AgencyInvoiceDocument` (QuestPDF). Deterministic invoice number: `INV-{agencyId[..8]}-{yyyyMMdd}-{bookingId[..6]}`.

**D-43 negative-grep** — `AgencyInvoiceDocumentTests.Never_renders_NET_markup_or_commission_strings` extracts text with PdfPig and fails if the rendered stream contains any of `"NET"`, `"Markup"`, `"Commission"`, or negative-sign literal. The PDF shows the gross total only — never net, never a markup line, never a commission.

**InvoicesController** (`GET /api/b2b/invoices/{id}.pdf`) streams the PDF under `B2BPolicy`:
- Owner → `application/pdf` + `Content-Disposition: inline; filename="invoice-{id}.pdf"`
- Cross-tenant → **404** (Pitfall 10 — NOT 403; never leak cross-agency existence)
- Unknown id → 404
- Missing/malformed agency_id claim → 401 (Pitfall 28)

**QuestPDF race fix** — adding concurrent QuestPDF tests alongside the existing `QuestPdfBookingReceiptGeneratorTests` triggered a flake on Windows from the QuestPDF static license state. Fixed by annotating both test classes with `[Collection("QuestPDF")]` so xUnit serializes their execution.

### Wave C — TicketingDeadlineConsumer + Keycloak client + AgentBookingsController.VoidAsync (B2B-09 email fan-out + B2B-10 void)

**`IKeycloakB2BAdminClient` duplicated from PaymentService into BookingService** — ~150 LOC of Keycloak Admin REST facade, token caching, and role intersection logic. Deliberate duplication rather than a shared library; the two clients have slightly different default role allow-lists (`BookingService` includes `agent` because it needs recipients who can *receive* a deadline alert, not just admin actions).

**`TicketingDeadlineConsumer`** implements both `IConsumer<TicketingDeadlineWarning>` (amber "Heads-up") and `IConsumer<TicketingDeadlineUrgent>` (red "URGENT") in a single class. On every message:
1. Re-resolve the recipient set **fresh from Keycloak** using only the `AgencyId` from the message body. The message body NEVER carries recipients — that would be trivially spoofable. (Anti-spoofing, T-05-04-07.)
2. If the intersection (agency_id attribute ∩ `[agent-admin, agent]` role) is empty → log + return (no skipped_messages on the exchange).
3. Otherwise fan out via `ITicketingDeadlineEmailSender` (currently `LoggerTicketingDeadlineEmailSender`, MVP Phase-5 stub; SendGrid template-id wiring lands in the email template plan).

Tests use MassTransit `ITestHarness` + `DuringAny` guard predicates to assert that the consumer was invoked, the Keycloak client was called, and the logger sink received the correct `TicketingDeadlineHorizon` enum value (`Warning | Urgent`).

**`AgentBookingsController.VoidAsync(Guid id, CancellationToken ct)`** under `[Authorize(Policy="B2BAdminPolicy")]`:
- Missing agency_id claim → 401 (Pitfall 28)
- Non-admin role → 403 (policy-level, before action body)
- Cross-tenant / unknown id → **404** (Pitfall 10 — never 403)
- **Post-ticket (TicketNumber set) → 409 RFC 7807 problem+json** with `type=/errors/post-ticket-cancel-unsupported` (D-39)
- Pre-ticket → `VoidRequested` saga event + `202 AcceptedAtAction`

**`AgentBookingsController.ListForAgencyAsync` extended filters**: client-name contains-filter, PNR equals-filter, status-filter, `from` / `to` `DateTime?` bracket, page size capped at 20/50/100. Backend filter clause pins `.Where(s => s.AgencyId == agencyId)` as the outermost filter — **≥5 D-34 grep hits** across the controller now.

**`B2BAdminPolicy` registered in `BookingService.API/Program.cs`** — one-line copy-paste from PaymentService's Plan 05-03 registration. Role claim = `agent-admin`.

### Wave D — b2b-web portal surface (B2B-08/09/10 UI)

16 Next.js 16 files under `src/portals/b2b-web/` ship the full agent-portal surface:

**`/dashboard` (RSC page + 4 cards)** — 2-col grid (D-44). `Promise.all` server-side fetch against `/api/dashboard/summary` + `/api/wallet/balance`; graceful empty-state on 502.
- `TtlAlertsCard` — amber `Warning24hTtlCount` + red `UrgentTtlCount` buckets; "No upcoming ticketing deadlines" when both zero.
- `WalletSummaryCard` — `formatMoney(balance, currency)` + low-balance banner when balance ≤ threshold; "Manage wallet" CTA gated to `agent-admin`.
- `RecentBookingsCard` — 5-row table.
- `QuickLinksGrid` — 4 tiles with `adminOnly` filter hiding `/admin/agents` + `/admin/wallet` from non-admins.

**`/bookings` (RSC page + client island + 4 components)** — filters bar (client, PNR, status, from/to), table, pager.
- `bookings-list-client.tsx` — `'use client'` island; single `syncUrl({patch})` helper pushes URL via `router.replace`, 300ms debounce on filter changes, resets `page=1` on any filter change.
- Size param clamped to `[20, 50, 100]` server-side regardless of URL input.

**`/bookings/[id]` (RSC page + 5 components)** — status card, TTL countdown, void button, documents panel.
- Pitfall 14 — `params: Promise<{id: string}>`, always awaited.
- Pitfall 10 — upstream 404 → `redirect('/forbidden')`, NOT `notFound()`; the generic page never confirms whether the booking exists under another agency.
- `TtlCountdown` — `'use client'`, 1s `setInterval`, `Xd Yh Zm Ws` format, amber < 24h, red < 2h, aria-live sticky-polite counter (survives batched React renders in test fake-timer act() bursts), `onThresholdCross('warn' | 'urgent')` fired once per boundary.
- `VoidBookingButton` — hidden unless `agent-admin`; Radix-style `alertdialog`; POST `/api/bookings/{id}/void`; 202 → `router.refresh()`, 409 → "already ticketed" alert copy, other → generic error.

**5 route-handler proxies** — all server-side, all inject access-token via `gatewayFetch`, all fail-closed on missing `agency_id` (Pitfall 26).
- `/api/bookings` (GET) — forwards query-string.
- `/api/bookings/[id]/void` (POST) — forwards status + body verbatim (RFC 7807 pass-through for 409).
- `/api/bookings/[id]/invoice.pdf` (GET) — **stream-through** `new Response(upstream.body, ...)` with `Content-Disposition` + `Content-Type: application/pdf`. Pitfall 11 — never `.blob()` / `.arrayBuffer()`.
- `/api/bookings/[id]/e-ticket.pdf` (GET) — same pattern.
- `/api/dashboard/summary` (GET) — thin pass-through; backend is agency-scoped via the token's `agency_id` claim (D-34); we never pass `agency_id` from the browser.

**`/forbidden`** — static "Access denied" page with a link back to `/bookings`. Never reads the requested id; timing + response shape carry no information.

## Commits

| Hash    | Type  | Scope  | Summary                                                                                         |
| ------- | ----- | ------ | ----------------------------------------------------------------------------------------------- |
| 5fa8ca3 | test  | 05-04  | Failing tests for B2B TTL monitor extensions + AgencyDashboardController (RED)                  |
| 8469e72 | feat  | 05-04  | B2B TTL monitor contracts + agency dashboard summary (GREEN — Wave A)                           |
| 77ae647 | feat  | 05-04  | Wave A backend scaffolding for agency-scoped bookings + void + filters                          |
| e9b8165 | feat  | 05-04  | Wave B — AgencyInvoiceDocument (D-43 GROSS-only) + InvoicesController + PdfPig negative-grep    |
| 34e3e15 | feat  | 05-04  | Wave C — TicketingDeadlineConsumer + IKeycloakB2BAdminClient + LoggerTicketingDeadlineEmailSender |
| 4491995 | test  | 05-04  | Wave D RED — b2b-web component test suite (ttl-countdown + pager + void-booking + dashboard)    |
| 18b5b77 | feat  | 05-04  | Wave D GREEN — B2B portal dashboard + bookings surface + route-handler proxies                  |

## Tests

- **BookingService.Tests**: 72 / 72 passing on HEAD (up from 49 at Wave A; +23 net across the phase). New tests: `AgencyInvoiceDocumentTests` (3), `AgencyInvoiceControllerTests` (5), `TicketingDeadlineConsumerTests` (4), `AgencyDashboardControllerTests` (3), `TicketingDeadlineMonitorB2BTests` (5), plus expanded `AgentBookingsControllerTests` for void + filters.
- **b2b-web vitest**: 62 / 62 passing on HEAD. Wave D adds 22 new facts: `ttl-countdown` (5), `pager` (5), `void-booking-button` (3), `dashboard-cards` (9).
- **QuestPDF race**: pre-existing `QuestPdfBookingReceiptGeneratorTests.Generate_includes_fare_yqyr_tax_breakdown` flaked on Windows once `AgencyInvoiceDocumentTests` was added. Fixed by annotating both test classes with `[Collection("QuestPDF")]`.

## Deviations from Plan

### Auto-fixed

**1. [Rule 1 — test infra bug] `VoidBookingButton` test missing `next/navigation` mock**
- **Found during:** Wave D GREEN verification.
- **Issue:** `useRouter()` throws `invariant expected app router to be mounted` under plain React-Testing-Library renders (no Next app-router provider).
- **Fix:** Added a module-level `vi.mock('next/navigation', () => ({ useRouter: () => ({ refresh, push, replace, back, forward, prefetch }) }))` in `void-booking-button.test.tsx`.
- **Files modified:** `src/portals/b2b-web/tests/components/bookings/void-booking-button.test.tsx`
- **Commit:** 18b5b77

**2. [Rule 1 — render/state-batch bug] `TtlCountdown` aria-live flickered off during fake-timer bursts**
- **Found during:** Wave D GREEN verification.
- **Issue:** `vi.advanceTimersByTime(2000)` inside `act(...)` fires two `setInterval` callbacks back-to-back; React batches state updates and the second tick's `setAnnounceThisTick(false)` overwrote the first tick's `setAnnounceThisTick(true)` before any DOM commit. Net result: `aria-live="off"` after a cross.
- **Fix:** Replaced the boolean `announceThisTick` state with a counter `announceFrames` (initial `0`). On cross → `setAnnounceFrames(ANNOUNCE_STICKY_TICKS /* 2 */)`. On non-cross → `setAnnounceFrames((prev) => prev > 0 ? prev - 1 : 0)`. Render `polite` while counter > 0. Two batched updates now resolve to `2 → 1` (still polite); a subsequent non-cross tick renders `1 → 0` (off).
- **Files modified:** `src/portals/b2b-web/components/bookings/ttl-countdown.tsx`
- **Commit:** 18b5b77

**3. [Rule 1 — copy mismatch] `VoidBookingButton` 409 alert copy failed the regex**
- **Found during:** Wave D GREEN verification.
- **Issue:** Test regex `/already ticketed/i` did not match the original copy `"already been ticketed"` (word "been" between "already" and "ticketed").
- **Fix:** Updated the copy to include the literal phrase "already ticketed": `"This booking is already ticketed — post-ticket cancellation is not supported. Please contact support to process the refund."`
- **Files modified:** `src/portals/b2b-web/components/bookings/void-booking-button.tsx`
- **Commit:** 18b5b77

**4. [Rule 2 — test hygiene] Removed RED placeholders from Notifications.Tests**
- **Found during:** Wave B.
- **Issue:** Earlier plan scaffolding staged `tests/Notifications.Tests/AgencyInvoiceControllerTests.cs` + `AgencyInvoiceDocumentTests.cs` expecting the invoice to live in NotificationService. Plan 05-04 pivoted the invoice to BookingService (where `BookingSagaState` lives — saving a cross-service RPC).
- **Fix:** Deleted the placeholders; real tests ship in `tests/BookingService.Tests/`.
- **Commit:** e9b8165

**5. [Rule 3 — naming reconciliation] Reused existing `Warn2HSent` flag name**
- **Found during:** Wave A.
- **Issue:** Plan specifies `Urgent2HSent` flag; existing field from Plan 03-03 is `Warn2HSent`.
- **Fix:** Kept existing name — renaming would require a breaking EF migration for zero semantic difference.
- **Commit:** 8469e72

**6. [Rule 3 — scope reconciliation] Extended `TtlMonitorHostedService` rather than introducing `TicketingDeadlineMonitor`**
- **Found during:** Wave A.
- **Issue:** Plan frontmatter lists `TicketingDeadlineMonitor.cs` as a new file; the existing service already publishes at both horizons with the exact flag semantics.
- **Fix:** Extended the existing class; plan action point 7 permits this. Zero duplicate-publish risk.
- **Commit:** 8469e72

**7. [Rule 2 — architecture call] Duplicated Keycloak B2B admin client into BookingService**
- **Found during:** Wave C.
- **Issue:** PaymentService already owns a `KeycloakB2BAdminClient` (Plan 05-03); BookingService needs one to fan out TTL email recipients. Options were (a) introduce a shared library, or (b) duplicate ~150 LOC.
- **Fix:** Chose (b). The two clients have different default `AllowedRoles` defaults (`BookingService` includes `agent`, `PaymentService` excludes it because wallet alerts target admins only). Coupling them in a shared library would require a config-surface split anyway.
- **Files added:** `BookingService.Application/Keycloak/IKeycloakB2BAdminClient.cs`, `BookingService.Infrastructure/Keycloak/KeycloakB2BAdminClient.cs`, `KeycloakB2BAdminOptions.cs`.
- **Commit:** 34e3e15

### Deferred — none

All items previously listed under "From Plan 05-04" in `deferred-items.md` shipped in this session. The section was removed from `deferred-items.md` and the audit note updated to reflect that the consumer is now wired in `Program.cs`.

### Summary of completion vs plan

| Plan surface               | Status   |
| -------------------------- | -------- |
| TicketingDeadlineWarning   | Shipped  |
| TicketingDeadlineUrgent    | Shipped  |
| TTL monitor B2B publish    | Shipped  |
| AgencyDashboardController  | Shipped  |
| D-34 enforcement (≥5 hits) | Shipped  |
| Pitfall 28 fail-closed 401 | Shipped  |
| AgencyInvoiceDocument      | Shipped  |
| InvoicesController         | Shipped  |
| VoidAsync (D-39 + P-10)    | Shipped  |
| B2BAdminPolicy reg         | Shipped  |
| TicketingDeadlineConsumer  | Shipped  |
| IKeycloakB2BAdminClient    | Shipped  |
| b2b-web /dashboard         | Shipped  |
| b2b-web /bookings + filters| Shipped  |
| b2b-web /bookings/[id]     | Shipped  |
| b2b-web /forbidden         | Shipped  |
| Route handlers (5)         | Shipped  |

Requirements B2B-08 / B2B-09 / B2B-10 closed.

## Known Stubs

| File                                          | Reason                                                                                |
| --------------------------------------------- | ------------------------------------------------------------------------------------- |
| `LoggerTicketingDeadlineEmailSender.cs`       | MVP stub — real SendGrid sender lands in the B2B email-template plan. Logs + returns. |

No UI stubs — every card reads real data, every route handler proxies a real backend.

## TDD Gate Compliance

- RED commits (tests first):
  - `5fa8ca3` — TTL B2B contracts + AgencyDashboardController (Wave A RED)
  - `1bb77a2` (inherited from 05-03 chain) + Wave B RED folded into `e9b8165` because AgencyInvoice tests were previously placeholders in the wrong project
  - `d8ed7f2` (inherited) + Wave C RED folded into `34e3e15`
  - `4491995` — Wave D RED for all 4 b2b-web suites (22 facts all initially failing)
- GREEN commits (implementation):
  - `8469e72`, `77ae647`, `e9b8165`, `34e3e15`, `18b5b77`
- REFACTOR commits — none needed.

## Threat Flags

None. All new surface was in the plan's `<threat_model>`.

## Self-Check: PASSED
