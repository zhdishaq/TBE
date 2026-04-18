# Phase 6: Backoffice & CRM - Context

**Gathered:** 2026-04-19
**Status:** Ready for planning

<domain>
## Phase Boundary

Deliver the operations team's single pane of glass: a `tbe-backoffice`-authenticated backoffice portal managing all bookings (B2C + B2B + manual/offline), staff-authored cancel/modify/refund flows with reason-logged audit trails, DB-level append-only `BookingEvents`, manual booking entry without GDS round-trips, supplier contract management, payment reconciliation, MIS reports with CSV/Excel export, a MassTransit dead-letter queue surface, and an event-sourced CRM (Customer 360, agency management, communication log, upcoming trips) projected from existing BookingService/PaymentService contracts. Folds in three Phase-5 deferrals (D-38 markup rule CRUD, D-39 post-ticket manual wallet credit, D-41 monthly commission payout batch) and COMP-03 GDPR erasure with a tombstone model.

**Out of scope for Phase 6** — deferred to later phases:
- Production hardening: distributed tracing, load testing, second-GDS cutover (Phase 7).
- Customer self-service GDPR erasure from the B2C portal (Phase 7 candidate).
- Stripe card-refund passthrough (v1 credits agency wallet only; PCI SAQ-A preserved).
- PDF export of MIS reports (CSV + Excel only in v1).
- Attachments on communication log (plain-text markdown body only in v1).
- Multi-dimensional markup rules (airline × class × date range) — still v2 per Phase 5 D-36.
- Per-booking immediate commission credit / offset-against-top-up payout mechanics.
- Agent self-service markup editing — explicitly disallowed (prevents unbounded self-margin).
- Hotel booking (Plan 04-03 still outstanding in Phase 4 backlog).

</domain>

<decisions>
## Implementation Decisions

### Scope & Plan Split
- **D-45:** **Phase 6 ships as 4 plans** matching ROADMAP's plan list, with all 16 requirements (BO-01..10, CRM-01..05, COMP-03) + the 3 Phase-5 deferrals (D-38, D-39, D-41) folded in. Plan-to-requirement mapping:
  - **Plan 1 — Unified booking management:** BO-01, BO-03, BO-04, BO-05, BO-09, BO-10 + **D-39** (post-ticket manual wallet credit).
  - **Plan 2 — Manual booking + supplier contracts + payment reconciliation:** BO-02, BO-07, BO-06.
  - **Plan 3 — MIS reporting + financial views:** BO-08 + **D-38** (markup CRUD) + **D-41** (commission payout).
  - **Plan 4 — CRM service + GDPR:** CRM-01, CRM-02, CRM-03, CRM-04, CRM-05 + **COMP-03**.

### Portal & RBAC
- **D-46:** **`tbe-backoffice` realm exposes 4 roles** — `ops-admin` (super-role, all mutations including markup CRUD + commission payout approve + GDPR erasure), `ops-cs` (customer service: view + cancel/modify bookings, manual booking entry, communication log writes), `ops-finance` (supplier contracts, payment reconciliation, markup CRUD, commission statement review, MIS reporting), `ops-read` (view-only audit/MIS access). Mirrors Phase 5's 3-role B2B precedent extended with a finance split.
- **D-47:** **New portal fork at `src/portals/backoffice-web/`** forked from `b2b-web` (Plan 05-00 pattern). `basePath: '/backoffice'`, cookie `__Secure-tbe-backoffice.session-token` (per-portal scoping continues Pitfall 19 mitigation), `tbe-backoffice` realm. Per-route CSP with NO Stripe origins (backoffice never takes card payments directly). Palette differentiation via slate-900 accent (not indigo-600 — preserves visual distinction from B2B). Auth.js v5 edge-split (Pitfall 3) carried forward. `gatewayFetch` Bearer forwarding (D-05). starterKit `.jsx` untouched (Pitfall 17).
- **D-48:** **4-eyes approval overlay on exactly two action classes** — (a) post-ticket manual wallet credit (D-39 refund flow) and (b) booking cancellations. Both require an `ops-admin` co-sign after the originating operator (`ops-finance` or `ops-cs`) opens the request. **All other mutations — including GDPR erasure (COMP-03) — are single-operator** with attributed audit-log entry as the compliance control. Rationale: audit-log-is-primary matches startup-ops reality; 4-eyes reserved for irreversible money/reputational moves.

### Audit Log & Event Sourcing
- **D-49:** **BO-04 DENY UPDATE/DELETE on `BookingEvents` via SQL Server role grants.** Dedicated DB role `booking_events_writer` with `GRANT INSERT, SELECT ON dbo.BookingEvents` + `DENY UPDATE, DELETE ON dbo.BookingEvents`. BookingService connects as that role (connection string uses the role-scoped SQL login). Rejected at the engine before any row touches — no trigger logic to maintain. Matches Phase 3 wallet append-only precedent (D-14).
- **D-50:** **BO-05 pricing-snapshot storage = single `Snapshot nvarchar(max)` JSON column on `BookingEvents`.** Holds the full event envelope (domain event fields + pricing breakdown following FLTB-03 shape + supplier response payload). Queryable via `JSON_VALUE` / `JSON_QUERY` for MIS + reconciliation. Forward-compatible with new event types without schema migrations. Typed meta columns stay narrow: `EventId PK`, `BookingId FK`, `EventType nvarchar(64)`, `OccurredAt datetime2`, `Actor nvarchar(128)` (username or service-account name), `CorrelationId guid`, `Snapshot nvarchar(max)`.
- **D-51:** **CRM read models via MassTransit consumers + `CrmDbContext` projection tables.** `CrmService` subscribes to the existing contracts: `BookingConfirmed`, `BookingCancelled`, `UserRegistered`, `WalletTopUp`, `TicketIssued`, plus a new `CustomerCommunicationLogged` for CRM-04. Consumers write to projection tables: `Customers`, `Agencies`, `BookingProjections`, `CommunicationLog`, `UpcomingTrips`. Idempotent via MassTransit outbox (`MessageId` dedup, Plan 03-01 pattern). No cross-service DB reads; no full event-store engine. Rebuild-from-scratch supported by re-subscribing to all published events from `MessageId=0` (topology preserved in RabbitMQ with durable queues).

### Money Flows (D-38 / D-39 / D-41 + BO-06)
- **D-52:** **D-38 markup rule CRUD = `ops-finance` + `ops-admin` only**, hard-bounded server-side validation (`FlatAmount ∈ [£0, £500]`, `PercentOfNet ∈ [0%, 25%]`), every mutation writes a row to `pricing.MarkupRuleAuditLog` (`RuleId`, `Actor`, `BeforeJson`, `AfterJson`, `Reason nvarchar(500)`, `ChangedAt`). Phase 5 D-36's max-2-active-rules-per-agency cap preserved. No approval workflow; `ops-finance` acts solo, bounded by hard ranges + audit log.
- **D-53:** **D-39 post-ticket refund = manual wallet credit via `BackofficeWalletCreditRequest` workflow.** ops-finance opens a request (AgencyId, Amount, ReasonCode, LinkedBookingId, Notes) → request lands in `PaymentService.WalletCreditRequests` with Status=PendingApproval → ops-admin reviews + approves → PaymentService atomically writes a `payment.WalletTransactions` row with `Kind = ManualCredit` + approval metadata. Reason code enum locked: `RefundedBooking | GoodwillCredit | DisputeResolution | SupplierRefundPassthrough`. **Never credits the Stripe card** — wallet-only keeps PaymentService decoupled from card-refund flows and preserves SAQ-A scope. 4-eyes enforced per D-48.
- **D-54:** **D-41 commission payout = monthly batch with ops-finance approval.** Nightly aggregator job populates `payment.CommissionAccruals` (rolling tally per agency from `BookingSagaState.AgencyCommissionAmount` for `Status = Confirmed` bookings). On the first business day of each calendar month a draft `AgencyMonthlyStatement` is generated per agency; ops-finance reviews + approves → job writes `payment.WalletTransactions` rows of `Kind = CommissionPayout` crediting each agency wallet AND archives a `AgencyStatement.pdf` (QuestPDF, mirrors Phase-5 `AgencyInvoiceDocument` pattern). Late arrivals (bookings confirmed after close) roll into next month's statement. Payout period configurable but defaults to calendar month.
- **D-55:** **BO-06 payment reconciliation = Stripe webhook subscription + nightly diff job.** Extend the existing PaymentService Stripe webhook subscriber (Phase 5 wallet top-up flow) to persist every event into `payment.StripeEvents` (EventId PK from Stripe, Type, CreatedAt, RawPayload JSON, Processed bit). Nightly job diffs `StripeEvents` against `payment.WalletTransactions` + `booking.BookingSagaState.PaymentIntentId`; any mismatch (orphan Stripe event, orphan wallet row, ledger-vs-Stripe amount drift) lands in `payment.PaymentReconciliationQueue` with a reason code. Backoffice `/backoffice/payments/reconciliation` surface lists the queue (like BO-09 DLQ).

### Tactical Decisions
- **D-56:** **BO-02 manual booking entry = no-GDS record.** Staff enters supplier reference (GDS PNR already issued in terminal, OR non-GDS supplier booking ref), fare breakdown, passenger list, itinerary. Booking lands in `BookingSagaState` with `Channel = Manual` (new enum value, `BookingChannel { B2C=0, B2B=1, Manual=2 }`), `Status = Confirmed`, no saga ran. No GDS API calls. Shows up in the unified list alongside online bookings with a "manual" channel tag per the ROADMAP UAT.
- **D-57:** **COMP-03 GDPR erasure = staff-initiated with PII tombstone.** `ops-admin` triggers erasure from `/backoffice/customers/{id}/erase`. `BookingSagaState` PII columns (`CustomerName`, `CustomerEmail`, `CustomerPhone`, `PassportNumber`, `DateOfBirth`) set to NULL. A row in `crm.CustomerErasureTombstones` records `OriginalCustomerId`, `EmailHash` (SHA-256 for dedup if the same person returns), `ErasedAt`, `ErasedBy`. `BookingEvents` remain untouched (already immutable per D-49) — CRM projection layer applies PII-replace-on-read redaction and labels affected bookings "Anonymized Customer". No FK cascade delete; financial audit preserved. Single-operator per D-48 (audit-log + irreversibility marker is the control).
- **D-58:** **BO-09/10 dead-letter queue via MassTransit `_error` queue consumer + `DeadLetterQueue` table.** A `BackofficeService` consumer tails the `_error` queues RabbitMQ creates on max-retry exceeded and writes each envelope into `backoffice.DeadLetterQueue` (`MessageType`, `OriginalQueue`, `Payload nvarchar(max)` JSON, `FailureReason nvarchar(1000)`, `FirstFailedAt`, `LastRequeuedAt nullable`, `RequeueCount int default 0`, `ResolvedAt nullable`, `ResolvedBy nullable`, `ResolutionReason nvarchar(500) nullable`). Backoffice view lists rows with filters (type, queue, date). "Requeue" republishes the original envelope to the live queue and increments `RequeueCount`. "Resolve manually" writes `ResolutionReason` + actor and marks archived (soft-hidden from the default view).
- **D-59:** **BO-08 MIS export formats = CSV (always) + Excel via ClosedXML (multi-sheet workbooks).** Workbook structure: Summary sheet (totals) + Details sheet (per-booking rows) + Totals row per subtable. PDF export deferred to Phase 7. Library: `ClosedXML` (pure-managed .xlsx writer, no Excel runtime required).
- **D-60:** **BO-08 MIS query model = daily rollup table.** Nightly job (CoreHostedService pattern per Phase 1) populates `reporting.MisDailyAggregates` (`Date date`, `Product nvarchar(16)`, `Channel nvarchar(16)`, `BookingsCount int`, `Revenue decimal(18,4)`, `Commission decimal(18,4)`, `PRIMARY KEY (Date, Product, Channel)`). Reports query aggregates; drill-down links hit raw `BookingSagaState` for on-demand per-booking rows. Trades: deviates from a pure live-query roadmap reading but satisfies "report for a selected date range" UAT with consistent performance.
- **D-61:** **CRM-02 agency credit limit = enforced at `WalletReserveCommand` in PaymentService.** The atomic reserve check becomes `(currentBalance + creditLimit) >= reserveAmount` (current `currentBalance >= reserveAmount` is strengthened to allow negative balance up to `-creditLimit`). Reserve fails with 402 Payment Required + `application/problem+json` when overdraft would exceed credit limit. `Agencies.CreditLimit decimal(18,4) NOT NULL DEFAULT 0` added via migration. Not alert-only — this is a hard block at booking time.
- **D-62:** **CRM-04 communication log = plain-text markdown-safe body only.** `crm.CommunicationLog` columns: `LogId GUID PK`, `EntityType nvarchar(16) CHECK (EntityType IN ('Customer','Agency'))`, `EntityId GUID` (Customer.Id or Agency.Id depending on EntityType), `CreatedBy nvarchar(128)`, `CreatedAt datetime2`, `Body nvarchar(max)` (markdown source; rendered server-side-safe in the portal). No attachments in v1. Internal-use only (never customer-visible).

### Claude's Discretion
- Exact EF Core column types/nullability/indexes on `BookingEvents`, `StripeEvents`, `PaymentReconciliationQueue`, `DeadLetterQueue`, `MisDailyAggregates`, `CommunicationLog`, `WalletCreditRequests`, `CommissionAccruals`, `AgencyMonthlyStatement`, `CustomerErasureTombstones`, `MarkupRuleAuditLog` beyond the fields explicitly listed.
- MassTransit contract additions vs reuse — e.g., whether `CommissionPayoutApproved`, `WalletCreditApproved`, `BookingCancelledByStaff`, `CustomerErased` become first-class integration events or stay service-local.
- Exact JSON shape inside the `Snapshot` column per event type (schema contract details).
- MIS nightly rollup job scheduling (cron time, timezone, DST handling, catch-up on missed runs).
- Monthly statement cut-over details (timezone, month-end vs first-of-next-month, weekend handling).
- `tbe-backoffice` realm JSON delta structure (mirror of `infra/keycloak/realm-tbe-b2b.json` pattern).
- `backoffice-web` navigation tree, page layouts, Radix component choices — UI-SPEC phase to follow.
- `AgencyStatement.pdf` QuestPDF document design + tokens.
- Dead-letter envelope serialization format (raw JSON vs MassTransit `ConsumeContext` snapshot).
- `PaymentReconciliationQueue` auto-resolve logic for known benign drift patterns (e.g., Stripe fees recorded only in Balance Transactions).
- Portal accent tokens beyond the slate-900 primary (surface, border, hover states).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & roadmap
- `.planning/REQUIREMENTS.md` — BO-01..BO-10, CRM-01..CRM-05, COMP-03 acceptance criteria
- `.planning/ROADMAP.md` §Phase 6 — plan list + UAT
- `.planning/ROADMAP.md` §Phase 7 — boundary check (what stays in Phase 7 hardening vs what folds here)

### Prior phase decisions (locked — must not be reversed)
- `.planning/phases/01-infrastructure-foundation/01-CONTEXT.md` — service layout, shared projects, RabbitMQ topology, Keycloak `tbe-backoffice` realm scaffolding
- `.planning/phases/03-core-flight-booking-saga-b2c/03-CONTEXT.md` — saga step ordering (D-05), wallet append-only ledger (D-14), `UPDLOCK+ROWLOCK+HOLDLOCK` (D-15), RazorLight + QuestPDF pattern (D-17/D-18), email idempotency (D-19), MassTransit outbox
- `.planning/phases/04-b2c-portal-customer-facing/04-CONTEXT.md` — starterKit fork pattern (D-01..D-03), Auth.js v5 edge-split (D-04), `gatewayFetch` Bearer forwarding (D-05), Pitfall 17 (.jsx untouched), per-route CSP
- `.planning/phases/05-b2b-agent-portal/05-CONTEXT.md` — D-32..D-44 (realm isolation, agency_id single source of truth, 4-role precedent, markup schema D-36, post-ticket deferral D-39, commission deferral D-41, portal-differentiation D-42, invoice PDF D-43, compact-UI D-44). Most directly relevant to portal + RBAC + money-flow decisions in this phase.

### Architecture & stack
- `.planning/research/ARCHITECTURE.md` — service topology, gateway routing (YARP routes for `/api/backoffice/*` still to be added)
- `.planning/research/STACK.md` — pinned library versions (ClosedXML to be added)
- `.planning/research/PITFALLS.md` — known traps (Pitfalls 17/19/28 apply to backoffice portal)
- `.planning/research/SUMMARY.md` — synthesized critical rules

### Existing code (scout findings)
- `src/services/BackofficeService/` — skeleton from Phase 1 (Program.cs only); Application + Infrastructure projects empty
- `src/services/CrmService/` — skeleton from Phase 1 (Program.cs only); Application + Infrastructure projects empty
- `src/portals/b2b-web/next.config.mjs` — per-route CSP reference (slate-down for backoffice, no Stripe origins)
- `src/portals/b2b-web/app/(portal)/**` — RSC + Auth.js v5 pattern to clone
- `src/services/PaymentService/**` — wallet ledger pattern; Stripe webhook subscriber pattern; KeycloakB2BAdminClient pattern (for recipient resolution on email fan-out)
- `src/services/BookingService/**` — saga state machine; QuestPDF invoice generator (D-43) + PdfPig negative-grep test pattern
- `infra/keycloak/realm-tbe-b2b.json` — realm delta pattern to mirror for `tbe-backoffice`

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`BackofficeService` + `CrmService` skeletons** — Program.cs stubs exist from Phase 1 infrastructure scaffold; Application + Infrastructure projects structured but empty. Ready for each of the 4 plans to attach domain code.
- **MassTransit outbox pattern (Plan 03-01)** — idempotent event publishing with `MessageId` dedup. Reuse for CRM projection consumers (D-51), dead-letter consumer (D-58), commission-payout publishers (D-54).
- **QuestPDF + PdfPig negative-grep testing (Plan 05-04)** — `AgencyInvoiceDocument` pattern ports directly to `AgencyMonthlyStatement` (D-54). `[Collection("QuestPDF")]` xUnit serialization to avoid static-license race on Windows.
- **Per-portal Auth.js v5 edge-split (Plan 04-00 / 05-00)** — `auth.config.ts` for middleware, `lib/auth.ts` for Node-runtime session/refresh. Clone for `backoffice-web`.
- **`gatewayFetch` Bearer forwarding (Plan 04-01)** — single helper in `lib/api-client.ts` reads session + prepends `Authorization: Bearer`. Clone for `backoffice-web`.
- **`KeycloakB2BAdminClient` service-account pattern (Plan 05-01)** — 30s-skew in-process token cache, Node-runtime-only, never touches Edge. Port to `KeycloakBackofficeAdminClient` if staff-admin operations need it.
- **`AgentPortalBadge` pattern (Plan 05-04 D-42)** — wordmark-badge component for portal differentiation. Clone as `BackofficePortalBadge`.
- **TanStack Query 30s polls (WalletChip, Plan 05-05)** — refetchInterval pattern for dashboard live-data panes (reconciliation queue depth, DLQ depth).
- **Payment ledger pattern (`payment.WalletTransactions` append-only)** — extend `Kind` enum with `ManualCredit | CommissionPayout` (D-53 / D-54) and add `ApprovedBy` / `ApprovalNotes` columns.
- **Stripe webhook subscriber (Plan 05-03)** — already persists `payment_intent.succeeded` + `refunded`; extend to persist every event for reconciliation (D-55).

### Established Patterns
- **Append-only ledger** — Phase 3 wallet + Phase 6 `BookingEvents` (D-49) + Phase 6 `StripeEvents` (D-55) + Phase 6 `DeadLetterQueue` (D-58) all follow it. SQL Server role grants (D-49) are the locked enforcement pattern.
- **Portal fork-per-audience** — b2c-web + b2b-web → backoffice-web continues Pitfall 19 mitigation (per-portal cookie scoping, separate Keycloak realm, no shared session surface).
- **MassTransit contracts as the service boundary** — Phase 6 never reads BookingService DB from CrmService; only consumes events (D-51).
- **Hard server-side validation on money writes** — Phase 5 D-40 (wallet top-up caps) + Phase 6 D-52 (markup rule bounds). `application/problem+json` shape with `allowedRange` extensions.
- **Policy-scoped auth** — B2BPolicy / B2BAdminPolicy precedent from Plan 05-01 → add BackofficeReadPolicy / BackofficeCsPolicy / BackofficeFinancePolicy / BackofficeAdminPolicy; every mutating controller action gates on the tightest applicable policy.

### Integration Points
- **Keycloak `tbe-backoffice` realm** — Phase 1 scaffolded it empty; Phase 6 populates roles + test users via realm delta (`infra/keycloak/realm-tbe-backoffice.json`).
- **YARP gateway** — add JWT scheme `tbe-backoffice` + routes for `/api/backoffice/*` + `/api/crm/*` mirror Plan 05-01's `tbe-b2b` + `B2BPolicy` addition.
- **BookingService saga state** — extend `BookingChannel` enum (D-56 adds `Manual=2`); add `CancelledByStaff`, `CancellationReason`, `CancellationApprovedBy` columns for BO-03 + D-48 4-eyes.
- **PaymentService wallet ledger** — extend `Kind` enum (D-53 `ManualCredit`, D-54 `CommissionPayout`); add `Agencies.CreditLimit` (D-61).
- **PricingService** — add `MarkupRuleAuditLog` table + audit triggers in service layer (D-52).
- **CrmService** — brand new consumers + DbContext migrations for D-51 projection tables.
- **Notification templates** — 4-eyes approval request emails (ops-finance → ops-admin), commission statement PDF delivery, GDPR erasure confirmation (internal).

</code_context>

<specifics>
## Specific Ideas

- **4-eyes approval UX** — opening operator sees a pending-approval banner with countdown to auto-expire (e.g., 72 hours); approver sees a queue badge in the portal header; approve/deny buttons record reason text.
- **Reason codes as enums, not free text** — D-53 refund reason codes + BO-03 cancellation reason codes + DLQ resolution reason codes all lock as `nvarchar(64)` CHECK-constraint enums. Makes MIS categorisation queryable.
- **Commission statement PDF as brand continuity** — use the same slate-900-accent header + indigo-600 brand ink as the agency invoice; `ops-finance` approves before send; archived under `/backoffice/agencies/{id}/statements/{period}`.
- **Dead-letter requeue is "fire-and-remember"** — the DLQ row stays with `RequeueCount++` so ops can see retry churn, not just success/failure.
- **GDPR tombstone dedup via `EmailHash`** — if the same real person returns and books again, `CustomerErasureTombstones.EmailHash` lets staff warn "this email was previously erased" without re-exposing the PII.
- **Negative-balance credit-limit UX** — B2B agent portal wallet chip renders `balance: -£150 / limit: -£500` in orange when under zero; at -credit-limit the reserve fails with a clear error payload and agent-admin sees "top up wallet" CTA.

</specifics>

<deferred>
## Deferred Ideas

### To Phase 7 (Hardening & Go-Live)
- Customer-self-service GDPR erasure from B2C portal (adds frontend scope + dedup reconciliation with staff-initiated flow)
- MIS PDF export
- Distributed tracing spans for backoffice mutations
- 4-eyes on high-value refund above a threshold (layering onto D-48 basic 4-eyes)

### To v2 / Future Milestones
- Stripe card-refund passthrough (`refunds.create` when original charge is <120d old); currently wallet-only (D-53)
- Per-booking immediate commission credit or offset-against-top-up payout (rejected in favour of monthly batch — D-54)
- Attachments on communication log (PDFs, images)
- Multi-dimensional markup rules (airline × class × date range) — v2 per D-36
- Customer self-service markup editing — explicitly disallowed (unbounded self-margin risk)
- Hard delete with FK cascade — rejected (violates BO-04 audit)
- 5-role RBAC with separate CS-vs-booking-edit split — rejected (4 roles chosen, D-46)
- Full event-store engine (Marten / EventStoreDB) — rejected (MassTransit + projection tables sufficient, D-51)
- Real-time MIS without daily rollup — rejected (D-60 chose rollup for consistent perf)
- Hotel manual-booking integration — depends on Plan 04-03 hotel-booking shipping first
- 4-eyes on GDPR erasure — user declined; audit-log + irreversibility marker is the control per D-48

</deferred>

---

*Phase: 06-backoffice-crm*
*Context gathered: 2026-04-19*
