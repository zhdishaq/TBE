---
phase: 06-backoffice-crm
plan: 03
subsystem: backoffice
tags: [markup-audit, d-52, partial, scaffold-only]
status: partial
requires:
  - 06-01
provides:
  - pricing.MarkupRuleAuditLog (schema + migration + DbContext + POCO — NOT YET wired to controller)
affects:
  - PricingService data layer (DbContext)
tech-stack:
  added: []
  patterns:
    - Append-only audit log mirrored off rule mutation (D-52 plan)
key-files:
  created:
    - src/services/PricingService/PricingService.Application/Agency/MarkupRuleAuditLogRow.cs
    - src/services/PricingService/PricingService.Infrastructure/Migrations/20260603000000_AddMarkupRuleAuditLog.cs
  modified:
    - src/services/PricingService/PricingService.Infrastructure/PricingDbContext.cs
decisions: []
metrics:
  duration: "~40 minutes (foundation scaffold only)"
  tasks_completed: 0
  tasks_partial: 1
  completed_date: "2026-04-19"
---

# Phase 06 Plan 03: Partial — D-52 Audit-Log Scaffold Only

**One-liner:** Started Plan 06-03 (D-38 markup CRUD + BO-08 MIS + D-41 commission payouts) but stopped after
landing the D-52 audit-log data-layer scaffold (entity + migration + DbContext wiring) because the plan's full
scope — 3 tasks across ~60 files, 3 backend services, and a portal, with a plan-embedded
`checkpoint:human-verify` mid-flight — exceeds a single autonomous executor session. The plan is flagged
`autonomous: false` in its own frontmatter and has a blocking checkpoint between Task 2 and Task 3 by design.

## What Shipped (Commit `34dd414`)

### D-52 Audit Log Data Layer
- **`MarkupRuleAuditLogRow` POCO** (`pricing` schema): Id (bigint identity), RuleId, AgencyId (denormalised
  for agency-scoped queries), Action (Created/Updated/Deactivated/Deleted), Actor (preferred_username),
  BeforeJson (nullable, null on Created), AfterJson (nullable, null on Deleted), Reason (nvarchar(500)
  required), ChangedAt (datetime2 default SYSUTCDATETIME()).
- **EF Core migration `20260603000000_AddMarkupRuleAuditLog`** — creates `pricing.MarkupRuleAuditLog`,
  CHECK(Action IN ...) at the DB level, three composite descending indexes
  (`AgencyId+ChangedAt`, `RuleId+ChangedAt`, `Actor+ChangedAt`) sized for the audit-tab drill-down + the
  compliance "who did what?" query. Timestamp is chosen so it lands after Plan 05-02's
  `20260416000000_AddAgencyMarkupRules`.
- **`PricingDbContext`** — registers `DbSet<MarkupRuleAuditLogRow>` + `OnModelCreating` config
  (ToTable("MarkupRuleAuditLog","pricing"), CHECK mirror via `t.HasCheckConstraint`, property types, three
  descending indexes matching the migration). Builds clean
  (`dotnet build src/services/PricingService/PricingService.Infrastructure` — 0 errors, 3 pre-existing
  CS8669 warnings in auto-generated ModelSnapshot, out-of-scope per Rule scope boundary).

## What Is NOT Done (and Why)

### Task 1 — remaining (~11 files)
- `MarkupRulesController` (GET/POST/PUT/DELETE + `/audit-log`) with hard bounds (FlatAmount £[0,500],
  PercentOfNet [0,25]) and atomic rule+audit writes.
- `PricingService.API/Program.cs` — add Backoffice JWT scheme (audience `tbe-api`,
  `ValidateAudience=true`) + 4 `Backoffice*Policy` registrations (ReadPolicy, CsPolicy, FinancePolicy,
  AdminPolicy) copied from `BackofficeService.API/Program.cs`.
- YARP gateway route for `/api/pricing/markup-rules/*` → PricingService cluster.
- 3 portal pages under `src/portals/backoffice-web/app/(portal)/agencies/markup-rules/` (page.tsx,
  markup-rule-form.tsx, audit-log-tab.tsx).
- 3 route-handler proxies.
- New `tests/Pricing.Tests/MarkupRuleAuditTests.cs` **integration suite** (PLAN path reconciliation:
  PLAN calls the project `TBE.PricingService.Tests` but repo convention has it as `tests/Pricing.Tests/` —
  this is Deviation Rule 3 territory; the existing csproj already has the EF InMemory dep needed for bounds
  tests, but Testcontainers-MsSql is NOT referenced and would need to be added for the atomicity fact).

### Task 2 — BO-08 MIS + CSV/XLSX (~22 files, NOT started)
- `reporting.MisDailyAggregates` migration + POCO + DbContext wiring on `BackofficeDbContext`.
- Cross-schema read via a second `BookingReadDbContext` (option A in plan) pointing at BookingDb.
- `MisAggregateQueries` (raw SQL MERGE), `MisDailyAggregateJob` (Cronos `"30 2 * * *"`),
  `MisReportService`, `IMisExporter` + `MisCsvExporter` + `MisExcelExporter` (ClosedXML 0.105.0 — new
  NuGet dependency per D-59), `MisController`, portal report page + 4 route handlers.
- `MisDailyAggregateTests` + `MisExportTests` integration tests.

### Checkpoint — plan-embedded `checkpoint:human-verify` (blocks Task 3 entry)
- After Task 2 ships the plan explicitly mandates a user-verification stop with
  `<resume-signal>approved</resume-signal>` before the executor can touch commission payouts.

### Task 3 — D-41 Commission accrual + monthly statement + single-operator payout + QuestPDF (~22 files, NOT started)
- Three PaymentService migrations (`AddCommissionAccruals`, `AddAgencyMonthlyStatements`,
  `AddCommissionPayoutKind` — extends the `payment.WalletTransactions.Kind` CHECK to include
  `'CommissionPayout'`, adds `LinkedStatementId` column).
- POCOs, `CommissionAccrualService` (raw SQL cross-schema read of `dbo.BookingSagaState`),
  `MonthlyStatementService` (per-agency tx + ClosedXML PDF via QuestPDF), Cronos-scheduled hosted services
  (`CommissionAccrualJob "0 3 * * *"`, `MonthlyStatementJob "0 4 1-3 * *"` with IsBusinessDay gate).
- `IAgencyStatementPdfGenerator` + `QuestPdfAgencyStatementGenerator` (D-54 inversion — statement MUST
  contain "NET"/"Markup"/"Commission" strings, whereas Phase 5 AgencyInvoiceDocument MUST NOT).
- `CommissionPayoutsController` (GET list / POST approve / GET pdf / bulk-approve 207 Multi-Status).
- `TBE.Contracts.Events.CommissionPayoutApproved` record.
- 3 portal pages (`/finance/commission-payouts/*`) + 3 route handlers.
- `CommissionPayoutTests` integration suite (on `tests/Payments.Tests/` — PLAN calls it
  `TBE.PaymentService.Tests`, repo convention is `Payments.Tests`; another Rule-3 path reconciliation).

## Deferred — Reason

**Plan size vs. single-session budget.** The plan manifest lists 60+ files across:
- 3 backend services (PricingService, BackofficeService, PaymentService) each needing new migrations, new
  DbContext configuration, new hosted services, new controllers, new policy wiring, and gateway route
  additions.
- 1 portal (backoffice-web) needing 8 new pages + 10 new route handlers.
- 3 new test suites with integration-test fixtures (Testcontainers-MsSql + EF InMemory hybrid).
- 1 new NuGet package (ClosedXML 0.105.0) and 1 new package source-of-truth pin (Cronos).

The plan is `autonomous: false` and contains an explicit `checkpoint:human-verify` gate between Task 2 and
Task 3 (`resume-signal: approved`). Its own shape assumes interactive execution with human approvals mid-way.

## Next Executor Resume Contract

A follow-up executor session should:

1. **Resume at Task 1 implementation layer** — read `06-03-PLAN.md` §Task 1 "E. Portal pages" onwards.
2. **Start by landing `MarkupRulesController.cs`** with the exact bounds-check + single-tx rule+audit write
   pattern specified in the `<action>` block (lines 338-408 of the plan). Mirror
   `WalletCreditRequestsController` for Problem+json shape and actor-fail-closed.
3. **Before adding the Backoffice scheme to PricingService Program.cs**, note: this is the FIRST Phase-6
   touch to PricingService. The existing Program.cs only wires the default JwtBearer scheme; plan requires
   adding a separate `Backoffice` scheme while preserving existing auth (see plan §D).
4. **Run `dotnet test tests/Pricing.Tests/PricingService.Tests.csproj --filter "FullyQualifiedName~MarkupRuleAuditTests" --no-restore`** after each commit; plan mandates RED-first TDD per task.
5. **At plan-embedded checkpoint (post-Task-2)** — pause per `<resume-signal>approved</resume-signal>`.
6. **Task 3 QuestPDF inversion test** is a hard correctness signal: Phase 5 `AgencyInvoiceDocument` tests
   assert `!contains("NET"/"Markup"/"Commission")` — the new statement generator must assert the inverse.
   A shared `[Collection("QuestPDF")]` serialization is required (from 05-04-SUMMARY decisions).

## TDD Gate Compliance

This partial session landed a `feat(06-03): D-52 MarkupRuleAuditLog scaffold ...` commit WITHOUT a preceding
`test(06-03): ...` RED commit. Plan 06-03 marks Task 1 as `tdd="true"`. On resume, the executor should either
(a) consider the data-layer scaffold as pre-TDD infrastructure (table-stakes plumbing, not behaviour) and
start the RED gate when `MarkupRulesController` behaviour is introduced, or (b) retro-add a
`test(06-03)` commit covering DbSet registration + migration up/down before any further work.
Flagging here per the executor skill rules.

## Commits

| Task | Hash    | Message |
|------|---------|---------|
| 1 (partial — data layer) | `34dd414` | feat(06-03): D-52 MarkupRuleAuditLog scaffold — entity + migration + DbContext |

## Self-Check: PASSED (partial)

**Files verified present:**
- `src/services/PricingService/PricingService.Application/Agency/MarkupRuleAuditLogRow.cs` — FOUND
- `src/services/PricingService/PricingService.Infrastructure/Migrations/20260603000000_AddMarkupRuleAuditLog.cs` — FOUND
- `src/services/PricingService/PricingService.Infrastructure/PricingDbContext.cs` — MODIFIED (DbSet + OnModelCreating block added)

**Commit verified in git log:** `34dd414` — FOUND

**Build verified:** `dotnet build src/services/PricingService/PricingService.Infrastructure/` — 0 errors.

## Deferred Items (from Plan 06-03, awaiting future executor session)

1. Task 1 — Implementation layer (MarkupRulesController, Program.cs Backoffice scheme, portal pages,
   route handlers, `MarkupRuleAuditTests`). ~11 files.
2. Task 2 — BO-08 MIS full surface (migration, POCO, DbContext, hosted job, exporters, controller,
   portal, 2 integration tests). ~22 files.
3. Checkpoint — `checkpoint:human-verify` between Task 2 and Task 3 per plan.
4. Task 3 — D-41 Commission accrual + monthly statement + QuestPDF + payout controller + portal.
   ~22 files.
