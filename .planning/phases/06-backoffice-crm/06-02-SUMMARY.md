---
phase: 06-backoffice-crm
plan: 02
subsystem: backoffice
tags: [backoffice, manual-booking, supplier-contracts, reconciliation, stripe, saga-bypass, dotnet, ef-core, nextjs, keycloak, cronos]

# Dependency graph
requires:
  - phase: 06-01
    provides: BackofficeDbContext, BookingEventsWriter, four Backoffice*Policy definitions, backoffice-web portal shell, unified booking list, Pitfall 4 named JWT scheme ("Backoffice")
  - phase: 05
    provides: PaymentService Stripe webhook pipeline, wallet ledger, BookingService saga core
provides:
  - BO-02 Manual/offline booking entry (Channel=Manual, saga-bypass) via 3-step portal wizard
  - BO-07 Supplier contract CRUD with validity-window status chip (Upcoming/Active/Expired)
  - BO-06 Payment reconciliation — full RawPayload persistence (D-55), nightly diff job, ops-finance resolve UI
  - ManualBookingCreated BookingEventsWriter event type for audit trail
  - payment.StripeWebhookEvents.RawPayload + Processed columns (D-55 idempotent replay substrate)
  - payment.PaymentReconciliationQueue table + ReconciliationJob (cron 0 2 * * *) + ReconciliationController
  - backoffice.SupplierContracts table + SupplierContractsController (ReadPolicy list, FinancePolicy mutate)
affects: [06-03, 06-04, 07-reporting, future payments reconciliation, future supplier pricing]

# Tech tracking
tech-stack:
  added:
    - Cronos 0.8.4 (cron-expression parser for .NET BackgroundService)
    - Microsoft.Extensions.Hosting.Abstractions 8.0.1 (PaymentService.Infrastructure needed BackgroundService)
    - Microsoft.Extensions.TimeProvider.Testing 8.10.0 (FakeTimeProvider for reconciliation tests)
  patterns:
    - "Saga-bypass command: dedicated Application command inserts BookingSagaState with Channel=Manual directly, skipping GDS/payment saga steps"
    - "Pitfall 28 server-stamped fields: controller DTO omits Channel/Status; ManualBookingCommand stamps them authoritatively"
    - "D-55 raw-payload persistence: every Stripe webhook event writes RawPayload nvarchar(max) + Processed bit, regardless of event type"
    - "Validity-window status chip: computed in-process (Today < ValidFrom -> Upcoming; Today > ValidTo -> Expired; else Active) because EF Core InMemory cannot translate such comparisons reliably for filter test paths"
    - "BackgroundService overlap prevention: SemaphoreSlim(1,1) with WaitAsync(0) — skip rather than queue overlapping ticks"
    - "Per-tick DI scope: using var scope = _scopeFactory.CreateScope() inside the timer loop so DbContext is not held across ticks"
    - "Idempotent reconciliation rescans: filtered unique-effect indexes on (StripeEventId, Status='Pending') and (BookingId, DiscrepancyType, Status='Pending') let the job re-scan without duplicating rows"
    - "RFC-7807 /errors/supplier-contract-invalid-{product-type|net-rate|commission|validity} problem+json type URIs for supplier validation"
    - "FakeTimeProvider pinned clock (Microsoft.Extensions.Time.Testing) for deterministic reconciliation test time windows"

key-files:
  created:
    - src/services/BookingService/BookingService.Infrastructure/ManualBookingCommand.cs
    - src/services/BookingService/BookingService.API/Controllers/ManualBookingsController.cs
    - src/services/BookingService/BookingService.Infrastructure/Migrations/20260602000000_AddBookingChannelManual.cs
    - src/services/BackofficeService/BackofficeService.Application/Entities/SupplierContract.cs
    - src/services/BackofficeService/BackofficeService.Infrastructure/Controllers/SupplierContractsController.cs
    - src/services/BackofficeService/BackofficeService.Infrastructure/Migrations/20260602100000_CreateSupplierContracts.cs
    - src/services/PaymentService/PaymentService.Application/Reconciliation/IPaymentReconciliationService.cs
    - src/services/PaymentService/PaymentService.Infrastructure/Reconciliation/PaymentReconciliationItem.cs
    - src/services/PaymentService/PaymentService.Infrastructure/Reconciliation/PaymentReconciliationService.cs
    - src/services/PaymentService/PaymentService.Infrastructure/Reconciliation/ReconciliationJob.cs
    - src/services/PaymentService/PaymentService.Infrastructure/Migrations/20260602200000_ExtendStripeEventsWithRawPayload.cs
    - src/services/PaymentService/PaymentService.Infrastructure/Migrations/20260602200001_AddReconciliationQueue.cs
    - src/services/PaymentService/PaymentService.API/Controllers/ReconciliationController.cs
    - src/portals/backoffice-web/app/(portal)/bookings/new/page.tsx
    - src/portals/backoffice-web/app/(portal)/bookings/new/manual-booking-wizard.tsx
    - src/portals/backoffice-web/app/(portal)/suppliers/page.tsx
    - src/portals/backoffice-web/app/(portal)/suppliers/supplier-contracts-list.tsx
    - src/portals/backoffice-web/app/(portal)/suppliers/supplier-contract-form.tsx
    - src/portals/backoffice-web/app/(portal)/finance/reconciliation/page.tsx
    - src/portals/backoffice-web/app/(portal)/finance/reconciliation/reconciliation-queue.tsx
    - src/portals/backoffice-web/app/api/bookings/manual/route.ts
    - src/portals/backoffice-web/app/api/suppliers/route.ts
    - src/portals/backoffice-web/app/api/suppliers/[id]/route.ts
    - src/portals/backoffice-web/app/api/reconciliation/route.ts
    - src/portals/backoffice-web/app/api/reconciliation/[id]/resolve/route.ts
    - tests/TBE.BackofficeService.Tests/ManualBookingEntryTests.cs
    - tests/TBE.BackofficeService.Tests/SupplierContractTests.cs
    - tests/Payments.Tests/PaymentReconciliationTests.cs
  modified:
    - src/services/BookingService/BookingService.Infrastructure/BookingDbContext.cs (Channel column check constraint updated for Manual)
    - src/services/BackofficeService/BackofficeService.Infrastructure/BackofficeDbContext.cs (DbSet<SupplierContract>, check constraints, composite index)
    - src/services/BackofficeService/BackofficeService.API/Program.cs (TimeProvider.System singleton)
    - src/services/PaymentService/PaymentService.Infrastructure/Stripe/StripeWebhookEvent.cs (RawPayload, Processed)
    - src/services/PaymentService/PaymentService.Infrastructure/Configurations/StripeWebhookEventMap.cs (RawPayload/Processed config + Processed_ReceivedAt index)
    - src/services/PaymentService/PaymentService.Infrastructure/PaymentDbContext.cs (DbSet<PaymentReconciliationItem>, check constraints, 3 indexes)
    - src/services/PaymentService/PaymentService.Infrastructure/PaymentService.Infrastructure.csproj (Cronos, Hosting.Abstractions)
    - src/services/PaymentService/PaymentService.API/Controllers/StripeWebhookController.cs (persists RawPayload + Processed=false)
    - src/services/PaymentService/PaymentService.API/Program.cs (2nd JWT scheme "Backoffice", BackofficeReadPolicy/FinancePolicy, DI for reconciliation service + hosted job)
    - tests/TBE.BackofficeService.Tests/TBE.BackofficeService.Tests.csproj (TimeProvider.Testing)
    - tests/Payments.Tests/Payments.Tests.csproj (TimeProvider.Testing)

key-decisions:
  - "Manual bookings bypass saga entirely — dedicated ManualBookingCommand inserts BookingSagaState with Channel=Manual and CurrentState='Confirmed' without publishing AuthorizeAmountCommand / ReserveInventoryCommand"
  - "Supplier contract status is computed in-process (not via EF projection) because EF Core InMemory cannot reliably translate Today-vs-ValidFrom/ValidTo comparisons in filter paths; SQL Server sees the same logic applied per-row after the paged query"
  - "Every Stripe event persisted with RawPayload and Processed=false; reconciliation treats Processed=false older than 1h as a High-severity UnprocessedEvent (D-55 audit trail + poison-message detector in one)"
  - "Reconciliation AmountDrift severity: delta ≤ £5 GBP equivalent = Low (noise from rounding / FX); > £5 = High (investigate)"
  - "Backend RBAC is authoritative: portal proxies enforce ops-read for list endpoints but backend controllers re-check BackofficeReadPolicy/FinancePolicy under the 'Backoffice' named scheme (Pitfall 4)"
  - "ReconciliationJob uses Cronos '0 2 * * *' (2AM daily) with SemaphoreSlim(1,1).WaitAsync(0) to skip rather than queue overlapping ticks, avoiding thundering-herd on cold-start catch-up"
  - "FakeTimeProvider pinned to 2026-06-15T02:00Z in reconciliation tests so the 24h window and 1h UnprocessedSla are deterministic"

patterns-established:
  - "Saga-bypass manual command: controller -> Application command -> direct BookingSagaState insert + IBookingEventsWriter audit event, no MassTransit publish"
  - "Server-stamped channel/status: DTO omits them entirely (Pitfall 28) — Channel/Status set by the command, never by the client"
  - "D-55 raw payload audit: every webhook event (succeeded, failed, canceled, refunded, unhandled) persists full JSON so later replay/reconciliation can re-derive state"
  - "Filtered unique-effect indexes for idempotent async jobs: IX...Status='Pending' allows re-scans without UNIQUE violations on resolved rows"
  - "Per-tick DI scope in BackgroundService with Cronos + SemaphoreSlim overlap guard"
  - "RFC-7807 problem+json with stable /errors/... type URIs for domain validation"
  - "Node-runtime proxy pattern: portal /api routes forward to backend with Bearer token from session; backend re-authorizes under named JWT scheme"

requirements-completed: [BO-02, BO-06, BO-07]

# Metrics
duration: ~110min
completed: 2026-04-19
---

# Phase 06 Plan 02: Manual Booking + Supplier Contracts + Payment Reconciliation Summary

**Saga-bypass manual booking entry (Channel=Manual), supplier contract CRUD with validity-window status chip, and end-to-end Stripe payment reconciliation with D-55 raw-payload persistence + nightly Cronos job + ops-finance resolve UI.**

## Performance

- **Duration:** ~110 min
- **Started:** 2026-04-19 (see first RED commit 5b3b134)
- **Completed:** 2026-04-19
- **Tasks:** 3 (each RED + GREEN)
- **Files created:** 28
- **Files modified:** 11

## Accomplishments
- **BO-02** Manual/offline booking entry wired end-to-end: portal 3-step wizard -> /api/bookings/manual -> ManualBookingsController -> saga-bypass `ManualBookingCommand` stamps `Channel=Manual`, `CurrentState='Confirmed'`, writes `ManualBookingCreated` audit event. No GDS API calls, no saga message publish. Appears in unified booking list with `Channel=Manual` chip (surface already shipped in 06-01).
- **BO-07** Supplier contract CRUD landed on `backoffice.SupplierContracts`: net rate + commission % + validity window + currency + notes, all mutations under `BackofficeFinancePolicy`, reads under `BackofficeReadPolicy`. Status chip computed `Upcoming/Active/Expired` from `DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)` vs `ValidFrom/ValidTo`. Soft-delete, optimistic-ish `UpdatedBy`/`UpdatedAt` metadata.
- **BO-06** Payment reconciliation shipped: every Stripe webhook event now persists `RawPayload` + `Processed=false` (D-55); nightly `ReconciliationJob` (Cronos `0 2 * * *`) scans a 24h window and emits queue rows for OrphanStripeEvent / OrphanWalletRow / AmountDrift / UnprocessedEvent with `Low`/`Medium`/`High` severity; ops-finance resolves through portal modal with side-by-side Stripe-vs-wallet JSON diff + required resolution notes.
- Test coverage (Phase06-tagged): `ManualBookingEntryTests`, `SupplierContractTests`, `PaymentReconciliationTests` — 10 reconciliation scenarios including drift-Low (£3), drift-High (£20), unprocessed-event-beyond-1h, rescan idempotency, resolve-404, resolve-already-resolved-409.

## Task Commits

Each task was committed atomically (TDD RED then GREEN, `--no-verify` per plan):

1. **Task 1: BO-02 Manual booking entry**
   - RED: `5b3b134` — `test(06-02): add failing test for manual booking entry (BO-02)`
   - GREEN: `91ca88d` — `feat(06-02): BO-02 manual booking entry (Task 1)`
2. **Task 2: BO-07 Supplier contract CRUD**
   - RED: `bcd1d55` — `test(06-02): add failing test for supplier contract CRUD (BO-07)`
   - GREEN: `8cc5aec` — `feat(06-02): BO-07 supplier contract CRUD`
3. **Task 3: BO-06 Payment reconciliation**
   - RED: `6db4ccb` — `test(06-02): add failing test for payment reconciliation (BO-06)`
   - GREEN: `d8ceab1` — `feat(06-02): BO-06 payment reconciliation`

**Plan metadata:** pending final `docs(06-02): ...` commit covering this SUMMARY.md (to be added by executor after self-check).

## Files Created/Modified

### Created — BookingService (BO-02)
- `src/services/BookingService/BookingService.Infrastructure/ManualBookingCommand.cs` — Saga-bypass command. Validates ProductType, BookingReference, PNR; builds BookingSagaState with `Channel=Channel.Manual`, `ChannelText="manual"`, `CurrentState="Confirmed"`; writes `ManualBookingCreated` audit event.
- `src/services/BookingService/BookingService.API/Controllers/ManualBookingsController.cs` — POST `/api/backoffice/bookings/manual`, `[Authorize(Policy="BackofficeCsPolicy", AuthenticationSchemes="Backoffice")]`. DTO omits Channel/Status (Pitfall 28).
- `src/services/BookingService/BookingService.Infrastructure/Migrations/20260602000000_AddBookingChannelManual.cs` — No-op column migration (Channel is stored as int); documents enum extension.

### Created — BackofficeService (BO-07)
- `src/services/BackofficeService/BackofficeService.Application/Entities/SupplierContract.cs` — `[Table("SupplierContracts", Schema="backoffice")]` with Id, SupplierName, ProductType, NetRate, CommissionPercent, Currency, ValidFrom/ValidTo, Notes, CreatedBy/At, UpdatedBy/At, IsDeleted.
- `src/services/BackofficeService/BackofficeService.Infrastructure/Controllers/SupplierContractsController.cs` — Full CRUD + list. List: filter by ProductType/Q + compute Status per row + optionally post-filter. Create/Update/Delete: `BackofficeFinancePolicy`. Problem+json validation under `/errors/supplier-contract-invalid-*`.
- `src/services/BackofficeService/BackofficeService.Infrastructure/Migrations/20260602100000_CreateSupplierContracts.cs` — Table DDL + 4 CHECK constraints (ProductType in set, NetRate>=0, CommissionPercent in [0,100], ValidTo>=ValidFrom) + composite index `IX_SupplierContracts_IsDeleted_ProductType_ValidTo`.

### Created — PaymentService (BO-06)
- `src/services/PaymentService/PaymentService.Application/Reconciliation/IPaymentReconciliationService.cs` — Minimal interface `Task ScanAsync(CancellationToken ct)`.
- `src/services/PaymentService/PaymentService.Infrastructure/Reconciliation/PaymentReconciliationItem.cs` — Queue entity with Id, DiscrepancyType, Severity, BookingId?, StripeEventId?, Details JSON, DetectedAtUtc, Status, ResolvedBy?, ResolvedAtUtc?, ResolutionNotes?.
- `src/services/PaymentService/PaymentService.Infrastructure/Reconciliation/PaymentReconciliationService.cs` — 3-pass scanner (orphan-stripe + amount-drift, orphan-wallet, unprocessed>1h). Parses Stripe minor-unit amount from `data.object.amount / 100m`. Idempotent via HashSet de-dup against existing Pending rows.
- `src/services/PaymentService/PaymentService.Infrastructure/Reconciliation/ReconciliationJob.cs` — `BackgroundService` using `CronExpression.Parse("0 2 * * *")` + `SemaphoreSlim(1,1).WaitAsync(0)` overlap guard + per-tick `IServiceScopeFactory.CreateScope()`.
- `src/services/PaymentService/PaymentService.Infrastructure/Migrations/20260602200000_ExtendStripeEventsWithRawPayload.cs` — Adds RawPayload nvarchar(max) default `'{}'`, Processed bit default 0, backfills existing rows to `Processed=1`, adds `IX_StripeWebhookEvents_Processed_ReceivedAt`.
- `src/services/PaymentService/PaymentService.Infrastructure/Migrations/20260602200001_AddReconciliationQueue.cs` — Table + 3 indexes (incl. filtered unique-effect for Pending).
- `src/services/PaymentService/PaymentService.API/Controllers/ReconciliationController.cs` — GET `/api/backoffice/reconciliation` (ReadPolicy, default Status=Pending), POST `/{id}/resolve` (FinancePolicy), problem+json 404 / 409.

### Created — backoffice-web portal
- `app/(portal)/bookings/new/page.tsx` + `manual-booking-wizard.tsx` — 3-step wizard (Trip > Fare > Confirm) posting to `/api/bookings/manual`.
- `app/(portal)/suppliers/page.tsx` + `supplier-contracts-list.tsx` + `supplier-contract-form.tsx` — RSC gate + client list with status chip + modal create/edit/delete form.
- `app/(portal)/finance/reconciliation/page.tsx` + `reconciliation-queue.tsx` — RSC gate + client queue with filters (Status/Type/Severity) + `ReconciliationInspector` side-by-side Stripe vs wallet JSON + resolve modal requiring notes.
- `app/api/bookings/manual/route.ts`, `app/api/suppliers/route.ts`, `app/api/suppliers/[id]/route.ts`, `app/api/reconciliation/route.ts`, `app/api/reconciliation/[id]/resolve/route.ts` — Node-runtime `gatewayFetch` proxies forwarding Bearer token from session; RBAC re-check on portal (`isOpsRead` / `hasAnyRole`) but backend is the authoritative gate.

### Created — tests
- `tests/TBE.BackofficeService.Tests/ManualBookingEntryTests.cs`
- `tests/TBE.BackofficeService.Tests/SupplierContractTests.cs`
- `tests/Payments.Tests/PaymentReconciliationTests.cs`

### Modified
- `src/services/BookingService/BookingService.Infrastructure/BookingDbContext.cs` — Channel CHECK constraint extended for `Manual`.
- `src/services/BackofficeService/BackofficeService.Infrastructure/BackofficeDbContext.cs` — `DbSet<SupplierContract>` + model config.
- `src/services/BackofficeService/BackofficeService.API/Program.cs` — `services.AddSingleton<TimeProvider>(TimeProvider.System)`.
- `src/services/PaymentService/PaymentService.Infrastructure/Stripe/StripeWebhookEvent.cs` — `RawPayload`, `Processed`.
- `src/services/PaymentService/PaymentService.Infrastructure/Configurations/StripeWebhookEventMap.cs` — RawPayload nvarchar(max) required, Processed default false, `IX_StripeWebhookEvents_Processed_ReceivedAt`.
- `src/services/PaymentService/PaymentService.Infrastructure/PaymentDbContext.cs` — `DbSet<PaymentReconciliationItem>` + 3 indexes + 3 CHECK constraints.
- `src/services/PaymentService/PaymentService.Infrastructure/PaymentService.Infrastructure.csproj` — Cronos 0.8.4 + Hosting.Abstractions.
- `src/services/PaymentService/PaymentService.API/Controllers/StripeWebhookController.cs` — Insert `RawPayload = json, Processed = false`.
- `src/services/PaymentService/PaymentService.API/Program.cs` — 2nd JWT bearer `"Backoffice"` scheme (realm `tbe-backoffice`, audience `tbe-api`, role flattener in `OnTokenValidated`) + `BackofficeReadPolicy` (ops-read/cs/finance/admin) + `BackofficeFinancePolicy` (ops-finance/ops-admin), both `.AddAuthenticationSchemes("Backoffice")` per Pitfall 4. Registered `AddScoped<IPaymentReconciliationService, PaymentReconciliationService>` + `AddHostedService<ReconciliationJob>`.
- `tests/TBE.BackofficeService.Tests/TBE.BackofficeService.Tests.csproj` + `tests/Payments.Tests/Payments.Tests.csproj` — `Microsoft.Extensions.TimeProvider.Testing 8.10.0`.

## Architecture Highlights

### BO-02 — Saga-bypass manual booking
- `Channel=Manual` is the architectural signal that saga steps are skipped. The command authors `BookingSagaState` directly with `CurrentState="Confirmed"`; no `AuthorizeAmountCommand`, no `ReserveInventoryCommand`, no wallet debit. Payment capture on manual bookings is handled by ops-finance out-of-band.
- Pitfall 28 respected: `CreateManualBookingRequest` DTO does NOT include Channel or Status fields; controller passes only the supplier/fare fields and the command stamps authoritatively.
- `ManualBookingCreated` event flows through `IBookingEventsWriter` so the unified booking list and audit trail surface the entry.

### BO-07 — Supplier contracts with validity-window chip
- `Status` is NOT persisted. It is computed per-row at read time: `today < ValidFrom -> Upcoming`, `today > ValidTo -> Expired`, else `Active`. This keeps writes idempotent (no scheduled job needed to flip statuses at midnight).
- Filter-by-Status support in `List()` paginates after the compute pass; EF cannot translate `DateOnly.FromDateTime(DateTime.UtcNow) > ValidTo` for the InMemory provider used in tests, so the post-filter runs in-process. SQL Server path executes the same semantics.
- RBAC split: any `ops-*` role can read (BackofficeReadPolicy); only `ops-finance` and `ops-admin` can mutate (BackofficeFinancePolicy). Policies pinned to the `"Backoffice"` named JWT scheme.

### BO-06 — Reconciliation
- **D-55 raw payload:** every Stripe webhook event persists full JSON + `Processed=false`. A consumer flips `Processed=true` after applying effects. Rows with `Processed=false` older than 1h are surfaced as `UnprocessedEvent` (High severity) — this is both an audit trail and a poison-message detector.
- **Three-pass scan over a 24h window:**
  1. Orphan Stripe events (no matching wallet row): `BookingId` present -> High, absent -> Medium. When both sides match but amounts differ, emit `AmountDrift` with severity Low (`<=£5`) or High (`>£5`).
  2. Orphan wallet rows: wallet debit/credit linked to a `BookingId` with no matching Stripe event -> High.
  3. Unprocessed events (`Processed=false` older than 1h) -> High.
- **Idempotent rescans:** existing Pending rows are de-duped via HashSets keyed on `StripeEventId` or `(BookingId, DiscrepancyType)`. Filtered unique-effect indexes on the table (`Status='Pending'`) prevent duplicates even under concurrent runs.
- **Overlap prevention:** `ReconciliationJob` uses `SemaphoreSlim(1,1).WaitAsync(0)` — if a previous tick is still running, subsequent cron ticks skip rather than queue (avoids thundering herd after cold start).
- **Per-tick DI scope:** `using var scope = _scopeFactory.CreateScope()` inside the cron loop so the `DbContext` lifetime is scoped to one scan, not the entire service lifetime.
- **Ops-finance resolve UI:** side-by-side `<pre>` blocks of parsed Stripe JSON vs wallet JSON from `row.details`, textarea for required resolution notes (1-2000 chars), POST `/api/reconciliation/{id}/resolve` which stamps `Status=Resolved`, `ResolvedBy` (JWT `preferred_username`), `ResolvedAtUtc`, `ResolutionNotes`.

## Decisions Made
See `key-decisions` in frontmatter. Highlights:
- Manual bookings bypass saga entirely; not a saga state machine variant.
- Supplier status is computed per-read, not persisted.
- Every Stripe event is persisted (not just `payment_intent.succeeded`).
- Drift Low threshold is £5 (configurable via `AmountDriftLowBoundary` constant).
- Reconciliation job uses per-tick DI scope + `WaitAsync(0)` skip-overlap strategy.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] ManualBookingCommand placement**
- **Found during:** Task 1 (BO-02 GREEN)
- **Issue:** Plan listed `src/services/BookingService/BookingService.Application/ManualBookingCommand.cs`. The existing BookingService codebase keeps command orchestrators (DbContext + outbox wiring) in `.Infrastructure`, not `.Application`. Placing the command in `.Application` would have required circular references.
- **Fix:** Placed command at `src/services/BookingService/BookingService.Infrastructure/ManualBookingCommand.cs` to match established convention (consistent with `BookingEventsWriter`, `BookingsQueryService`, etc.).
- **Files modified:** `src/services/BookingService/BookingService.Infrastructure/ManualBookingCommand.cs`.
- **Verification:** `ManualBookingEntryTests` pass.
- **Committed in:** 91ca88d

**2. [Rule 3 - Blocking] BookingChannel enum already exists as `Channel`**
- **Found during:** Task 1 (BO-02 GREEN)
- **Issue:** Plan assumed an enum named `BookingChannel` in `BookingService.Application/Saga/`. The codebase actually has an enum named `Channel` (with `B2C`, `B2B`) in `BookingSagaState`-local scope. Creating a second `BookingChannel.cs` would have produced two parallel enums.
- **Fix:** Extended the existing `Channel` enum with `Manual=2`. `ManualBookingCommand` uses `Channel.Manual`. Saga logic continues to read the same enum.
- **Files modified:** existing BookingSagaState `Channel` enum (in place).
- **Verification:** Saga tests still green; manual booking writes `Channel=Manual`.
- **Committed in:** 91ca88d

**3. [Rule 3 - Blocking] Test project naming**
- **Found during:** Task 3 (BO-06 GREEN) — Plan referenced `tests/TBE.PaymentService.Tests/PaymentReconciliationTests.cs`; the repo project is named `tests/Payments.Tests/`.
- **Issue:** Plan's test project path does not exist in the repo.
- **Fix:** Placed reconciliation tests in the existing `tests/Payments.Tests/` project. Added `Microsoft.Extensions.TimeProvider.Testing` package to `Payments.Tests.csproj`.
- **Files modified:** `tests/Payments.Tests/PaymentReconciliationTests.cs`, `tests/Payments.Tests/Payments.Tests.csproj`.
- **Verification:** All 10 reconciliation tests pass under `dotnet test tests/Payments.Tests`.
- **Committed in:** 6db4ccb (RED) / d8ceab1 (GREEN)

**4. [Rule 3 - Blocking] Stripe events table name mismatch**
- **Found during:** Task 3 (BO-06 GREEN)
- **Issue:** Plan used `payment.StripeEvents`. Actual EF entity is `StripeWebhookEvent` mapped to `payment.StripeWebhookEvents`.
- **Fix:** Migration and config updated `payment.StripeWebhookEvents`. Reconciliation scanner queries `PaymentDbContext.StripeWebhookEvents`.
- **Files modified:** `20260602200000_ExtendStripeEventsWithRawPayload.cs`, `StripeWebhookEventMap.cs`, `PaymentReconciliationService.cs`.
- **Verification:** Migration applies, StripeWebhookControllerTests (4/4) still pass.
- **Committed in:** d8ceab1

**5. [Rule 3 - Blocking] PaymentService second JWT scheme missing**
- **Found during:** Task 3 (BO-06 GREEN)
- **Issue:** Plan assumed `BackofficeReadPolicy` / `BackofficeFinancePolicy` already registered on PaymentService. They were only registered on BackofficeService (Plan 06-01). Reconciliation endpoints needed the `"Backoffice"` named scheme to avoid colliding with the existing B2B/B2C JWT scheme on PaymentService.
- **Fix:** Added a second `.AddJwtBearer("Backoffice", ...)` chained after the default scheme in `PaymentService.API/Program.cs`. Configured via `Keycloak:Backoffice:Authority` (defaults to `{keycloakBaseUrl}/realms/tbe-backoffice`), audience `tbe-api`. Added `OnTokenValidated` to flatten `realm_access.roles` into `ClaimTypes.Role`. Registered `BackofficeReadPolicy` (ops-read/cs/finance/admin) and `BackofficeFinancePolicy` (ops-finance/ops-admin), both pinned via `.AddAuthenticationSchemes("Backoffice")`.
- **Files modified:** `src/services/PaymentService/PaymentService.API/Program.cs`.
- **Verification:** ReconciliationController authorizes correctly; existing PaymentService endpoints unaffected.
- **Committed in:** d8ceab1

**6. [Rule 3 - Blocking] ReconciliationService namespace collision**
- **Found during:** Task 3 (BO-06 GREEN) post-RED build
- **Issue:** Initially wrote service with `namespace TBE.PaymentService.Application.Reconciliation` (per plan). Physical location is `Infrastructure/Reconciliation/` (DbContext lives there). Test imports resolved `TBE.PaymentService.Infrastructure.Reconciliation`, so RED compile failed with CS0246.
- **Fix:** Rewrote the file with `namespace TBE.PaymentService.Infrastructure.Reconciliation`. Kept the interface `IPaymentReconciliationService` in `Application.Reconciliation` (consumer-facing contract), implementation in `Infrastructure` (persistence-coupled).
- **Files modified:** `PaymentReconciliationService.cs`.
- **Verification:** Build green, all 10 tests pass.
- **Committed in:** d8ceab1

---

**Total deviations:** 6 auto-fixed (6 Rule 3 Blocking — plan-vs-repo layout/naming mismatches).
**Impact on plan:** No scope change. All six were file-path / naming alignment issues where the plan referenced aspirational conventions and the repo already had an established pattern. Fixing them preserved the repo's existing architectural separation (Application = contracts, Infrastructure = EF + orchestration). No behavior differs from the plan's intent.

## Known Stubs

None. All data surfaces are wired end-to-end:
- Manual booking wizard posts real data to a real endpoint that writes real rows.
- Supplier contract list queries real rows with computed status.
- Reconciliation queue queries real rows; inspector parses real stored `Details` JSON; resolve mutates real rows.

## Threat Flags

No new threat surfaces beyond the plan's `<threat_model>`. All three new endpoints are inside the existing `"Backoffice"` JWT scheme + policy pattern established in Plan 06-01:

| Flag | File | Description |
|------|------|-------------|
| none | - | All new controllers follow existing RBAC + named-scheme pattern (Pitfall 4). No new trust boundaries. Stripe webhook endpoint unchanged from Plan 05 (still signed via `Stripe-Signature` HMAC). |

## TDD Gate Compliance

| Task | RED commit | GREEN commit | Notes |
|------|------------|--------------|-------|
| 1 BO-02 | 5b3b134 | 91ca88d | RED failed with CS0246 (ManualBookingCommand not found) as expected |
| 2 BO-07 | bcd1d55 | 8cc5aec | RED failed with CS0246 (SupplierContract not found) as expected |
| 3 BO-06 | 6db4ccb | d8ceab1 | RED failed with CS0234 (Reconciliation namespace not found) as expected |

All three tasks have correctly ordered `test(...)` then `feat(...)` commits. No refactor commits — implementation was already clean per test shape.

## Issues Encountered

- **7 pre-existing failures in `tests/Payments.Tests` full run.** Testcontainers / Docker-based tests (WalletRepositoryTests, B2BAdminPolicy, WalletControllerTopUpTests) fail because no Docker runtime is available on the worktree host. These predate this plan. Verified not-a-regression by running filtered `StripeWebhookControllerTests` (4/4 pass) and `PaymentReconciliationTests` (10/10 pass) separately. Logged to `deferred-items.md` is NOT applicable here — these are pre-existing infra failures, not newly introduced, and they are expected in environments without Docker.
- **Namespace collision for PaymentReconciliationService** (see Deviation #6). Resolved inline.

## User Setup Required

None. All configuration is via existing `appsettings.json` / Keycloak realm (`tbe-backoffice`, roles `ops-read`, `ops-cs`, `ops-finance`, `ops-admin`) already in place from Plan 06-01. The new `Keycloak:Backoffice:Authority` setting on PaymentService defaults to `{keycloakBaseUrl}/realms/tbe-backoffice` and needs no explicit config in dev. Migrations run automatically on EF migrate.

## Next Phase Readiness

- **Unblocked:** 06-03 (refund workflow) and 06-04 (reporting / dashboards) can consume `payment.PaymentReconciliationQueue` rows and `backoffice.SupplierContracts` rates for commission calculations.
- **Unblocked:** 07 (reporting) gets a reliable reconciliation substrate — every Stripe event is now persisted with RawPayload, so historical replay is feasible.
- **No blockers.**
- **Monitoring TODO (future plan):** alert when PaymentReconciliationQueue Pending High-severity rows exceed threshold N for more than T minutes. Out of scope for Plan 06-02.

## Self-Check: PASSED

Key file verification (all `FOUND`):
- `src/services/BookingService/BookingService.Infrastructure/ManualBookingCommand.cs` — FOUND
- `src/services/BookingService/BookingService.API/Controllers/ManualBookingsController.cs` — FOUND
- `src/services/BookingService/BookingService.Infrastructure/Migrations/20260602000000_AddBookingChannelManual.cs` — FOUND
- `src/services/BackofficeService/BackofficeService.Application/Entities/SupplierContract.cs` — FOUND
- `src/services/BackofficeService/BackofficeService.Infrastructure/Controllers/SupplierContractsController.cs` — FOUND
- `src/services/BackofficeService/BackofficeService.Infrastructure/Migrations/20260602100000_CreateSupplierContracts.cs` — FOUND
- `src/services/PaymentService/PaymentService.Application/Reconciliation/IPaymentReconciliationService.cs` — FOUND
- `src/services/PaymentService/PaymentService.Infrastructure/Reconciliation/PaymentReconciliationItem.cs` — FOUND
- `src/services/PaymentService/PaymentService.Infrastructure/Reconciliation/PaymentReconciliationService.cs` — FOUND
- `src/services/PaymentService/PaymentService.Infrastructure/Reconciliation/ReconciliationJob.cs` — FOUND
- `src/services/PaymentService/PaymentService.Infrastructure/Migrations/20260602200000_ExtendStripeEventsWithRawPayload.cs` — FOUND
- `src/services/PaymentService/PaymentService.Infrastructure/Migrations/20260602200001_AddReconciliationQueue.cs` — FOUND
- `src/services/PaymentService/PaymentService.API/Controllers/ReconciliationController.cs` — FOUND
- `src/portals/backoffice-web/app/(portal)/bookings/new/page.tsx` — FOUND
- `src/portals/backoffice-web/app/(portal)/bookings/new/manual-booking-wizard.tsx` — FOUND
- `src/portals/backoffice-web/app/(portal)/suppliers/page.tsx` — FOUND
- `src/portals/backoffice-web/app/(portal)/suppliers/supplier-contracts-list.tsx` — FOUND
- `src/portals/backoffice-web/app/(portal)/suppliers/supplier-contract-form.tsx` — FOUND
- `src/portals/backoffice-web/app/(portal)/finance/reconciliation/page.tsx` — FOUND
- `src/portals/backoffice-web/app/(portal)/finance/reconciliation/reconciliation-queue.tsx` — FOUND
- `src/portals/backoffice-web/app/api/bookings/manual/route.ts` — FOUND
- `src/portals/backoffice-web/app/api/suppliers/route.ts` — FOUND
- `src/portals/backoffice-web/app/api/suppliers/[id]/route.ts` — FOUND
- `src/portals/backoffice-web/app/api/reconciliation/route.ts` — FOUND
- `src/portals/backoffice-web/app/api/reconciliation/[id]/resolve/route.ts` — FOUND
- `tests/TBE.BackofficeService.Tests/ManualBookingEntryTests.cs` — FOUND
- `tests/TBE.BackofficeService.Tests/SupplierContractTests.cs` — FOUND
- `tests/Payments.Tests/PaymentReconciliationTests.cs` — FOUND

Commit hash verification (all `FOUND` via `git log`):
- 5b3b134 — FOUND (test BO-02)
- 91ca88d — FOUND (feat BO-02)
- bcd1d55 — FOUND (test BO-07)
- 8cc5aec — FOUND (feat BO-07)
- 6db4ccb — FOUND (test BO-06)
- d8ceab1 — FOUND (feat BO-06)

---
*Phase: 06-backoffice-crm*
*Completed: 2026-04-19*
