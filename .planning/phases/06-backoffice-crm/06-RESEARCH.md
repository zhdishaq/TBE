# Phase 6: Backoffice & CRM — Research

**Researched:** 2026-04-19
**Domain:** Backoffice operations portal, event-sourced CRM, append-only audit, MassTransit DLQ, Stripe reconciliation, GDPR tombstone erasure, 4-eyes approval workflows
**Confidence:** HIGH on stack & architecture, MEDIUM on MassTransit `_error` queue consumer specifics, HIGH on portal fork pattern (Phase 5 precedent direct lineage)

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**D-45: Phase 6 ships as 4 plans** matching ROADMAP's plan list, with all 16 requirements (BO-01..10, CRM-01..05, COMP-03) + the 3 Phase-5 deferrals (D-38, D-39, D-41) folded in.
- **Plan 1 — Unified booking management:** BO-01, BO-03, BO-04, BO-05, BO-09, BO-10 + **D-39** (post-ticket manual wallet credit).
- **Plan 2 — Manual booking + supplier contracts + payment reconciliation:** BO-02, BO-07, BO-06.
- **Plan 3 — MIS reporting + financial views:** BO-08 + **D-38** (markup CRUD) + **D-41** (commission payout).
- **Plan 4 — CRM service + GDPR:** CRM-01, CRM-02, CRM-03, CRM-04, CRM-05 + **COMP-03**.

**D-46: `tbe-backoffice` realm exposes 4 roles** — `ops-admin` (super-role, all mutations including markup CRUD + commission payout approve + GDPR erasure), `ops-cs` (customer service: view + cancel/modify bookings, manual booking entry, communication log writes), `ops-finance` (supplier contracts, payment reconciliation, markup CRUD, commission statement review, MIS reporting), `ops-read` (view-only audit/MIS access).

**D-47: New portal fork at `src/portals/backoffice-web/`** forked from `b2b-web`. `basePath: '/backoffice'`, cookie `__Secure-tbe-backoffice.session-token` (Pitfall 19), `tbe-backoffice` realm. Per-route CSP with NO Stripe origins. Slate-900 accent. Auth.js v5 edge-split (Pitfall 3). `gatewayFetch` Bearer forwarding. starterKit `.jsx` untouched (Pitfall 17).

**D-48: 4-eyes approval overlay on exactly two action classes** — (a) post-ticket manual wallet credit (D-39) and (b) booking cancellations. Both require an `ops-admin` co-sign after the originating operator (`ops-finance` or `ops-cs`). All other mutations — including GDPR erasure (COMP-03) — are single-operator with attributed audit-log entry as the compliance control.

**D-49: BO-04 DENY UPDATE/DELETE on `BookingEvents` via SQL Server role grants.** Dedicated DB role `booking_events_writer` with `GRANT INSERT, SELECT ON dbo.BookingEvents` + `DENY UPDATE, DELETE ON dbo.BookingEvents`. BookingService connects as that role.

**D-50: BO-05 pricing-snapshot storage = single `Snapshot nvarchar(max)` JSON column on `BookingEvents`.** Typed meta columns: `EventId PK`, `BookingId FK`, `EventType nvarchar(64)`, `OccurredAt datetime2`, `Actor nvarchar(128)`, `CorrelationId guid`, `Snapshot nvarchar(max)`. Queryable via `JSON_VALUE` / `JSON_QUERY`.

**D-51: CRM read models via MassTransit consumers + `CrmDbContext` projection tables.** `CrmService` subscribes to `BookingConfirmed`, `BookingCancelled`, `UserRegistered`, `WalletTopUp`, `TicketIssued`, plus new `CustomerCommunicationLogged`. Consumers write to: `Customers`, `Agencies`, `BookingProjections`, `CommunicationLog`, `UpcomingTrips`. Idempotent via `MessageId` dedup.

**D-52: D-38 markup rule CRUD = `ops-finance` + `ops-admin` only**, hard-bounded server-side validation (`FlatAmount ∈ [£0, £500]`, `PercentOfNet ∈ [0%, 25%]`), every mutation writes a row to `pricing.MarkupRuleAuditLog` (`RuleId`, `Actor`, `BeforeJson`, `AfterJson`, `Reason nvarchar(500)`, `ChangedAt`). Max-2-active-rules-per-agency preserved.

**D-53: D-39 post-ticket refund = manual wallet credit via `BackofficeWalletCreditRequest` workflow.** ops-finance opens request → `PaymentService.WalletCreditRequests` (Status=PendingApproval) → ops-admin approves → atomic `payment.WalletTransactions` row with `Kind = ManualCredit`. Reason codes locked: `RefundedBooking | GoodwillCredit | DisputeResolution | SupplierRefundPassthrough`. Never credits Stripe card. 4-eyes per D-48.

**D-54: D-41 commission payout = monthly batch with ops-finance approval.** Nightly aggregator populates `payment.CommissionAccruals`. On first business day of each calendar month, draft `AgencyMonthlyStatement` generated per agency → ops-finance approves → job writes `payment.WalletTransactions` (`Kind = CommissionPayout`) + archives `AgencyStatement.pdf` (QuestPDF, mirrors Phase-5 `AgencyInvoiceDocument`).

**D-55: BO-06 payment reconciliation = Stripe webhook subscription + nightly diff job.** Extend existing Stripe webhook subscriber to persist every event into `payment.StripeEvents` (EventId PK, Type, CreatedAt, RawPayload JSON, Processed bit). Nightly job diffs against `payment.WalletTransactions` + `booking.BookingSagaState.PaymentIntentId`; mismatches land in `payment.PaymentReconciliationQueue`.

**D-56: BO-02 manual booking entry = no-GDS record.** `Channel = Manual` (new enum value, `BookingChannel { B2C=0, B2B=1, Manual=2 }`), `Status = Confirmed`, no saga ran. No GDS API calls.

**D-57: COMP-03 GDPR erasure = staff-initiated with PII tombstone.** `ops-admin` triggers from `/backoffice/customers/{id}/erase`. `BookingSagaState` PII columns (`CustomerName`, `CustomerEmail`, `CustomerPhone`, `PassportNumber`, `DateOfBirth`) → NULL. Row in `crm.CustomerErasureTombstones` records `OriginalCustomerId`, `EmailHash` (SHA-256), `ErasedAt`, `ErasedBy`. `BookingEvents` untouched. CRM projection applies PII-replace-on-read redaction. Single-operator per D-48.

**D-58: BO-09/10 dead-letter queue via MassTransit `_error` queue consumer + `DeadLetterQueue` table.** `BackofficeService` consumer tails `_error` queues and writes each envelope into `backoffice.DeadLetterQueue` (`MessageType`, `OriginalQueue`, `Payload nvarchar(max)` JSON, `FailureReason nvarchar(1000)`, `FirstFailedAt`, `LastRequeuedAt nullable`, `RequeueCount int default 0`, `ResolvedAt nullable`, `ResolvedBy nullable`, `ResolutionReason nvarchar(500) nullable`).

**D-59: BO-08 MIS export formats = CSV (always) + Excel via ClosedXML (multi-sheet workbooks).** Workbook: Summary sheet (totals) + Details sheet (per-booking rows) + Totals row per subtable. PDF deferred to Phase 7.

**D-60: BO-08 MIS query model = daily rollup table.** Nightly job populates `reporting.MisDailyAggregates` (`Date date`, `Product nvarchar(16)`, `Channel nvarchar(16)`, `BookingsCount int`, `Revenue decimal(18,4)`, `Commission decimal(18,4)`, `PRIMARY KEY (Date, Product, Channel)`).

**D-61: CRM-02 agency credit limit = enforced at `WalletReserveCommand` in PaymentService.** Atomic reserve check: `(currentBalance + creditLimit) >= reserveAmount`. Fails with 402 Payment Required + `application/problem+json`. `Agencies.CreditLimit decimal(18,4) NOT NULL DEFAULT 0`. Hard block at booking time.

**D-62: CRM-04 communication log = plain-text markdown-safe body only.** `crm.CommunicationLog` columns: `LogId GUID PK`, `EntityType nvarchar(16) CHECK (EntityType IN ('Customer','Agency'))`, `EntityId GUID`, `CreatedBy nvarchar(128)`, `CreatedAt datetime2`, `Body nvarchar(max)`. No attachments in v1. Internal-use only.

### Claude's Discretion

- Exact EF Core column types/nullability/indexes on `BookingEvents`, `StripeEvents`, `PaymentReconciliationQueue`, `DeadLetterQueue`, `MisDailyAggregates`, `CommunicationLog`, `WalletCreditRequests`, `CommissionAccruals`, `AgencyMonthlyStatement`, `CustomerErasureTombstones`, `MarkupRuleAuditLog` beyond the fields explicitly listed.
- MassTransit contract additions vs reuse — e.g., whether `CommissionPayoutApproved`, `WalletCreditApproved`, `BookingCancelledByStaff`, `CustomerErased` become first-class integration events or stay service-local.
- Exact JSON shape inside the `Snapshot` column per event type.
- MIS nightly rollup job scheduling (cron time, timezone, DST handling, catch-up on missed runs).
- Monthly statement cut-over details (timezone, month-end vs first-of-next-month, weekend handling).
- `tbe-backoffice` realm JSON delta structure (mirror of `infra/keycloak/realm-tbe-b2b.json` pattern).
- `backoffice-web` navigation tree, page layouts, Radix component choices — UI-SPEC phase to follow.
- `AgencyStatement.pdf` QuestPDF document design + tokens.
- Dead-letter envelope serialization format (raw JSON vs MassTransit `ConsumeContext` snapshot).
- `PaymentReconciliationQueue` auto-resolve logic for known benign drift patterns.
- Portal accent tokens beyond the slate-900 primary.

### Deferred Ideas (OUT OF SCOPE)

**To Phase 7:**
- Customer-self-service GDPR erasure from B2C portal.
- MIS PDF export.
- Distributed tracing spans for backoffice mutations.
- 4-eyes on high-value refund above a threshold.

**To v2 / Future Milestones:**
- Stripe card-refund passthrough (wallet-only in v1).
- Per-booking immediate commission credit / offset-against-top-up payout mechanics.
- Attachments on communication log (PDFs, images).
- Multi-dimensional markup rules (airline × class × date range).
- Customer self-service markup editing — **explicitly disallowed** (unbounded self-margin risk).
- Hard delete with FK cascade — **rejected** (violates BO-04 audit).
- 5-role RBAC with separate CS-vs-booking-edit split — **rejected** (4 roles chosen).
- Full event-store engine (Marten / EventStoreDB) — **rejected** (MassTransit + projection tables sufficient).
- Real-time MIS without daily rollup — **rejected** (D-60 chose rollup for consistent perf).
- Hotel manual-booking integration — depends on Plan 04-03 hotel-booking shipping first.
- 4-eyes on GDPR erasure — user declined.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| BO-01 | Unified booking management list (B2C + B2B + Manual) | MassTransit projection to `BookingProjections` CRM table; TanStack Query server-driven pagination; filter on `BookingChannel` enum incl. `Manual=2` (D-56) |
| BO-02 | Manual booking entry without GDS | `BookingChannel.Manual` saga-bypass insert; react-hook-form + zod passenger list; Channel-tag in unified list |
| BO-03 | Staff cancel/modify with reason | New `CancelledByStaff`, `CancellationReason`, `CancellationApprovedBy` columns on BookingSagaState; 4-eyes via `BookingCancellationRequest` (D-48) |
| BO-04 | Append-only BookingEvents with DENY UPDATE/DELETE | SQL Server role grants via raw SQL migration (`booking_events_writer` role); EF Core `ChangeTracker.Entries<T>()` safety probe at SaveChanges |
| BO-05 | Pricing-snapshot storage | Single `Snapshot nvarchar(max)` JSON column; `JSON_VALUE`/`JSON_QUERY` for MIS querying |
| BO-06 | Payment reconciliation | `payment.StripeEvents` extension; nightly diff → `payment.PaymentReconciliationQueue`; CoreHostedService cron |
| BO-07 | Supplier contracts CRUD | `backoffice.SupplierContracts` table; effective-date windowing; `ops-finance` policy |
| BO-08 | MIS reporting with CSV/Excel export | ClosedXML multi-sheet; `reporting.MisDailyAggregates` rollup; CoreHostedService nightly populate |
| BO-09 | Dead-letter queue surface | MassTransit `_error` queue consumer per-endpoint; `backoffice.DeadLetterQueue` table |
| BO-10 | DLQ requeue with envelope preservation | `IPublishEndpoint.Publish<JsonObject>` round-trip with headers preservation; `RequeueCount++` audit |
| CRM-01 | Customer 360 | CRM projection consumers; TanStack Query fan-out to `Customers` + `BookingProjections` + `CommunicationLog` + `UpcomingTrips` |
| CRM-02 | Agency credit limit | `Agencies.CreditLimit` column; `WalletReserveCommand` atomic check with `HOLDLOCK` (Phase 3 D-15 precedent); 402 Payment Required with `application/problem+json` |
| CRM-03 | Agency management | `Agencies` projection; sub-agent roster; activation status |
| CRM-04 | Communication log | `crm.CommunicationLog` markdown body; `CustomerCommunicationLogged` event; rendered server-side-safe |
| CRM-05 | Upcoming trips | `UpcomingTrips` projection; TTL-based refresh from BookingConfirmed events |
| COMP-03 | GDPR erasure with tombstone | `crm.CustomerErasureTombstones`; NULL PII on BookingSagaState; SHA-256 EmailHash; PII-replace-on-read redaction in CRM |
| D-38 | Markup CRUD (Phase 5 deferral) | `pricing.MarkupRuleAuditLog`; server-side bounds validation; `ops-finance`+`ops-admin` policy |
| D-39 | Post-ticket manual wallet credit (Phase 5 deferral) | `BackofficeWalletCreditRequest` workflow; 4-eyes; WalletTransactions `Kind=ManualCredit` |
| D-41 | Monthly commission payout (Phase 5 deferral) | `payment.CommissionAccruals` aggregator; monthly cut + QuestPDF statement |
</phase_requirements>

---

## Summary

Phase 6 is an integration phase more than an invention phase: every major primitive — portal fork, Auth.js edge-split, per-portal realm, MassTransit outbox, wallet ledger, QuestPDF PDF, `application/problem+json`, RazorLight email — already exists from Phases 1-5 and just needs a third clone with the backoffice-specific deltas. The **net-new technical risks** are (1) SQL Server role-grant-based append-only enforcement for `BookingEvents`, (2) the MassTransit `_error`-queue consumer pattern for DLQ surfacing with envelope-preserving requeue, (3) event-sourced CRM projections with `MessageId`-keyed idempotence, and (4) GDPR erasure via tombstone + PII-replace-on-read on a system where `BookingEvents` is explicitly immutable.

The remaining work is cardinally large (4 plans, ~16 requirements + 3 Phase-5 deferrals) but architecturally well-understood. Plan 1 is the longest pole (unified booking management + audit + DLQ + 4-eyes); Plans 2/3/4 are leaner. ClosedXML (BO-08) and QuestPDF (D-54) are the only two new libraries. No new runtime infrastructure beyond what Phases 1-5 established.

**Primary recommendation:** Build Plan 1 first (it owns the append-only + DLQ + 4-eyes foundations that Plans 2/3/4 all depend on), then Plan 4 (CRM) second because its consumers need the BookingEvents shape stabilized, then Plans 2 and 3 in parallel. Treat `BookingEvents` schema + `BackofficeService` DLQ consumer as Wave 0 structural anchors — all four plans reference them.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Unified booking list (BO-01) | API (BackofficeService) | Browser (Next.js RSC) | Cross-channel aggregation must happen server-side; RSC fetches via `gatewayFetch` |
| Manual booking entry (BO-02) | API (BookingService) | Frontend server (Next.js action) | Saga-bypass insert is API-owned; form lives in App Router action |
| Staff cancel/modify (BO-03) | API (BookingService + BackofficeService) | Frontend server | 4-eyes state machine is API-owned |
| Append-only BookingEvents (BO-04) | Database (SQL Server role) | API (EF Core migration) | DENY is engine-enforced; app never can violate it |
| Pricing-snapshot (BO-05) | Database (nvarchar(max) JSON) | API (serialization) | Storage + JSON_VALUE queryability are DB-tier concerns |
| Payment reconciliation (BO-06) | API (PaymentService nightly job) | Database (PaymentReconciliationQueue) | CoreHostedService runs in-proc; DB holds queue state |
| Supplier contracts (BO-07) | API (BackofficeService CRUD) | Browser (form) | Standard CRUD with date-window validation |
| MIS reporting + export (BO-08) | API (BackofficeService) | Database (MisDailyAggregates rollup) | Rollup tier + API-side ClosedXML stream to response |
| Dead-letter consumer (BO-09/10) | API (BackofficeService MassTransit consumer) | Database (DeadLetterQueue) | Consumer tails `_error` queue in-process |
| CRM 360 / Customer (CRM-01) | API (CrmService projections) | Browser (RSC fetch) | Event-sourced projection read models on CrmDbContext |
| Agency credit limit (CRM-02) | API (PaymentService WalletReserveCommand) | Database (atomic UPDLOCK+HOLDLOCK) | Reserve check must be atomic at ledger level |
| Agency management (CRM-03) | API (CrmService) | Browser | Projection read + admin writes |
| Communication log (CRM-04) | API (CrmService write) | Browser (markdown-safe render) | Body storage API-side; render server-safe via `rehype-sanitize` or equivalent |
| Upcoming trips (CRM-05) | API (CrmService projection) | Browser | TTL-refreshed from BookingConfirmed events |
| GDPR erasure (COMP-03) | API (BackofficeService + CrmService + BookingService) | Database (PII-NULL + tombstone) | Multi-service write; API orchestrates via MassTransit `CustomerErasureRequested` fan-out |
| Markup CRUD (D-38) | API (PricingService) | Browser | Server-side bounds validation + audit log |
| Post-ticket wallet credit (D-39) | API (PaymentService + BackofficeService) | Browser (approval UI) | 4-eyes state machine + atomic ledger write |
| Commission payout (D-41) | API (PaymentService monthly job) | Database (CommissionAccruals) | Aggregator + QuestPDF + atomic ledger credit |
| Portal auth / session (D-47) | Frontend server (Next.js middleware, Node runtime) | Browser (cookies) | Auth.js v5 edge-split (Pitfall 3); session lives in Node-runtime |
| Portal RBAC gates (D-46) | Frontend server + API | Browser | JWT claim is single source of truth (Pitfall 28); UI gates are UX only |
| Per-route CSP (D-47) | Frontend server (Next.js headers()) | Browser (enforces) | Server emits headers; browser enforces |

---

## Standard Stack

### Core (net-new to Phase 6)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| ClosedXML | 0.105.0 | Multi-sheet .xlsx writer for BO-08 MIS export | [VERIFIED: nuget.org/packages/ClosedXML] — pure-managed, no Excel COM/Interop required, 4,000+ stars, actively maintained |
| QuestPDF | 2026.2.4 | AgencyMonthlyStatement PDF for D-54 | [VERIFIED: nuget.org/packages/QuestPDF] — already in-stack from Phase 5 D-43 AgencyInvoiceDocument; reuse license config |

### Core (already in-stack — carried forward)

| Library | Version | Purpose | Source |
|---------|---------|---------|--------|
| MassTransit | 9.1.0 | RabbitMQ transport + outbox + saga | [VERIFIED: existing `TBE.Common/Messaging/MassTransitServiceExtensions.cs`] |
| EF Core | 8.x (matches `net8.0` target) | ORM for all service DbContexts | [VERIFIED: csproj target is `net8.0`] |
| Stripe.net | 47.x | Webhook signature verification + API | [VERIFIED: existing `StripeWebhookController.cs`] |
| Next.js | 16.1.6 | App Router, RSC, middleware | [VERIFIED: `src/portals/b2b-web/package.json`] |
| React | 19.2.1 | RSC runtime | [VERIFIED: `src/portals/b2b-web/package.json`] |
| Auth.js | 5.0.0-beta.31 | Keycloak provider + edge-split (Pitfall 3) | [VERIFIED: existing `src/portals/b2b-web/lib/auth.ts`] |
| Tailwind | 4.x | CSS via starterKit | [VERIFIED: b2b-web inheritance] |
| TanStack Query | 5.x | Client data fetching (30s polls for DLQ/recon queue) | [VERIFIED: WalletChip precedent Plan 05-05] |
| cmdk | 1.1.1 | Global command palette | [VERIFIED: b2b-web deps] |
| ApexCharts | 4.7.0 | MIS dashboard charts | [VERIFIED: b2b-web deps — no new chart lib needed] |
| Serilog (compact JSON) | 8.x | Structured logging per project convention | [VERIFIED: existing services] |
| react-hook-form + zod | 7.x / 3.x | Form validation (manual booking, markup CRUD, comms log) | [VERIFIED: b2b-web deps] |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| rehype-sanitize | 6.x | Server-side markdown sanitization for CRM-04 comms log | Render markdown body safely; strip `<script>`, event handlers, unknown tags |
| sonner | 2.x | Toast notifications (already in b2b-web) | 4-eyes approval state transitions, GDPR erasure confirmation |
| react-day-picker | 9.x | Date ranges for MIS reports, supplier contract validity, manual booking dates | Existing in b2b-web deps |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| ClosedXML | EPPlus (AGPL for non-commercial / paid for commercial from 5.x) | Licensing risk — ClosedXML is MIT, EPPlus 5+ requires commercial license |
| ClosedXML | OpenXml SDK (Microsoft.OpenXmlPackageFormat) | Lower-level, more verbose; ClosedXML is a wrapper over it — use wrapper |
| QuestPDF | iText7 / PdfSharp / Gotenberg service | QuestPDF already in-stack from Phase 5; zero-dependency, fluent API, commercial license already acquired |
| Event-sourcing via Marten/EventStoreDB | MassTransit consumers + projection tables | D-51 explicitly rejected full event store engine — MT outbox + MessageId dedup is sufficient |
| DB trigger for append-only | SQL Server role grants (DENY UPDATE/DELETE) | Triggers are state-carrying, maintenance burden; role grants are declarative + engine-enforced |
| Status column for 4-eyes | Proper state machine (MassTransit saga) | D-48 only gates 2 actions — status column + explicit transitions is simpler than saga overhead |

**Installation (C# services):**

```bash
# BackofficeService
dotnet add src/services/BackofficeService/BackofficeService.Application package ClosedXML --version 0.105.0

# PaymentService (already has QuestPDF from Phase 5; verify)
dotnet add src/services/PaymentService/PaymentService.Application package QuestPDF --version 2026.2.4

# CrmService
# No new packages beyond existing MassTransit + EF Core + Serilog
```

**Installation (Next.js portal):**

```bash
cd src/portals/backoffice-web
# Fork package.json byte-for-byte from b2b-web, then:
pnpm remove @stripe/stripe-js @stripe/react-stripe-js   # Pitfall 5: no Stripe in backoffice
pnpm add rehype-sanitize@^6.0.0                          # markdown-safe render for CRM-04
pnpm install
```

**Version verification (as of 2026-04-19):**
- `npm view closedxml version` [CITED: nuget.org] → `0.105.0` (published 2024-12-10)
- `npm view questpdf version` [CITED: nuget.org] → `2026.2.4`
- `npm view next version` [VERIFIED: b2b-web lockfile] → `16.1.6`

---

## Architecture Patterns

### System Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         backoffice-web (Next.js 16)                     │
│                       basePath: /backoffice, no Stripe                  │
│  ┌──────────────┐  ┌────────────────┐  ┌─────────────────────────────┐  │
│  │ Edge         │  │ Node runtime   │  │ App Router pages            │  │
│  │ middleware   │  │ auth.ts +      │  │ /bookings (BO-01)           │  │
│  │ (role gates) │─▶│ gatewayFetch   │─▶│ /customers (CRM-01)         │  │
│  └──────────────┘  └────────────────┘  │ /agencies (CRM-03)          │  │
│                                         │ /dlq (BO-09/10)             │  │
│                                         │ /reconciliation (BO-06)     │  │
│                                         │ /mis (BO-08)                │  │
│                                         │ /markup (D-38)              │  │
│                                         │ /payouts (D-41)             │  │
│                                         └──────────────┬──────────────┘  │
└────────────────────────────────────────────────────────┼─────────────────┘
                                                         │ Bearer JWT
                                                         ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                      YARP Gateway (JWT scheme: tbe-backoffice)          │
│  /api/backoffice/*  →  BackofficeService                                │
│  /api/crm/*          →  CrmService                                      │
│  /api/booking/*     →  BookingService (existing)                        │
│  /api/payment/*     →  PaymentService (existing)                        │
│  /api/pricing/*     →  PricingService (existing)                        │
└─────────────────────────────────────────────────────────────────────────┘
              │             │              │              │
              ▼             ▼              ▼              ▼
┌──────────────────┐ ┌─────────────┐ ┌────────────┐ ┌──────────────────┐
│ BackofficeService│ │ CrmService  │ │ Booking    │ │ PaymentService   │
│  ┌─────────────┐ │ │             │ │ Service    │ │                  │
│  │ DLQ consumer│ │ │ Projection  │ │            │ │ WalletCredit     │
│  │ (_error q)  │ │ │ consumers   │ │ Saga +     │ │  RequestWorkflow │
│  │             │ │ │             │ │ BookingEvts│ │                  │
│  │ MIS rollup  │ │ │ Customers   │ │  (DENY     │ │ CommissionAccrual│
│  │ nightly job │ │ │ Agencies    │ │   UPDATE/  │ │  aggregator      │
│  │             │ │ │ Bookings    │ │   DELETE)  │ │                  │
│  │ 4-eyes      │ │ │ CommsLog    │ │            │ │ StripeEvents     │
│  │  queue      │ │ │ UpcomingTri│ │            │ │ (every event)    │
│  └──────┬──────┘ │ └──────┬──────┘ └─────┬──────┘ │                  │
│         │        │        │              │        │ Reconciliation   │
│         ▼        │        │              │        │  queue + nightly │
│  ┌─────────────┐ │        │              │        │  diff job        │
│  │DeadLetterQue│ │        │              │        └─────┬────────────┘
│  │MisDailyAgg  │ │        │              │              │
│  │SupplierContr│ │        │              │              │
│  │ErasureTombst│ │        │              │              │
│  └─────────────┘ │        │              │              │
└──────────────────┘        │              │              │
                            ▼              ▼              ▼
                      ┌──────────────────────────────────────┐
                      │         SQL Server                   │
                      │  ┌─────────────────────────────────┐ │
                      │  │ booking_events_writer ROLE      │ │
                      │  │  GRANT INSERT, SELECT           │ │
                      │  │  DENY UPDATE, DELETE            │ │
                      │  └─────────────────────────────────┘ │
                      └──────────────────────────────────────┘
                            ▲              ▲              ▲
                            │              │              │
                            └──────────────┼──────────────┘
                                           │
                      ┌────────────────────┴───────────────────┐
                      │                RabbitMQ                │
                      │  Durable queues + _error queues        │
                      │  BackofficeService tails ALL _error    │
                      │  Projection consumers tail *_confirmed │
                      └────────────────────────────────────────┘
                                           ▲
                                           │ Stripe webhooks (BO-06)
                                           │
                                    ┌──────┴──────┐
                                    │   Stripe    │
                                    └─────────────┘
```

**Data flow for a DLQ requeue (BO-09/BO-10):**
1. Message in `queue-name` fails `N+1` times → MassTransit moves to `queue-name_error`
2. `BackofficeService.ErrorQueueConsumer` (bound to `queue-name_error`) deserializes envelope (`UseRawJsonDeserializer`)
3. Row inserted into `backoffice.DeadLetterQueue` with `MessageType`, `OriginalQueue`, `Payload`, `FailureReason` (from `MT-Fault-Message` header)
4. Ops operator clicks "Requeue" → `BackofficeService.DlqController.Requeue(id)` reads row, `IPublishEndpoint.Publish(payload, context => preserveHeaders)`, increments `RequeueCount`
5. If message fails again → new `_error` row → new `DeadLetterQueue` entry with incremented `RequeueCount` linked by `CorrelationId`

**Data flow for a GDPR erasure (COMP-03):**
1. `ops-admin` submits erasure request on `/backoffice/customers/{id}/erase` with typed email confirmation (UI-SPEC)
2. `BackofficeService` publishes `CustomerErasureRequested(CustomerId, EmailHash, Actor)` event
3. `BookingService` consumer NULLs PII columns on all `BookingSagaState` rows where `CustomerId == id`
4. `CrmService` consumer writes `crm.CustomerErasureTombstones` row + NULLs PII on projection tables (`Customers`, `CommunicationLog.EntityId` where EntityType='Customer')
5. `BookingEvents` rows untouched (already immutable per D-49); CRM projection layer applies PII-replace-on-read redaction using tombstone lookup
6. `CustomerErased(CustomerId, ErasedAt)` published for audit

### Recommended Project Structure

```
src/
├── portals/
│   └── backoffice-web/               # D-47 fork from b2b-web
│       ├── next.config.mjs           # basePath /backoffice, per-route CSP (NO Stripe)
│       ├── middleware.ts             # Edge: role gate on /backoffice/admin/*, /finance/*, /cs/*
│       ├── auth.config.ts            # Edge-safe Auth.js config (Pitfall 3)
│       ├── lib/auth.ts               # Node-runtime: Keycloak OIDC, refresh, session
│       ├── lib/api-client.ts         # gatewayFetch helper (Bearer forwarding)
│       ├── lib/rbac.ts               # hasRole(session, 'ops-admin') helpers
│       ├── components/BackofficePortalBadge.tsx
│       ├── components/FourEyesApprovalBadge.tsx
│       ├── components/ui/            # byte-for-byte .jsx copies (Pitfall 17)
│       └── app/
│           ├── (portal)/
│           │   ├── layout.tsx        # header + sidebar + BackofficePortalBadge
│           │   ├── bookings/         # BO-01
│           │   │   ├── page.tsx      # unified list
│           │   │   ├── new/page.tsx  # BO-02 manual booking
│           │   │   └── [id]/page.tsx # detail + events + actions
│           │   ├── customers/        # CRM-01
│           │   │   ├── page.tsx
│           │   │   └── [id]/
│           │   │       ├── page.tsx  # CRM 360 profile
│           │   │       └── erase/page.tsx  # COMP-03
│           │   ├── agencies/         # CRM-02, CRM-03
│           │   ├── dlq/              # BO-09/10
│           │   ├── reconciliation/   # BO-06
│           │   ├── mis/              # BO-08
│           │   ├── markup/           # D-38
│           │   ├── payouts/          # D-41
│           │   ├── contracts/        # BO-07
│           │   └── approvals/        # 4-eyes queue (D-48)
│           └── api/
│               └── auth/[...nextauth]/route.ts
│
└── services/
    ├── BackofficeService/             # Fresh code in empty skeleton
    │   ├── BackofficeService.Api/
    │   │   └── Program.cs             # Auth policies for 4 roles; YARP route registration
    │   ├── BackofficeService.Application/
    │   │   ├── Consumers/ErrorQueueConsumer.cs   # BO-09/10 DLQ consumer
    │   │   ├── HostedServices/MisDailyAggregateJob.cs   # BO-08 D-60 nightly rollup
    │   │   ├── Controllers/DlqController.cs
    │   │   ├── Controllers/BookingsController.cs  # unified list
    │   │   ├── Controllers/FourEyesController.cs
    │   │   └── Exporters/MisExcelExporter.cs      # ClosedXML
    │   └── BackofficeService.Infrastructure/
    │       ├── Migrations/
    │       │   ├── 20260501_CreateDeadLetterQueue.cs
    │       │   ├── 20260502_CreateSupplierContracts.cs
    │       │   ├── 20260503_CreateMisDailyAggregates.cs
    │       │   └── 20260504_CreateCustomerErasureTombstones.cs
    │       └── Data/BackofficeDbContext.cs
    ├── CrmService/                    # Fresh code in empty skeleton
    │   ├── CrmService.Api/
    │   ├── CrmService.Application/
    │   │   ├── Consumers/BookingConfirmedConsumer.cs
    │   │   ├── Consumers/BookingCancelledConsumer.cs
    │   │   ├── Consumers/UserRegisteredConsumer.cs
    │   │   ├── Consumers/WalletTopUpConsumer.cs
    │   │   ├── Consumers/TicketIssuedConsumer.cs
    │   │   ├── Consumers/CustomerCommunicationLoggedConsumer.cs
    │   │   └── Consumers/CustomerErasureRequestedConsumer.cs
    │   └── CrmService.Infrastructure/
    │       ├── Migrations/20260501_CreateCrmProjections.cs
    │       └── Data/CrmDbContext.cs
    ├── BookingService/                # Extend
    │   ├── Migrations/
    │   │   ├── 20260501_AddBookingEventsTable.cs
    │   │   ├── 20260502_AddAppendOnlyRoleGrants.cs   # Raw SQL DENY UPDATE/DELETE
    │   │   ├── 20260503_AddBookingChannelManual.cs
    │   │   └── 20260504_AddCancellationColumns.cs
    │   └── Application/
    │       ├── BookingEventsWriter.cs    # append-only writer
    │       └── ManualBookingCommand.cs   # BO-02
    └── PaymentService/                # Extend
        ├── Migrations/
        │   ├── 20260501_ExtendStripeEventsWithRawPayload.cs
        │   ├── 20260502_AddWalletCreditRequests.cs
        │   ├── 20260503_AddCommissionAccruals.cs
        │   ├── 20260504_AddReconciliationQueue.cs
        │   └── 20260505_AddAgencyCreditLimit.cs
        └── Application/
            ├── Consumers/StripeWebhookConsumer.cs    # extend existing
            ├── HostedServices/ReconciliationJob.cs
            ├── HostedServices/CommissionAccrualJob.cs
            └── HostedServices/MonthlyStatementJob.cs

infra/
└── keycloak/
    └── realms/
        └── tbe-backoffice-realm.json    # FIX: 4 roles per D-46 (ops-admin/ops-cs/ops-finance/ops-read)
```

### Pattern 1: SQL Server Role-Grant Append-Only (D-49)

**What:** Database role denies UPDATE/DELETE on BookingEvents; app connects as that role; rejection happens in the engine before any rows are touched.
**When to use:** BO-04 BookingEvents — any append-only audit table where trigger-based enforcement is considered fragile.

**Example (EF Core migration):**
```csharp
// Source: Phase 3 wallet ledger precedent + MS docs on DENY permissions
// https://learn.microsoft.com/en-us/sql/t-sql/statements/deny-object-permissions-transact-sql
protected override void Up(MigrationBuilder migrationBuilder)
{
    // Step 1: Create the table
    migrationBuilder.CreateTable(
        name: "BookingEvents",
        columns: table => new
        {
            EventId = table.Column<Guid>(nullable: false),
            BookingId = table.Column<Guid>(nullable: false),
            EventType = table.Column<string>(maxLength: 64, nullable: false),
            OccurredAt = table.Column<DateTime>(nullable: false),
            Actor = table.Column<string>(maxLength: 128, nullable: false),
            CorrelationId = table.Column<Guid>(nullable: false),
            Snapshot = table.Column<string>(type: "nvarchar(max)", nullable: false),
        },
        constraints: table => { table.PrimaryKey("PK_BookingEvents", x => x.EventId); });

    // Step 2: Separate migration — create role + grant/deny
    // (in a dedicated migration to avoid mixing DDL with data-plane setup)
    migrationBuilder.Sql(@"
        IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'booking_events_writer')
            CREATE ROLE booking_events_writer;

        GRANT INSERT, SELECT ON dbo.BookingEvents TO booking_events_writer;
        DENY UPDATE, DELETE ON dbo.BookingEvents TO booking_events_writer;

        -- Grant the role to the BookingService SQL login
        IF NOT EXISTS (SELECT 1 FROM sys.database_role_members rm
            JOIN sys.database_principals p ON rm.member_principal_id = p.principal_id
            WHERE p.name = 'tbe_booking_app' AND
                  rm.role_principal_id = (SELECT principal_id FROM sys.database_principals WHERE name='booking_events_writer'))
            ALTER ROLE booking_events_writer ADD MEMBER tbe_booking_app;
    ");
}
```

**Connection string:** BookingService must use a dedicated SQL login (`tbe_booking_app`) that is a member of `booking_events_writer`. The login should NOT have `db_datawriter` on BookingEvents directly — the role is the only grant path. [ASSUMED] Most deployments will use a single per-service SQL login with multiple role memberships; exact login-to-role mapping is Claude's discretion unless overridden by ops.

### Pattern 2: MassTransit `_error` Queue Consumer (D-58, BO-09/10)

**What:** MassTransit auto-creates a `{queue}_error` queue for every receive endpoint. Messages exceeding max retries land there. A consumer tailing these queues captures the envelope and surfaces it to the backoffice.

**When to use:** BO-09/10 — surface all poison messages across the system for ops triage + requeue.

**Example (MassTransit 9):**
```csharp
// Source: MassTransit docs (https://masstransit.io/documentation/concepts/exceptions)
//         MassTransit discussion #5971 on raw JSON access
//         [CITED: masstransit.io + github.com/MassTransit/MassTransit/discussions/5971]
using MassTransit;
using System.Text.Json.Nodes;

public class ErrorQueueConsumer : IConsumer<JsonObject>
{
    private readonly BackofficeDbContext _db;
    private readonly ILogger<ErrorQueueConsumer> _logger;

    public ErrorQueueConsumer(BackofficeDbContext db, ILogger<ErrorQueueConsumer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<JsonObject> context)
    {
        // Fault envelope headers (set by MassTransit when moving to _error)
        var faultMessage = context.Headers.Get<string>("MT-Fault-Message") ?? "(no message)";
        var faultStackTrace = context.Headers.Get<string>("MT-Fault-StackTrace");
        var originalQueue = context.Headers.Get<string>("MT-Fault-InputAddress") ?? "unknown";
        var messageType = context.Headers.Get<string>("MT-MessageType") ?? "unknown";

        var dlqRow = new DeadLetterQueueRow
        {
            Id = Guid.NewGuid(),
            MessageId = context.MessageId ?? Guid.NewGuid(),
            CorrelationId = context.CorrelationId,
            MessageType = messageType,
            OriginalQueue = originalQueue,
            Payload = context.Message.ToJsonString(),  // raw envelope as JSON
            FailureReason = faultMessage.Length > 1000 ? faultMessage[..1000] : faultMessage,
            FailureStackTrace = faultStackTrace,
            FirstFailedAt = DateTime.UtcNow,
            RequeueCount = 0,
            ResolvedAt = null
        };

        _db.DeadLetterQueue.Add(dlqRow);
        await _db.SaveChangesAsync();

        _logger.LogWarning("DLQ captured {MessageType} from {OriginalQueue}: {FailureReason}",
            messageType, originalQueue, faultMessage);
    }
}

// Registration (Program.cs):
x.AddConsumer<ErrorQueueConsumer>();
x.UsingRabbitMq((context, cfg) =>
{
    // Bind to EVERY _error queue we want to tail
    cfg.ReceiveEndpoint("booking-saga_error", e =>
    {
        e.UseRawJsonDeserializer();   // CRITICAL: let us consume ANY envelope shape
        e.ConfigureConsumer<ErrorQueueConsumer>(context);
    });
    cfg.ReceiveEndpoint("payment-webhook-consumer_error", e =>
    {
        e.UseRawJsonDeserializer();
        e.ConfigureConsumer<ErrorQueueConsumer>(context);
    });
    // ... one per upstream queue we want DLQ'd
});
```

### Pattern 3: CoreHostedService Nightly Job (BO-06, BO-08, D-41)

**What:** `BackgroundService` with `IServiceScopeFactory` + cron-aware scheduling + overlap prevention.
**When to use:** MIS daily rollup, nightly reconciliation, monthly commission accrual, monthly statement generation.
**Template:** `src/services/BookingService/BookingService.Infrastructure/Ttl/TtlMonitorHostedService.cs` (existing).

**Example:**
```csharp
// Source: Existing TTL monitor pattern
public class MisDailyAggregateJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<MisJobOptions> _options;
    private readonly ILogger<MisDailyAggregateJob> _logger;
    private static readonly SemaphoreSlim _runLock = new(1, 1);  // overlap prevention

    public MisDailyAggregateJob(IServiceScopeFactory scopeFactory,
        IOptionsMonitor<MisJobOptions> options,
        ILogger<MisDailyAggregateJob> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var opts = _options.CurrentValue;
            var now = DateTime.UtcNow;
            var nextRun = ComputeNextRun(now, opts.CronExpression, opts.TimeZone);
            var delay = nextRun - now;

            try { await Task.Delay(delay, stoppingToken); }
            catch (OperationCanceledException) { return; }

            if (!await _runLock.WaitAsync(0, stoppingToken))
            {
                _logger.LogWarning("MIS aggregate skipped — previous run still in progress");
                continue;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<MisDailyAggregateHandler>();
                await handler.RunAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "MIS aggregate failed — will retry on next schedule");
            }
            finally { _runLock.Release(); }
        }
    }

    private static DateTime ComputeNextRun(DateTime nowUtc, string cronExpression, string timeZone)
    {
        // Use Cronos.CronExpression.Parse for robust DST handling
        var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        var expr = Cronos.CronExpression.Parse(cronExpression);
        return expr.GetNextOccurrence(nowUtc, tz)
            ?? throw new InvalidOperationException("No next occurrence — check cron expression");
    }
}
```

**DST handling:** Use `Cronos` NuGet package (already in-stack if Phase 1 used it — otherwise `dotnet add package Cronos`). Cronos respects IANA time zones and DST transitions correctly.

### Pattern 4: ClosedXML Multi-Sheet Workbook (D-59, BO-08)

**What:** ClosedXML writes `.xlsx` with Summary + Details sheets and totals rows.
**When to use:** MIS report export.

**Example:**
```csharp
// Source: ClosedXML docs (https://github.com/ClosedXML/ClosedXML/wiki)
using ClosedXML.Excel;

public async Task<byte[]> ExportMisReport(MisReportRequest req, CancellationToken ct)
{
    using var wb = new XLWorkbook();

    // === Summary sheet ===
    var summary = wb.Worksheets.Add("Summary");
    summary.Cell("A1").Value = "MIS Report";
    summary.Cell("A1").Style.Font.Bold = true;
    summary.Cell("A1").Style.Font.FontSize = 16;
    summary.Cell("A3").Value = $"Period: {req.From:yyyy-MM-dd} to {req.To:yyyy-MM-dd}";
    summary.Cell("A5").Value = "Total Bookings";
    summary.Cell("B5").Value = req.TotalBookings;
    summary.Cell("A6").Value = "Total Revenue (£)";
    summary.Cell("B6").Value = req.TotalRevenue;
    summary.Cell("B6").Style.NumberFormat.Format = "#,##0.00";
    summary.Cell("A7").Value = "Total Commission (£)";
    summary.Cell("B7").Value = req.TotalCommission;
    summary.Cell("B7").Style.NumberFormat.Format = "#,##0.00";
    summary.Columns().AdjustToContents();

    // === Details sheet ===
    var details = wb.Worksheets.Add("Details");
    details.Cell("A1").Value = "Date";
    details.Cell("B1").Value = "Product";
    details.Cell("C1").Value = "Channel";
    details.Cell("D1").Value = "Bookings";
    details.Cell("E1").Value = "Revenue (£)";
    details.Cell("F1").Value = "Commission (£)";
    details.Range("A1:F1").Style.Font.Bold = true;
    details.Range("A1:F1").Style.Fill.BackgroundColor = XLColor.FromHtml("#F3F4F6");

    int row = 2;
    foreach (var item in req.Rows)
    {
        details.Cell(row, 1).Value = item.Date;
        details.Cell(row, 1).Style.NumberFormat.Format = "yyyy-mm-dd";
        details.Cell(row, 2).Value = item.Product;
        details.Cell(row, 3).Value = item.Channel;
        details.Cell(row, 4).Value = item.BookingsCount;
        details.Cell(row, 5).Value = item.Revenue;
        details.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
        details.Cell(row, 6).Value = item.Commission;
        details.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
        row++;
    }

    // Totals row at end of Details
    details.Cell(row, 1).Value = "Totals";
    details.Cell(row, 1).Style.Font.Bold = true;
    details.Cell(row, 4).FormulaA1 = $"=SUM(D2:D{row - 1})";
    details.Cell(row, 5).FormulaA1 = $"=SUM(E2:E{row - 1})";
    details.Cell(row, 6).FormulaA1 = $"=SUM(F2:F{row - 1})";
    details.Range($"D{row}:F{row}").Style.Font.Bold = true;
    details.Columns().AdjustToContents();

    // Stream to memory
    using var ms = new MemoryStream();
    wb.SaveAs(ms);
    return ms.ToArray();
}
```

**Controller delivery:**
```csharp
[HttpGet("mis/export.xlsx")]
[Authorize(Policy = "BackofficeFinancePolicy")]
public async Task<IActionResult> ExportXlsx([FromQuery] MisQuery q, CancellationToken ct)
{
    var bytes = await _exporter.ExportMisReport(q, ct);
    return File(bytes,
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        $"mis-{q.From:yyyyMMdd}-{q.To:yyyyMMdd}.xlsx");
}
```

### Pattern 5: Event-Sourced CRM Projection with MessageId Dedup (D-51)

**What:** MassTransit consumer subscribes to published events; projection writes are idempotent via the same `MessageId` dedup table Phase 3 established.
**When to use:** All CRM projections (Customers, Agencies, BookingProjections, CommunicationLog, UpcomingTrips).

**Example:**
```csharp
// Source: Phase 3 MassTransit outbox precedent (Plan 03-01)
public class BookingConfirmedConsumer : IConsumer<BookingConfirmed>
{
    private readonly CrmDbContext _db;
    public BookingConfirmedConsumer(CrmDbContext db) { _db = db; }

    public async Task Consume(ConsumeContext<BookingConfirmed> context)
    {
        var messageId = context.MessageId ?? Guid.NewGuid();

        // Idempotency: check dedup table first
        var already = await _db.InboxMessages
            .AnyAsync(m => m.MessageId == messageId, context.CancellationToken);
        if (already) return;

        var msg = context.Message;
        var projection = await _db.BookingProjections
            .FirstOrDefaultAsync(b => b.BookingId == msg.BookingId, context.CancellationToken);

        if (projection == null)
        {
            projection = new BookingProjection
            {
                BookingId = msg.BookingId,
                CustomerId = msg.CustomerId,
                AgencyId = msg.AgencyId,
                Channel = msg.Channel,
                TotalAmount = msg.TotalAmount,
                Currency = msg.Currency,
                Status = "Confirmed",
                ConfirmedAt = msg.ConfirmedAt,
            };
            _db.BookingProjections.Add(projection);
        }
        else
        {
            projection.Status = "Confirmed";
            projection.ConfirmedAt = msg.ConfirmedAt;
        }

        // Upsert Customer
        var customer = await _db.Customers
            .FirstOrDefaultAsync(c => c.Id == msg.CustomerId, context.CancellationToken);
        if (customer == null)
        {
            customer = new Customer
            {
                Id = msg.CustomerId,
                Name = msg.CustomerName,
                Email = msg.CustomerEmail,
                Phone = msg.CustomerPhone,
                FirstSeenAt = msg.ConfirmedAt,
            };
            _db.Customers.Add(customer);
        }
        customer.LastSeenAt = msg.ConfirmedAt;

        _db.InboxMessages.Add(new InboxMessage { MessageId = messageId, ConsumedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync(context.CancellationToken);
    }
}
```

**Rebuild-from-scratch:** CrmService exposes an ops-only `/api/crm/projections/rebuild` endpoint that truncates projection tables, deletes the InboxMessages dedup rows, and re-subscribes to the durable queues from the beginning. [ASSUMED] RabbitMQ durable queues retain messages indefinitely only if no consumer ACKs them — for actual rebuild you need to republish from a stored BookingEvents archive. D-51 explicitly defers the full event-store engine, so rebuild is best-effort and requires ops coordination. Flag as Open Question.

### Pattern 6: 4-Eyes Approval Workflow with DB Status Column (D-48)

**What:** Lightweight state machine via status column rather than full saga.
**When to use:** BackofficeWalletCreditRequest + BookingCancellationRequest (D-48's 2 actions).

**Example schema:**
```csharp
public class WalletCreditRequest
{
    public Guid Id { get; set; }
    public Guid AgencyId { get; set; }
    public decimal Amount { get; set; }
    public string ReasonCode { get; set; }  // enum constraint: RefundedBooking|GoodwillCredit|DisputeResolution|SupplierRefundPassthrough
    public Guid? LinkedBookingId { get; set; }
    public string Notes { get; set; }
    public string Status { get; set; }  // "PendingApproval" | "Approved" | "Denied" | "Expired"
    public string RequestedBy { get; set; }  // ops-finance username
    public DateTime RequestedAt { get; set; }
    public string ApprovedBy { get; set; }  // ops-admin username (nullable)
    public DateTime? ApprovedAt { get; set; }
    public DateTime ExpiresAt { get; set; }  // RequestedAt + 72h per UI-SPEC
    public string DenyReason { get; set; }  // nullable
}
```

**Self-approval guard (critical):**
```csharp
[HttpPost("wallet-credit-requests/{id}/approve")]
[Authorize(Policy = "BackofficeAdminPolicy")]
public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveReq req, CancellationToken ct)
{
    var actor = User.FindFirstValue("preferred_username")
        ?? throw new InvalidOperationException("No subject");

    var request = await _db.WalletCreditRequests
        .FirstOrDefaultAsync(r => r.Id == id, ct);

    if (request == null) return NotFound();
    if (request.Status != "PendingApproval") return Conflict(Problem("Already decided"));
    if (request.ExpiresAt < DateTime.UtcNow) return Conflict(Problem("Expired"));

    // 4-eyes: actor cannot approve their own request
    if (string.Equals(request.RequestedBy, actor, StringComparison.OrdinalIgnoreCase))
        return StatusCode(403, Problem("Self-approval forbidden"));

    request.Status = "Approved";
    request.ApprovedBy = actor;
    request.ApprovedAt = DateTime.UtcNow;
    await _db.SaveChangesAsync(ct);

    // Publish for PaymentService to materialize the WalletTransactions row atomically
    await _bus.Publish(new WalletCreditApproved(request.Id, request.AgencyId, request.Amount,
        request.ReasonCode, request.LinkedBookingId, actor), ct);

    return NoContent();
}
```

### Anti-Patterns to Avoid

- **EF Core `TemporalTableBuilder` instead of role-grant DENY:** Temporal tables make old rows queryable but DO allow UPDATE. They satisfy a history-preservation goal, not a strict-append-only goal. D-49 requires true append-only.
- **Writing BookingEvents from the same DbContext as the mutable tables:** Change-tracking across a role-denied entity will throw at SaveChanges when any of the mutable entity updates happen in the same transaction. Use a **second `DbContext` + second connection + second login** for BookingEvents writes, OR raw ADO.NET for the insert. [CITED: SQL Server DENY docs]
- **Re-poisoning on DLQ requeue:** Requeuing raw JSON without headers drops the envelope's tracing context. Use `IPublishEndpoint.Publish` with a `PublishContextCallback` that restores `MessageId`, `CorrelationId`, and custom headers.
- **Trigger-based append-only:** Triggers run per-row in a cursor-like fashion and degrade INSERT throughput. Role grants are declarative and engine-optimized.
- **Status column without expiry enforcement:** A pending approval that never expires becomes stale state. UI-SPEC mandates 72h expiry — the PendingApproval status MUST have a background job that transitions to Expired, OR a CHECK constraint enforcing ExpiresAt > GETUTCDATE() at read time.
- **Calling Stripe API from CoreHostedService without circuit breaker:** D-55 reconciliation diff is on our stored StripeEvents table — do NOT re-call Stripe API from the nightly job. Stripe rate limits are strict; a large backlog would throttle.
- **CRM projection consumer writing to BookingService DB:** D-51 mandates service boundary — CrmService reads MassTransit events only, never cross-DB.
- **Cascade delete on GDPR erasure:** D-57 says NULL PII; never DELETE rows from BookingSagaState/BookingEvents. Financial audit must survive erasure.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Multi-sheet Excel export | Custom OpenXML writer | ClosedXML 0.105.0 | OpenXML SDK is ceremony-heavy; ClosedXML wraps it |
| PDF document composition | `iText7`, DIY HTML→PDF | QuestPDF 2026.2.4 (already in-stack) | Already licensed + Phase 5 AgencyInvoiceDocument precedent |
| MassTransit `_error` queue auto-creation | Custom DLX (dead-letter exchange) config | MassTransit creates `_error` queue per receive endpoint automatically | Default MassTransit behavior — do not override unless necessary |
| Append-only enforcement | DB triggers + audit columns | SQL Server role grants (`DENY UPDATE, DELETE`) | Engine-level rejection; no trigger maintenance |
| 4-eyes workflow | Full MassTransit saga per approval type | DB status column + explicit controller transitions + self-approval guard | Only 2 action classes in D-48 — saga overhead unjustified |
| Keycloak RBAC in code | Custom role checker | Keycloak realm roles + ASP.NET Core `[Authorize(Policy=...)]` + per-role policy registration | JWT claim is single source of truth (Pitfall 28) |
| Cron scheduling | DIY `Task.Delay` loops | `Cronos` NuGet for cron expression parsing + IANA time zone DST | Cronos handles DST fall-back correctly (ambiguous local times) |
| Stripe signature verification | Manual HMAC check | `Stripe.EventUtility.ConstructEvent` (already used Phase 5) | 300s tolerance, timing-safe comparison |
| Markdown sanitization | Regex + ad-hoc strip | `rehype-sanitize` (server-side) | XSS hardening — CRM-04 body is user-supplied |
| SHA-256 email hash for tombstones | `MD5` / custom hash | `System.Security.Cryptography.SHA256` | Cryptographic hash for dedup; MD5 is collision-broken |
| `application/problem+json` | Custom error envelope | Plan 05-03 precedent shape | Consistency across services |
| Auth.js edge-split | Custom cookie session | Plan 04-00 / 05-00 pattern (Pitfall 3) | Edge runtime can't use Node-only crypto |
| Realm fork | Hand-editing JSON | Fork `tbe-b2b-realm.json`, rename roles per D-46 | Same structure, just different role set |

**Key insight:** Phase 6 is almost entirely integration work, not new-library work. Every pattern has a Phase 1-5 precedent — the main research value is confirming which precedent applies and flagging the 2-3 places where the precedent doesn't quite fit (DLQ consumer, GDPR erasure orchestration, BookingEvents role-grant).

---

## Runtime State Inventory

**Trigger:** Phase 6 involves extending existing services (BookingService, PaymentService) and adding new service code on top of existing skeletons.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| **Stored data** | `infra/keycloak/realms/tbe-backoffice-realm.json` exists (from Phase 1 scaffold) but has WRONG role names (`backoffice-admin`, `backoffice-operator`, `finance`) vs D-46 required names (`ops-admin`, `ops-cs`, `ops-finance`, `ops-read`). This is a realm JSON file committed to git + will be imported into the running Keycloak — both need the rename. | (1) Rewrite realm JSON with correct 4 roles per D-46. (2) If Keycloak has already imported the old JSON (check `docker compose logs keycloak` or Keycloak admin UI), DELETE the old roles via admin API or re-import the realm. (3) Create test users in new roles for UAT. |
| **Stored data** | `payment.StripeWebhookEvents` table exists (Phase 5) with only 3 columns: `EventId PK`, `EventType`, `ReceivedAtUtc`. D-55 requires extension: add `RawPayload nvarchar(max)` + `Processed bit DEFAULT 0`. | EF Core migration: `ALTER TABLE payment.StripeWebhookEvents ADD RawPayload nvarchar(max) NULL, Processed bit NOT NULL DEFAULT 0`. Also update `StripeWebhookController` to persist the full event JSON (`stripeEvent.ToJson()` from Stripe SDK). Rename table to `payment.StripeEvents` per D-55 exact wording, OR document the name mismatch in plan. [ASSUMED: recommend keeping `StripeWebhookEvents` name and updating D-55 to match — renaming a live webhook table requires ingress migration planning] |
| **Stored data** | `BookingChannel` enum in BookingSagaState currently has `B2C=0`, `B2B=1`. D-56 requires adding `Manual=2`. Enum is persisted as an integer column — safe to extend. | Add `Manual = 2` to `BookingChannel` enum in `TBE.Common` or the saga state module; EF Core migration not strictly required unless a CHECK constraint exists (verify). No data migration needed. |
| **Stored data** | `payment.WalletTransactions.Kind` enum currently has `TopUp`, `Reserve`, `Release`, `Capture` (Phase 3/5). D-53 adds `ManualCredit`; D-54 adds `CommissionPayout`. Also need `ApprovedBy nvarchar(128) NULL` + `ApprovalNotes nvarchar(500) NULL` columns for D-53 audit. | EF Core migration: extend enum (if string-persisted) OR add new values (if int-persisted); add two new columns. No data migration for existing rows (NULL ApprovedBy is correct for legacy rows). |
| **Live service config** | RabbitMQ queues created by MassTransit at service startup. Phase 6 adds new consumers in BackofficeService and CrmService — new queues will auto-create. `_error` queues also auto-create per endpoint. No manual config. | None — MassTransit handles queue/exchange creation. However: on first-boot of BackofficeService, verify `_error` queue names match the binding list in the DLQ consumer registration. |
| **Live service config** | YARP gateway needs new routes (`/api/backoffice/*`, `/api/crm/*`) + new JWT scheme `tbe-backoffice`. Existing gateway config is in `src/Gateway/Gateway/appsettings.json` (Phase 1 scaffold). | Add route + JWT scheme config; add backoffice authority URL to JwtBearer schemes. |
| **OS-registered state** | None — all services run in Docker Compose or as .NET hosts. No Windows Task Scheduler, launchd, systemd tasks. Nightly jobs are in-process BackgroundServices. | None. |
| **Secrets and env vars** | New env vars needed: `BACKOFFICE_REALM_AUTHORITY`, `BACKOFFICE_CLIENT_ID`, `BACKOFFICE_CLIENT_SECRET` (portal), `BOOKING_EVENTS_WRITER_CONNECTION_STRING` (BookingService — distinct SQL login for append-only role), `STRIPE_RECONCILIATION_WEBHOOK_SECRET` (if adding a separate endpoint, optional). | Add to `docker-compose.yml` + `.env.example`. Document in plan. |
| **Secrets and env vars** | Existing Stripe webhook secret covers BO-06 extension — no new Stripe secret needed. | None. |
| **Build artifacts / installed packages** | BackofficeService + CrmService skeletons compile currently but have no code — need to reference new packages (ClosedXML for BackofficeService, QuestPDF for PaymentService). | `dotnet add package` commands in plan. No build artifact cleanup needed (skeletons have no egg-info-equivalent). |
| **Build artifacts / installed packages** | `src/portals/backoffice-web/` does NOT yet exist — needs fresh fork from `b2b-web`. No stale build artifacts. | Fork with `cp -r` then delete Stripe deps from package.json. |
| **Live service config** | `CustomerErasureRequested` event fan-out touches 3 services (BookingService, CrmService, PaymentService?). PaymentService likely does NOT need to participate (wallet transactions are not PII). Verify in plan. | None — Claude's discretion per D-57 as to PaymentService participation. [ASSUMED: PaymentService NOT included in erasure fan-out — wallet ledger is financial, not PII] |

**Nothing found in category:**
- **OS-registered state:** None — verified by inspecting `docker-compose.yml` and looking for BackgroundService vs external scheduler references. All jobs are in-process.
- **Build artifacts for backoffice-web:** None — the portal fork is greenfield.

---

## Common Pitfalls

### Pitfall 1: EF Core ChangeTracker Trips DENY on BookingEvents (P1)

**What goes wrong:** BookingService has a single `DbContext` covering saga state + BookingEvents. When the saga handler loads a BookingSagaState entity and modifies it (e.g., sets `Status = Confirmed`), `ChangeTracker` may detect stale state on BookingEvents as well if the same context loaded events earlier. On `SaveChanges`, EF Core issues an UPDATE that the role denies → `SqlException: UPDATE permission denied`.

**Why it happens:** `ChangeTracker` doesn't know about role-level permissions. It treats all tracked entities as UPDATE candidates.

**How to avoid:**
- Option A (recommended): **Dedicated `BookingEventsDbContext`** with its own connection string using the `tbe_booking_events_writer` SQL login that has ONLY the role grant. This context tracks ONLY `BookingEvents` entity. Writes happen via a `BookingEventsWriter` service, not the saga handler's context.
- Option B: In the main `BookingDbContext`, mark BookingEvents as `Ignore()` or use `[DatabaseGenerated(DatabaseGeneratedOption.None)]` + explicit `Add`-only entry state + `SaveChanges`. Fragile.
- Option C: Use raw ADO.NET `SqlCommand` with `INSERT` for BookingEvents writes. Bypasses EF change-tracker entirely.

**Warning signs:** Integration tests fail with "UPDATE permission was denied" after migration runs. Plan acceptance test must include an **attempt** to UPDATE/DELETE a BookingEvents row through the app to prove rejection.

### Pitfall 2: Re-Poisoning on DLQ Requeue (BO-10)

**What goes wrong:** Operator requeues a dead-letter message. Root cause isn't fixed. Message fails again. New DLQ row created. Operator requeues again. Infinite cycle.

**Why it happens:** Requeue UX makes it cheap to "try again" without investigation.

**How to avoid:**
- Enforce `RequeueCount` cap (e.g., max 3) in the Requeue endpoint — after 3 requeues the only action available is "Resolve manually" with reason text.
- UI-SPEC mandates chevron-with-count badge (`Retry ×3` in amber) — make this visually loud.
- Log requeue reason text as mandatory field; blank reason → 400.

**Warning signs:** DLQ list shows single-digit `RequeueCount` across many rows in first month of prod (normal); triple-digit requeue on any single row (broken consumer — escalate).

### Pitfall 3: Timezone / DST on Month-End Commission Cut (D-54)

**What goes wrong:** UK DST fall-back on last Sunday of October creates an ambiguous 01:00-02:00 window. A job scheduled for 01:30 local time runs twice. Monthly statement gets two runs, two PDFs generated, two WalletTransactions credited.

**Why it happens:** Naive `TimeZoneInfo.ConvertTimeFromUtc` doesn't handle DST transitions correctly.

**How to avoid:**
- Use `Cronos` NuGet — it handles DST fall-back correctly (returns only one of the ambiguous times).
- Schedule monthly cut at 03:00 UTC (post-DST-window safe) rather than local 00:00.
- Write an idempotency guard: `CommissionAccruals.MonthlyBatchLog (Year, Month, RunAt, RunBy)` with `PRIMARY KEY (Year, Month)` — second run of the month throws duplicate-key.

**Warning signs:** Test on 2026-10-26 (last Sunday of Oct, UK DST end) + 2027-03-29 (last Sunday of Mar, UK DST start) — verify only one run happens.

### Pitfall 4: Self-Approval in 4-Eyes (D-48)

**What goes wrong:** Operator creates a wallet credit request, then logs into their `ops-admin` alt account (maybe a test account) and approves their own request. 4-eyes defeated.

**Why it happens:** The approve endpoint gates on the `ops-admin` role but doesn't check `request.RequestedBy != actor`.

**How to avoid:**
- Explicit guard in controller (see Pattern 6 code example).
- `RequestedBy` is username (preferred_username JWT claim), NOT user ID — same username cannot approve their own request regardless of role.
- Audit log records BOTH `RequestedBy` and `ApprovedBy`; backoffice UI renders both in the approval history tab.

**Warning signs:** Integration test: same test-user with both `ops-finance` + `ops-admin` roles tries to approve own request → expect 403.

### Pitfall 5: Envelope Format Drift on DLQ (BO-09)

**What goes wrong:** MassTransit version upgrade changes the `Fault<T>` envelope shape. DLQ rows stored with old shape fail to deserialize on requeue. Operators stuck with unreplayable dead-letters.

**Why it happens:** Storing raw envelope JSON tightly couples the DLQ to MassTransit's internal format.

**How to avoid:**
- Store `MessageType` as a separate typed column, and store the **inner message payload only** (not the full Fault envelope) in `Payload`. On requeue, reconstruct the envelope at publish time (IPublishEndpoint handles this automatically given the typed message).
- Version the DLQ row schema: `EnvelopeVersion int DEFAULT 1`; on MassTransit upgrade, add migration code that converts old rows.

**Warning signs:** Test: MassTransit major-version upgrade + DLQ replay of rows stored under old version → expect clean replay.

### Pitfall 6: GDPR Tombstone Race (COMP-03)

**What goes wrong:** Erasure fires on customer C. BookingService NULLs PII rows. At the same instant, a BookingConfirmed event for customer C arrives at the CRM projection. Projection writes a fresh `Customer` row with the pre-erasure PII (from the event payload). Customer's PII is restored.

**Why it happens:** Event payloads carry PII; erasure only wipes DB rows, not in-flight events.

**How to avoid:**
- CRM consumer checks `CustomerErasureTombstones.EmailHash` on every incoming event with PII. If hash matches, write projection WITHOUT PII fields (or skip entirely for UserRegistered).
- Publish `CustomerErased` event ordered AFTER all pending writes for that customer → CRM consumer handles it as a "retroactive redact" pass.

**Warning signs:** Concurrency test: publish BookingConfirmed for customer X and erase customer X within 100ms → verify final state has no PII.

### Pitfall 7: Overlapping Nightly Job Runs (BO-06, BO-08, D-41)

**What goes wrong:** Nightly reconciliation takes 90 minutes due to large backlog. At 01:00 the scheduled run kicks off; at 02:00 the CoreHostedService timer ticks again and starts a second run. Two runs write to PaymentReconciliationQueue concurrently — duplicate rows.

**Why it happens:** `BackgroundService` with naive `Task.Delay` doesn't guard against overlap.

**How to avoid:**
- `SemaphoreSlim(1, 1)` with `WaitAsync(0)` — if held, skip this run (see Pattern 3).
- Idempotent upserts: `PaymentReconciliationQueue` uses `MERGE` on `(StripeEventId, WalletTxId)` composite key.

### Pitfall 8: Per-Portal Cookie Scope Leak (Pitfall 19 carried forward)

**What goes wrong:** Backoffice operator also has a B2B agent account. Browser tab A on `/b2b` authenticates. Tab B on `/backoffice` reads the `__Secure-tbe-b2b.session-token` cookie and treats them as logged in. OR vice versa.

**Why it happens:** Cookies with path `/` on the same domain are shared across subpaths.

**How to avoid:**
- Separate cookie names per portal: `__Secure-tbe-b2c.session-token`, `__Secure-tbe-b2b.session-token`, `__Secure-tbe-backoffice.session-token`. Auth.js config sets `cookies.sessionToken.name` per portal.
- Separate realms (Keycloak tbe-backoffice) — even if cookie name collision happens, JWT fails issuer check.
- Check `iss` claim on JWT — must match the configured realm authority for this portal.

### Pitfall 9: Starter-Kit .jsx Rewrite (Pitfall 17 carried forward)

**What goes wrong:** Developer in Phase 6 converts a `.jsx` component from the starterKit to `.tsx` for type safety. Hydration breaks because `.jsx` is byte-for-byte synced across 3 portals, and the TypeScript version diverges.

**How to avoid:**
- Strict rule: `components/ui/*.jsx` files in `backoffice-web/` are byte-for-byte copies of `b2b-web/`.
- Any TS friction resolved via `types/ui.d.ts` ambient shim (Phase 4 D-17 pattern).
- Add grep check to CI: `diff -q src/portals/backoffice-web/components/ui/ src/portals/b2b-web/components/ui/` → exit 0 expected.

### Pitfall 10: JWT Claim as Single Source of Truth (Pitfall 28 carried forward)

**What goes wrong:** Portal UI hides a button from `ops-read` role. But the API endpoint doesn't check the role — relies on UI hiding. Attacker hits the endpoint directly.

**How to avoid:**
- Every mutating API endpoint gates on `[Authorize(Policy = "BackofficeXxxPolicy")]` — policy checks the JWT role claim.
- UI role checks are UX-only, never security.
- Integration tests hit mutating endpoints directly with `ops-read` token → expect 403.

---

## Code Examples

### CustomerErasureRequested fan-out (COMP-03)

```csharp
// Source: Claude design based on D-57 + Pitfall 6 mitigation
// BackofficeService — initiator
[HttpPost("customers/{id}/erase")]
[Authorize(Policy = "BackofficeAdminPolicy")]
public async Task<IActionResult> EraseCustomer(Guid id, [FromBody] EraseReq req, CancellationToken ct)
{
    var actor = User.FindFirstValue("preferred_username")!;
    var customer = await _crmClient.GetCustomer(id, ct);
    if (customer == null) return NotFound();

    // Typed email confirmation (UI enforces; server double-checks)
    if (!string.Equals(req.ConfirmEmail, customer.Email, StringComparison.OrdinalIgnoreCase))
        return BadRequest(Problem("Email confirmation mismatch"));

    var emailHash = Convert.ToHexString(
        System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(customer.Email.ToLowerInvariant())));

    await _bus.Publish(new CustomerErasureRequested(
        CustomerId: id,
        EmailHash: emailHash,
        ErasedBy: actor,
        ErasedAt: DateTime.UtcNow), ct);

    return Accepted();
}

// BookingService — consumer
public class CustomerErasureRequestedConsumer : IConsumer<CustomerErasureRequested>
{
    private readonly BookingDbContext _db;
    public async Task Consume(ConsumeContext<CustomerErasureRequested> context)
    {
        var msg = context.Message;
        var sagas = await _db.BookingSagaStates
            .Where(s => s.CustomerId == msg.CustomerId)
            .ToListAsync(context.CancellationToken);
        foreach (var s in sagas)
        {
            s.CustomerName = null;
            s.CustomerEmail = null;
            s.CustomerPhone = null;
            s.PassportNumber = null;
            s.DateOfBirth = null;
            // BookingEvents: untouched per D-57
        }
        await _db.SaveChangesAsync(context.CancellationToken);
    }
}

// CrmService — consumer (PII-replace + tombstone)
public class CustomerErasureRequestedConsumer : IConsumer<CustomerErasureRequested>
{
    private readonly CrmDbContext _db;
    public async Task Consume(ConsumeContext<CustomerErasureRequested> context)
    {
        var msg = context.Message;
        _db.CustomerErasureTombstones.Add(new CustomerErasureTombstone
        {
            Id = Guid.NewGuid(),
            OriginalCustomerId = msg.CustomerId,
            EmailHash = msg.EmailHash,
            ErasedAt = msg.ErasedAt,
            ErasedBy = msg.ErasedBy
        });
        var customer = await _db.Customers.FindAsync(new object?[] { msg.CustomerId },
            context.CancellationToken);
        if (customer != null)
        {
            customer.Name = null;
            customer.Email = null;
            customer.Phone = null;
            customer.IsErased = true;
        }
        await _db.SaveChangesAsync(context.CancellationToken);
        await context.Publish(new CustomerErased(msg.CustomerId, msg.ErasedAt, msg.ErasedBy));
    }
}
```

### Per-route CSP for backoffice-web (D-47)

```javascript
// Source: fork of b2b-web/next.config.mjs with Stripe stripped
// src/portals/backoffice-web/next.config.mjs

const standardSecurityHeaders = [
  { key: 'X-Content-Type-Options', value: 'nosniff' },
  { key: 'Referrer-Policy', value: 'strict-origin-when-cross-origin' },
  { key: 'X-Frame-Options', value: 'DENY' },
];

// Backoffice CSP — Stripe explicitly absent.
// Backoffice never takes card payments (PCI SAQ-A preserved; v1 wallet-only refunds).
const backofficeCsp = [
  "default-src 'self'",
  "script-src 'self' 'unsafe-inline'",
  "style-src 'self' 'unsafe-inline'",
  "img-src 'self' data: https:",
  "font-src 'self' data:",
  "connect-src 'self'",
  "form-action 'self'",
].join('; ');

const nextConfig = {
  basePath: '/backoffice',
  output: 'standalone',
  async headers() {
    return [
      {
        source: '/:path*',
        headers: [
          { key: 'Content-Security-Policy', value: backofficeCsp },
          ...standardSecurityHeaders,
        ],
      },
    ];
  },
};

export default nextConfig;
```

### Auth.js v5 edge-safe + Node config (D-47)

```typescript
// src/portals/backoffice-web/auth.config.ts
// Edge-runtime SAFE — no Node crypto, no DB
import type { NextAuthConfig } from 'next-auth';

export const authConfig = {
  pages: { signIn: '/signin' },
  providers: [],  // populated in lib/auth.ts for Node runtime
  callbacks: {
    authorized({ auth, request: { nextUrl } }) {
      const isLoggedIn = !!auth?.user;
      const roles = (auth?.user as any)?.roles as string[] | undefined ?? [];
      const path = nextUrl.pathname;

      if (path.startsWith('/backoffice/admin/')) return roles.includes('ops-admin');
      if (path.startsWith('/backoffice/finance/')) return roles.some(r => r === 'ops-finance' || r === 'ops-admin');
      if (path.startsWith('/backoffice/cs/')) return roles.some(r => r === 'ops-cs' || r === 'ops-admin');
      // Read paths: any authenticated role
      return isLoggedIn;
    },
  },
  cookies: {
    sessionToken: {
      name: '__Secure-tbe-backoffice.session-token',
      options: {
        httpOnly: true,
        sameSite: 'lax',
        path: '/backoffice',
        secure: true,
      },
    },
  },
} satisfies NextAuthConfig;
```

### `payment.StripeEvents` extension migration (D-55)

```csharp
// Source: Claude design for D-55
// Migration: 20260501_ExtendStripeEventsForReconciliation
protected override void Up(MigrationBuilder mb)
{
    mb.AddColumn<string>(
        name: "RawPayload",
        schema: "payment",
        table: "StripeWebhookEvents",  // kept existing name; alias in code
        type: "nvarchar(max)",
        nullable: true);

    mb.AddColumn<bool>(
        name: "Processed",
        schema: "payment",
        table: "StripeWebhookEvents",
        type: "bit",
        nullable: false,
        defaultValue: false);

    mb.CreateIndex(
        name: "IX_StripeEvents_ProcessedAt",
        schema: "payment",
        table: "StripeWebhookEvents",
        columns: new[] { "Processed", "ReceivedAtUtc" });
}
```

### `Agencies.CreditLimit` + reserve enforcement (CRM-02, D-61)

```sql
-- Migration SQL (raw in EF Core migration)
ALTER TABLE payment.Agencies
    ADD CreditLimit decimal(18,4) NOT NULL DEFAULT 0;
```

```csharp
// PaymentService — WalletReserveHandler (extend existing)
public async Task<ReserveResult> ReserveAsync(WalletReserveCommand cmd, CancellationToken ct)
{
    await using var tx = await _db.Database.BeginTransactionAsync(
        System.Data.IsolationLevel.Serializable, ct);

    // Phase 3 D-15 lock pattern extended with credit-limit check
    var wallet = await _db.Wallets
        .FromSqlInterpolated(
            $"SELECT * FROM payment.Wallets WITH (UPDLOCK, ROWLOCK, HOLDLOCK) WHERE AgencyId = {cmd.AgencyId}")
        .FirstOrDefaultAsync(ct);

    if (wallet == null) return ReserveResult.NoWallet();

    var agency = await _db.Agencies.FindAsync(new object?[] { cmd.AgencyId }, ct);
    var creditLimit = agency?.CreditLimit ?? 0m;

    // D-61: strengthened check
    if (wallet.AvailableBalance + creditLimit < cmd.Amount)
    {
        await tx.RollbackAsync(ct);
        return ReserveResult.InsufficientFunds(
            available: wallet.AvailableBalance,
            creditLimit: creditLimit,
            requested: cmd.Amount);
    }

    wallet.AvailableBalance -= cmd.Amount;
    wallet.ReservedBalance += cmd.Amount;

    _db.WalletTransactions.Add(new WalletTransaction
    {
        Id = Guid.NewGuid(),
        WalletId = wallet.Id,
        AgencyId = cmd.AgencyId,
        Kind = "Reserve",
        Amount = cmd.Amount,
        OccurredAt = DateTime.UtcNow,
        ReferenceId = cmd.BookingId,
    });

    await _db.SaveChangesAsync(ct);
    await tx.CommitAsync(ct);
    return ReserveResult.Success(wallet.AvailableBalance, creditLimit);
}

// Controller returns 402 with problem+json on insufficient
if (result.IsInsufficient)
    return StatusCode(402, new ProblemDetails
    {
        Type = "tbe://errors/wallet-reserve-insufficient",
        Title = "Wallet reserve failed — credit limit exceeded",
        Status = 402,
        Detail = $"Requested £{result.Requested}, available £{result.Available}, credit limit £{result.CreditLimit}",
        Extensions = {
            ["allowedRange"] = new { floor = -result.CreditLimit, ceiling = decimal.MaxValue },
            ["availableBalance"] = result.Available,
            ["creditLimit"] = result.CreditLimit
        }
    });
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Triggers for append-only audit | Role grants (DENY UPDATE/DELETE) | SQL Server 2008+, became standard ~2015 | Declarative, engine-optimized, no trigger maintenance |
| Full event store (EventStoreDB / Marten) | MassTransit outbox + projection tables | Microservices era ~2020 | Lighter-weight, no new infrastructure, leverages existing MT |
| Hard-delete for GDPR | Tombstone + PII-NULL + log compaction | GDPR era 2018+ | Preserves financial audit while satisfying RTBF |
| JWT claim copy-in-DB for roles | Claim-as-SoT (Pitfall 28) | OAuth2 maturity ~2019 | No sync drift; fewer DB writes |
| Manual DLX (dead-letter exchange) config | MassTransit auto-creates `_error` per endpoint | MassTransit 6+ | Zero-config poison handling |
| Hand-built cron with `Task.Delay` | `Cronos` NuGet + BackgroundService | .NET Core 3+ | DST-safe, cron expressions parse correctly |
| OpenXML SDK direct | ClosedXML wrapper | ClosedXML 0.95+ (2019) | 10x less code, same output |
| Per-page CSP via `<meta>` tags | Per-route CSP via `next.config.mjs headers()` | Next.js 13+ | Enforceable at edge, no inline tag injection |

**Deprecated/outdated:**
- `TempTable` / `tempdb` for session storage: use Redis or in-proc.
- `[Authorize(Roles = "...")]` with comma-separated roles: use `[Authorize(Policy = "...")]` for clarity + testability.
- `EventUtility.ConstructEvent` tolerance < 300s: Stripe recommends 300s minimum.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| SQL Server | All services (BookingEvents, StripeEvents, DLQ, etc.) | ✓ | Matches Phase 1 (likely 2022+) | — |
| RabbitMQ | MassTransit transport + `_error` queues | ✓ | Phase 1 locked | — |
| Redis | Distributed locks (if needed for job overlap across multi-instance) | Unknown | TBD | In-proc `SemaphoreSlim` for single-instance deploys |
| Keycloak | tbe-backoffice realm | ✓ | Phase 1 scaffolded realm (wrong role names — see Runtime State Inventory) | — |
| Stripe test mode | Webhook reconciliation (BO-06) | ✓ | Phase 5 in-use | — |
| SendGrid (or equivalent) | 4-eyes approval notifications (D-48) + statement delivery (D-54) | ✓ | Phase 3 D-17 sendgrid template pattern | — |
| Node.js 20+ | backoffice-web build | ✓ | Matches b2b-web | — |
| pnpm | backoffice-web package install | ✓ | Matches b2b-web | — |
| .NET 8 SDK | All new service code | ✓ | All services target `net8.0` | — |

**Missing dependencies with no fallback:** None identified. All infrastructure from Phases 1-5 covers Phase 6 needs.

**Missing dependencies with fallback:** Redis is the only "maybe" — if nightly jobs need cross-instance overlap locking (multi-replica BackofficeService), Redis distributed lock would be needed. For v1 single-instance deploy, in-proc `SemaphoreSlim` is sufficient (Pattern 3).

---

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit + FluentAssertions (.NET services); vitest + react-testing-library (portal); Playwright (E2E) |
| Config file | `*.Tests.csproj` per service + `vitest.config.ts` in `src/portals/backoffice-web/` |
| Quick run command | `dotnet test --filter FullyQualifiedName~Phase6` |
| Full suite command | `dotnet test && pnpm --filter backoffice-web test && pnpm --filter backoffice-web test:e2e` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| BO-01 | Unified list returns B2C+B2B+Manual | integration | `dotnet test --filter UnifiedBookingListTests` | ❌ Wave 0 |
| BO-02 | Manual booking persists with Channel=Manual, Status=Confirmed | integration | `dotnet test --filter ManualBookingHandlerTests` | ❌ Wave 0 |
| BO-03 | Cancel booking requires 4-eyes + records CancelledByStaff | integration | `dotnet test --filter StaffCancellationFourEyesTests` | ❌ Wave 0 |
| BO-04 | UPDATE/DELETE on BookingEvents throws SqlException | integration | `dotnet test --filter BookingEventsAppendOnlyTests` | ❌ Wave 0 |
| BO-05 | Snapshot column round-trips typed event payloads via JSON_VALUE | integration | `dotnet test --filter SnapshotJsonQueryTests` | ❌ Wave 0 |
| BO-06 | Nightly diff surfaces ledger-vs-Stripe drift | integration | `dotnet test --filter ReconciliationJobTests` | ❌ Wave 0 |
| BO-07 | SupplierContracts CRUD gates on ops-finance | integration | `dotnet test --filter SupplierContractPolicyTests` | ❌ Wave 0 |
| BO-08 | Excel export has Summary+Details sheets + totals | unit | `dotnet test --filter MisExcelExporterTests` | ❌ Wave 0 |
| BO-09 | `_error` consumer writes row to DeadLetterQueue | integration | `dotnet test --filter DlqConsumerTests` | ❌ Wave 0 |
| BO-10 | Requeue publishes to original queue + increments RequeueCount | integration | `dotnet test --filter DlqRequeueTests` | ❌ Wave 0 |
| CRM-01 | BookingConfirmed populates BookingProjections + Customers | integration | `dotnet test --filter BookingConfirmedConsumerTests` | ❌ Wave 0 |
| CRM-02 | WalletReserveCommand respects creditLimit | integration | `dotnet test --filter WalletReserveCreditLimitTests` | ❌ Wave 0 |
| CRM-03 | Agencies projection reflects AgencyRegistered event | integration | `dotnet test --filter AgencyProjectionTests` | ❌ Wave 0 |
| CRM-04 | CustomerCommunicationLogged persists markdown body | integration | `dotnet test --filter CommunicationLogTests` | ❌ Wave 0 |
| CRM-05 | UpcomingTrips populates from BookingConfirmed with future dates | integration | `dotnet test --filter UpcomingTripsProjectionTests` | ❌ Wave 0 |
| COMP-03 | Erasure NULLs BookingSagaState PII + writes tombstone + BookingEvents untouched | integration | `dotnet test --filter GdprErasureTests` | ❌ Wave 0 |
| D-38 | Markup rule save outside [0,500] or [0%,25%] → 400 | unit | `dotnet test --filter MarkupRuleValidationTests` | ❌ Wave 0 |
| D-39 | WalletCreditRequest self-approval → 403 | integration | `dotnet test --filter WalletCreditSelfApprovalTests` | ❌ Wave 0 |
| D-41 | Monthly statement job is idempotent across same-month runs | integration | `dotnet test --filter MonthlyStatementIdempotencyTests` | ❌ Wave 0 |
| D-47 | backoffice-web CSP has no Stripe origins (structural test) | unit | `pnpm --filter backoffice-web test -- csp-no-stripe` | ❌ Wave 0 |
| D-48 | Non-D-48 mutations do NOT require 4-eyes (negative test) | integration | `dotnet test --filter FourEyesScopeTests` | ❌ Wave 0 |
| Pitfall 17 | `components/ui/*.jsx` diff-equal to b2b-web (grep guard) | unit | `pnpm --filter backoffice-web test -- ui-jsx-byte-equal` | ❌ Wave 0 |

### Sampling Rate

- **Per task commit:** `dotnet test --filter FullyQualifiedName~{module}` (service-local)
- **Per wave merge:** `dotnet test` (all services) + `pnpm --filter backoffice-web test`
- **Phase gate:** Full suite + E2E smoke on backoffice-web running against a local docker-compose stack

### Wave 0 Gaps

- [ ] `src/services/BackofficeService/BackofficeService.Tests/BackofficeService.Tests.csproj` — new test project
- [ ] `src/services/CrmService/CrmService.Tests/CrmService.Tests.csproj` — new test project
- [ ] `src/services/BookingService/BookingService.Tests/Phase6/` — folder for BO-03/04/05 tests (extend existing csproj)
- [ ] `src/services/PaymentService/PaymentService.Tests/Phase6/` — folder for BO-06/D-39/D-41/CRM-02 tests
- [ ] `src/portals/backoffice-web/vitest.config.ts` — vitest setup (fork from b2b-web)
- [ ] `src/portals/backoffice-web/tests/csp-no-stripe.test.ts` — CSP structural guard (Pitfall 5 defense)
- [ ] `src/portals/backoffice-web/tests/ui-jsx-byte-equal.test.ts` — Pitfall 17 guard
- [ ] `tests/integration/Phase6/` root folder for cross-service integration tests (DLQ consumer, GDPR fan-out, 4-eyes workflows) — [ASSUMED] uses Testcontainers for SQL Server + RabbitMQ

---

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | YES | Keycloak OIDC via tbe-backoffice realm; Auth.js v5 edge-split (Pitfall 3); refresh token rotation |
| V3 Session Management | YES | Auth.js session cookie `__Secure-tbe-backoffice.session-token` with `httpOnly`, `sameSite=lax`, `secure`, `path=/backoffice` |
| V4 Access Control | YES | ASP.NET Core `[Authorize(Policy = "BackofficeXxxPolicy")]`; JWT claim single source of truth (Pitfall 28); 4-eyes self-approval guard (D-48) |
| V5 Input Validation | YES | `react-hook-form` + `zod` (portal); FluentValidation or manual guards (API); `application/problem+json` error shape |
| V6 Cryptography | YES | SHA-256 for EmailHash (System.Security.Cryptography); never hand-rolled. Stripe signature verification via `EventUtility.ConstructEvent` |
| V7 Error Handling & Logging | YES | Serilog compact JSON; no PII in logs; BookingEvents provides audit trail (D-49) |
| V8 Data Protection | YES | GDPR erasure tombstone (COMP-03); PII column-level NULL-on-erase; database encryption-at-rest inherited from infra |
| V9 Communications | YES | TLS inherited from Phase 1 infra; per-portal cookie scoping (Pitfall 19) |
| V10 Malicious Code | YES | No eval; CSP drops `unsafe-eval` (Plan 05-05 precedent carried forward); starter-kit `.jsx` untouched (Pitfall 17) |
| V11 Business Logic | YES | 4-eyes approval (D-48); markup bounds (D-52); credit limit hard block (D-61); role-gated mutations |
| V12 Files & Resources | N/A | No file upload in v1 (attachments deferred — D-62) |
| V13 API Security | YES | `application/problem+json`; rate-limiting inherited from gateway; CORS scoped per portal |
| V14 Configuration | YES | No secrets in git; env vars for all secrets; docker-compose + .env.example pattern |

### Known Threat Patterns for Backoffice / Event-Sourced Stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| SQL injection on MIS date-range filter | Tampering | EF Core parameterized queries; never string-concat user input into JSON_VALUE path |
| Privilege escalation via role-gate UI-only | Elevation | Every API endpoint enforces `[Authorize(Policy = ...)]`; integration test hits endpoint with `ops-read` token → expect 403 |
| Self-approval in 4-eyes | Elevation | Explicit `RequestedBy != actor` guard in controller (Pattern 6) |
| GDPR tombstone bypass via projection rebuild | Disclosure | CRM consumer checks EmailHash on every event; rebuild respects tombstones |
| CSRF on mutating endpoints | Tampering | Auth.js `sameSite=lax` cookie + CSRF token for non-idempotent actions; Next.js Server Actions have built-in CSRF |
| Replay of Stripe webhook events | Tampering | StripeEvents table with EventId PK (Stripe ID is unique); dedup at ingress |
| DLQ requeue ping-pong | Denial-of-service | `RequeueCount` cap + mandatory reason text (Pitfall 2) |
| PII leak via structured logs | Disclosure | Serilog destructuring policy strips `CustomerEmail`, `CustomerName`, `PassportNumber`, `DateOfBirth` fields globally |
| Cross-portal cookie confusion | Spoofing | Per-portal cookie names + JWT issuer check (Pitfall 19) |
| Append-only bypass via direct SQL | Tampering | DB role grants at engine level (D-49); app cannot bypass |
| Markdown injection in communication log | XSS (Tampering) | `rehype-sanitize` on server-side render (CRM-04) |
| Stripe webhook spoofing | Spoofing | Signature verification with 300s tolerance (existing StripeWebhookController) |
| Timing attack on approval endpoints | Disclosure | Constant-time username comparison via `string.Equals(StringComparison.Ordinal)` — OR better, compare IDs |

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Most deployments use single per-service SQL login with multiple role memberships (vs separate logins per role) | Pattern 1 | If ops requires per-role login, plan must create `tbe_booking_events_writer` login separately |
| A2 | Rename `payment.StripeWebhookEvents` → `payment.StripeEvents` should NOT happen — keep existing name to preserve D-55 intent | Runtime State Inventory | Plan may need to rename if ops wants D-55 name literally; impacts existing webhook controller code |
| A3 | PaymentService NOT included in GDPR erasure fan-out (wallet ledger is financial, not PII) | Runtime State Inventory | If PII exists in WalletTransactions.Notes field (for ManualCredit Notes), PaymentService must also participate |
| A4 | RabbitMQ durable queues retain messages indefinitely only if no ACK — CRM projection rebuild-from-scratch requires BookingEvents archive, not queue replay | Pattern 5 | If ops needs full rebuild capability, need separate event-archive table in BookingService + replay endpoint |
| A5 | Testcontainers used for integration tests needing SQL Server + RabbitMQ | Validation Architecture | If ops has mandated `docker-compose test` harness instead, plan must document that approach |
| A6 | `Cronos` NuGet is already in-stack from Phase 1 (used by TtlMonitorHostedService) | Pattern 3 | If not present, Phase 6 plan adds `dotnet add package Cronos` |
| A7 | Stripe webhook reconciliation uses only StripeEvents + WalletTransactions JOIN; does NOT re-call Stripe API | Pattern 3 / D-55 | If ops wants live Stripe verification, job must respect Stripe rate limits + circuit breaker |
| A8 | Redis distributed lock NOT required for nightly jobs in v1 (single-instance BackofficeService) | Environment Availability | Multi-replica deploy requires Redis lock OR DB-based advisory lock |
| A9 | ClosedXML sufficient for all MIS export sheet types — no need for pivot tables, charts inside Excel | Standard Stack | If Phase 7 adds in-Excel charts, would need to evaluate EPPlus (paid) or OpenXML SDK directly |
| A10 | QuestPDF commercial license already acquired in Phase 5 covers Phase 6 reuse | Standard Stack | If Phase 5 used community/free tier with usage limits, reverify coverage |
| A11 | `Fault<T>` envelope JSON is deserializable as `JsonObject` via `UseRawJsonDeserializer` | Pattern 2 | If envelope format is non-JSON (MessagePack), requires binary deserializer |
| A12 | Nightly job DST-safety via `Cronos` + UTC-scheduled crons (`0 3 * * *`) avoids ambiguous local time windows | Pitfall 3 | If ops needs local-time scheduling, must explicitly test DST transitions |

---

## Open Questions

1. **Dedicated BookingEvents DbContext vs main DbContext?**
   - What we know: Pitfall 1 mandates either a separate DbContext or raw ADO.NET for BookingEvents writes.
   - What's unclear: Operator preference — single context simplifies transactions (saga state + event written atomically) vs separate context prevents ChangeTracker accidents.
   - Recommendation: Plan 1 decides — recommend separate context + MassTransit outbox for atomic publish.

2. **`StripeEvents` table name — rename or keep?**
   - What we know: D-55 says `payment.StripeEvents`; existing table is `payment.StripeWebhookEvents`.
   - What's unclear: Whether D-55 wording is definitive or just descriptive.
   - Recommendation: Keep existing name (adds zero migration risk; update D-55 literal in plan).

3. **CRM projection rebuild mechanism?**
   - What we know: D-51 rejects full event-store engine.
   - What's unclear: Operator expectation for rebuild-from-scratch — is MT queue replay enough, or do we need a BookingEvents-backed archive?
   - Recommendation: Flag for user confirmation in plan — recommend storing a `crm.EventArchive` table alongside projection tables, populated by the same consumers, as the rebuild source.

4. **CustomerErasureTombstone shared across services or CRM-owned?**
   - What we know: D-57 mentions `crm.CustomerErasureTombstones` (CRM schema).
   - What's unclear: BookingService consumer needs to check tombstone too (for late events). Is the CRM table canonical + BookingService reads cross-service, or does each service keep its own?
   - Recommendation: Publish `CustomerErased` event; BookingService maintains its own lightweight `booking.CustomerErasureTombstones` mirror (EmailHash only). Cross-service DB reads violate D-51 boundary.

5. **4-eyes 72h expiry — sweeper job or read-time check?**
   - What we know: UI-SPEC says 72h expiry; status transitions to "Expired".
   - What's unclear: Implementation — hosted service that runs every hour vs status computed at read time.
   - Recommendation: Both. Read-time check for responsiveness (`Status = 'PendingApproval' AND ExpiresAt > NOW`), plus hourly hosted service to materialize the transition for dashboard counts.

6. **Monthly statement cut timezone?**
   - What we know: D-54 says "first business day of each calendar month"; Claude discretion on timezone + weekend handling.
   - What's unclear: UK calendar vs operator-locale.
   - Recommendation: UTC-based cut at 03:00 on 1st of month; "first business day" means if 1st is Sat/Sun, run on Mon — cron expression `0 3 1-3 * 1-5` (1st-3rd of month, weekdays only, 03:00 UTC) with idempotency guard prevents double-run.

7. **DLQ envelope serialization — raw JSON or payload-only?**
   - What we know: Claude discretion per CONTEXT.
   - Recommendation: Payload-only + typed MessageType column. Reconstruct envelope at requeue time (Pitfall 5).

8. **PaymentService participation in GDPR erasure fan-out?**
   - What we know: D-57 locks BookingService + CRM. PaymentService not explicitly named.
   - What's unclear: Does `WalletTransactions.Notes` (D-53 ManualCredit) carry PII?
   - Recommendation: Enforce at schema level — `Notes` is operator-authored, NOT customer-authored, and `ReasonCode` is enum-bounded. No PII expected. Exclude PaymentService from erasure fan-out. Document in plan.

9. **Cronos vs Quartz.NET for nightly jobs?**
   - What we know: BookingService uses `TtlMonitorHostedService` with `Task.Delay` pattern.
   - What's unclear: Whether Cronos is already added, or we need to add it.
   - Recommendation: Verify during Plan 1 task execution; add `Cronos` (lightweight, 0 deps) if missing. Do NOT adopt Quartz.NET (heavier, SQL-backed scheduling is overkill for v1).

10. **`ops-read` exports Excel?**
    - What we know: D-46 says `ops-read` is "view-only audit/MIS access". UI-SPEC table includes Excel export buttons on MIS page.
    - What's unclear: Does "view-only" preclude export (considered as read from DB → file download → data exfiltration) or allow it (same data just formatted)?
    - Recommendation: Allow export for `ops-read` (export == read). Document explicitly in plan's RBAC matrix.

---

## Sources

### Primary (HIGH confidence)
- **CONTEXT D-45..D-62** — locked decisions for all 18 elements
- **UI-SPEC** — frozen UX contract incl. slate-900 accent, 4-eyes badge, Excel export buttons
- **REQUIREMENTS.md §BO-01..BO-10, CRM-01..CRM-05, COMP-03** — acceptance criteria
- **`src/portals/b2b-web/next.config.mjs`** — per-route CSP reference
- **`src/portals/b2b-web/package.json`** — stack-lock reference
- **`src/services/BookingService/BookingService.Infrastructure/Ttl/TtlMonitorHostedService.cs`** — BackgroundService + DST pattern reference
- **`src/services/PaymentService/PaymentService.Infrastructure/Migrations/20260417000000_AddWalletAndStripe.cs`** — WalletTransactions append-only precedent
- **`src/services/PaymentService/PaymentService.Application/Consumers/StripeWebhookConsumer.cs`** — existing Stripe consumer to extend
- **`src/services/BookingService/BookingService.Application/Saga/SagaDeadLetter.cs`** — existing SagaDeadLetter (DIFFERENT pattern from D-58)
- **`src/shared/TBE.Contracts/Events/SagaEvents.cs`** — 22 existing contracts; add CustomerCommunicationLogged, WalletCreditApproved, CommissionPayoutApproved, BookingCancelledByStaff, CustomerErasureRequested, CustomerErased

### Secondary (MEDIUM confidence — verified against official source)
- [MassTransit `_error` queue docs](https://masstransit.io/documentation/concepts/exceptions) — fault envelope + header format. Redirects to massient.com mirror.
- [MassTransit Discussion #5971](https://github.com/MassTransit/MassTransit/discussions/5971) — raw JSON consumer pattern + `UseRawJsonDeserializer`.
- [SQL Server DENY permissions docs](https://learn.microsoft.com/en-us/sql/t-sql/statements/deny-object-permissions-transact-sql) — role-grant append-only.
- [Cronos NuGet](https://github.com/HangfireIO/Cronos) — DST-safe cron expression parsing.
- [ClosedXML wiki](https://github.com/ClosedXML/ClosedXML/wiki) — multi-sheet workbook, styles, formulas.
- [QuestPDF docs](https://www.questpdf.com/) — fluent document composition.
- [NuGet ClosedXML 0.105.0](https://www.nuget.org/packages/ClosedXML/0.105.0) — published 2024-12-10.
- [NuGet QuestPDF 2026.2.4](https://www.nuget.org/packages/QuestPDF) — latest.
- [Next.js App Router headers() docs](https://nextjs.org/docs/app/api-reference/config/next-config-js/headers) — per-route CSP.

### Tertiary (LOW confidence — training knowledge, not verified this session)
- [ASSUMED] ASVS V1-V14 category applicability exactly as tabulated above (ASVS v5.0 categories).
- [ASSUMED] Keycloak admin API supports realm re-import without data loss (used in Runtime State Inventory remediation).
- [ASSUMED] `rehype-sanitize` default schema covers markdown-to-safe-HTML needs for CRM-04 body.

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — versions verified on NuGet; every library has Phase 1-5 precedent or explicit doc reference
- Architecture: HIGH — every pattern has a Phase 1-5 precedent or an explicit MassTransit doc reference; only genuinely new pattern is the `_error` queue consumer
- Pitfalls: HIGH — 7 of 10 pitfalls are Phase 1-5 carry-forwards (Pitfalls 3, 5, 17, 19, 28) with confirmed mitigation code
- GDPR tombstone: MEDIUM — pattern is industry-standard but A3/A4 assumptions need user confirmation
- MassTransit `_error` queue consumer: MEDIUM — docs are thin; Pattern 2 code is based on community discussion #5971 and doc-inferred fault envelope shape
- 4-eyes workflow: HIGH — DB-status-column approach is simpler than saga and maps cleanly to D-48's 2 action scope
- Validation architecture: HIGH — every requirement has a concrete test; Wave 0 gaps are enumerated

**Research date:** 2026-04-19
**Valid until:** 2026-05-19 (30 days — stable .NET 8 + MassTransit 9.1 + Next.js 16 stack)
