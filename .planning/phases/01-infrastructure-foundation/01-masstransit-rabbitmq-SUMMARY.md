---
phase: 01-infrastructure-foundation
plan: 03
subsystem: messaging
tags: [masstransit, rabbitmq, outbox, ef-core, booking-service, contracts, event-driven]
requires: [docker-compose-stack, mssql-migrations-shared]
provides: [tbe-contracts, tbe-common-messaging, booking-service, masstransit-outbox]
affects: [all-services, booking-service, db-migrator]
tech_stack_added:
  - MassTransit-9.1.0
  - MassTransit.RabbitMQ-9.1.0
  - MassTransit.EntityFrameworkCore-9.1.0
tech_stack_patterns:
  - canonical-event-contracts-in-tbe-contracts
  - reusable-addtbemasstransitwithrabbitm-extension
  - ef-core-outbox-inbox-outboxstate-tables
  - test-consumer-for-phase1-verification
key_files_created:
  - src/shared/TBE.Contracts/Events/BookingEvents.cs
  - src/shared/TBE.Common/Messaging/MassTransitServiceExtensions.cs
  - src/services/BookingService/BookingService.API/BookingService.API.csproj
  - src/services/BookingService/BookingService.API/Program.cs
  - src/services/BookingService/BookingService.API/Dockerfile
  - src/services/BookingService/BookingService.Application/Consumers/TestBookingConsumer.cs
key_files_modified:
  - src/shared/TBE.Contracts/TBE.Contracts.csproj
  - src/shared/TBE.Common/TBE.Common.csproj
  - src/services/BookingService/BookingService.Application/BookingService.Application.csproj
  - src/services/BookingService/BookingService.Infrastructure/BookingDbContext.cs
  - TBE.slnx
decisions:
  - "TBE.Contracts has no external package dependencies — pure C# record types only; MassTransit derives exchange names from full type name TBE.Contracts.Events.* automatically"
  - "TBE.Common.MassTransitServiceExtensions delegates consumer and outbox registration to callers via Action<IBusRegistrationConfigurator> — avoids DbContext type coupling in shared library"
  - "AspNetCore.HealthChecks.Rabbitmq 9.0.0 uses factory: parameter (async lambda returning Task<IConnection>) — applied from plan 04 lessons learned"
  - "BookingService.Application.csproj given explicit ProjectReferences to TBE.Contracts and TBE.Common so consumers resolve event types without re-referencing from API layer"
  - "Program.cs uses AddEntityFrameworkOutbox with UseBusOutbox() so all publish operations inside a DbContext transaction are held in OutboxMessage table until committed"
metrics:
  duration: "12 minutes"
  completed: "2026-04-12"
  tasks_completed: 2
  tasks_total: 2
  files_created: 6
  files_modified: 5
---

# Phase 01 Plan 03: MassTransit + RabbitMQ Messaging Backbone Summary

**One-liner:** MassTransit 9.1.0 over RabbitMQ wired into BookingService as reference implementation — TBE.Contracts defines 4 canonical event records, TBE.Common provides AddTbeMassTransitWithRabbitMq() reusable extension, BookingDbContext registers all 3 EF Core outbox tables, and TestBookingConsumer verifies end-to-end message delivery.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Create TBE.Contracts and TBE.Common shared projects with event contracts and MassTransit extension | 03b1610 | BookingEvents.cs, MassTransitServiceExtensions.cs, TBE.Contracts.csproj, TBE.Common.csproj |
| 2 | Scaffold BookingService.API with MassTransit outbox and TestBookingConsumer | a020476 | BookingService.API.csproj, Program.cs, Dockerfile, TestBookingConsumer.cs, BookingService.Application.csproj, TBE.slnx |

## Acceptance Criteria Status

| Criterion | Status |
|-----------|--------|
| TBE.Contracts/Events/BookingEvents.cs contains exactly 4 record types | PASS |
| All 4 records in namespace TBE.Contracts.Events | PASS |
| TBE.Common.csproj references MassTransit Version="9.1.0" | PASS |
| TBE.Common.csproj references MassTransit.RabbitMQ Version="9.1.0" | PASS |
| MassTransitServiceExtensions.cs contains AddTbeMassTransitWithRabbitMq | PASS |
| dotnet build TBE.Contracts exits code 0 | PASS |
| dotnet build TBE.Common exits code 0 | PASS |
| Both projects in TBE.slnx | PASS |
| BookingDbContext.cs contains AddInboxStateEntity, AddOutboxMessageEntity, AddOutboxStateEntity (3 total) | PASS |
| TestBookingConsumer.cs implements IConsumer<BookingInitiated> and contains _logger.LogInformation | PASS |
| BookingService.API/Program.cs calls AddTbeMassTransitWithRabbitMq | PASS |
| BookingService.API/Program.cs calls AddEntityFrameworkOutbox<BookingDbContext> with UseBusOutbox() | PASS |
| BookingService.Infrastructure.csproj references MassTransit.EntityFrameworkCore Version="9.1.0" | PASS |
| BookingService.Infrastructure.csproj references Microsoft.EntityFrameworkCore.SqlServer Version="8.0.25" | PASS |
| dotnet build BookingService.API.csproj exits code 0 | PASS |
| All 3 BookingService projects in TBE.slnx | PASS |
| TBE.Contracts has no PackageReference (pure contracts) | PASS |

## MassTransit Version Confirmation

All MassTransit references use **9.1.0** (not 8.x):
- `MassTransit` 9.1.0 — TBE.Common.csproj, BookingService.Application.csproj
- `MassTransit.RabbitMQ` 9.1.0 — TBE.Common.csproj, BookingService.API.csproj
- `MassTransit.EntityFrameworkCore` 9.1.0 — BookingService.Infrastructure.csproj, BookingService.API.csproj

## Outbox Tables Created

Three MassTransit outbox tables registered in `BookingDbContext.OnModelCreating`:
- `InboxState` — deduplication tracking for received messages
- `OutboxMessage` — transactionally captured outbound messages
- `OutboxState` — delivery state tracking per endpoint

Migration creates these tables in `BookingDb` when `TBE.DbMigrator` runs.

## Consumer Topology

Exchange names derived from `TBE.Contracts.Events.*` full type name:
- `TBE.Contracts.Events:BookingInitiated` → queue `booking-service-booking-initiated` (convention-based)
- `TBE.Contracts.Events:BookingConfirmed` → exchange only (no consumer in Phase 1)
- `TBE.Contracts.Events:BookingFailed` → exchange only (no consumer in Phase 1)
- `TBE.Contracts.Events:PaymentProcessed` → exchange only (no consumer in Phase 1)

`TestBookingConsumer` consumes `BookingInitiated` and logs BookingId, Channel, ProductType at Information level.

## Build Verification Results

```
dotnet build TBE.Contracts    → 0 Error(s)
dotnet build TBE.Common       → 0 Error(s)
dotnet build BookingService.Infrastructure → 0 Error(s)
dotnet build BookingService.Application   → 0 Error(s)
dotnet build BookingService.API           → 0 Error(s)
```

All 5 projects compile clean with 0 errors, 0 warnings.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] RabbitMQ health check API: factory: parameter and async lambda**
- **Found during:** Task 2 (applied proactively from plan 04 lessons)
- **Issue:** Plan's Program.cs template used `rabbitConnectionFactory:` parameter name and `.GetAwaiter().GetResult()`, but `AspNetCore.HealthChecks.Rabbitmq` 9.0.0 renamed the parameter to `factory:` and expects an async lambda returning `Task<IConnection>`
- **Fix:** Used `factory: async sp => await connectionFactory.CreateConnectionAsync()` consistent with how all other service APIs were fixed in plan 04
- **Files modified:** src/services/BookingService/BookingService.API/Program.cs
- **Commit:** a020476

**2. [Rule 3 - Blocking] BookingService.Application.csproj missing ProjectReferences**
- **Found during:** Task 2 — TestBookingConsumer.cs references TBE.Contracts.Events types
- **Issue:** Plan 04 created a minimal Application.csproj stub with only `MassTransit` package reference; the plan 03 spec requires ProjectReferences to TBE.Contracts and TBE.Common for consumer resolution
- **Fix:** Added `<ProjectReference>` to TBE.Contracts and TBE.Common in BookingService.Application.csproj
- **Files modified:** src/services/BookingService/BookingService.Application/BookingService.Application.csproj
- **Commit:** a020476

### Pre-existing Stubs Completed (Not Deviations)

The following stubs created by plan 04 were completed/superseded by this plan as intended:
- `TBE.Contracts/Events/BookingEvents.cs` — plan 04 stub had records but lacked full XML doc comments; plan 03 added detailed phase-referenced comments
- `TBE.Common/Messaging/MassTransitServiceExtensions.cs` — plan 04 created the full implementation; verified identical, no changes needed
- `BookingDbContext.cs` — plan 04 created the full outbox implementation; verified all 3 calls present, no changes needed

## Known Stubs

None — all plan objectives fully implemented. BookingService is the reference messaging implementation for subsequent phases.

## Threat Flags

None — no new network endpoints or trust boundary changes beyond the plan's threat model. RabbitMQ connection uses env-var credentials (not hardcoded); OutboxMessage is scoped to BookingDb only.

## Self-Check: PASSED

Files verified on disk:
- src/shared/TBE.Contracts/Events/BookingEvents.cs — FOUND (4 records)
- src/shared/TBE.Common/Messaging/MassTransitServiceExtensions.cs — FOUND
- src/services/BookingService/BookingService.API/BookingService.API.csproj — FOUND
- src/services/BookingService/BookingService.API/Program.cs — FOUND
- src/services/BookingService/BookingService.API/Dockerfile — FOUND
- src/services/BookingService/BookingService.Application/Consumers/TestBookingConsumer.cs — FOUND
- src/services/BookingService/BookingService.Infrastructure/BookingDbContext.cs — FOUND (3 outbox calls)

Commits verified in git log:
- 03b1610: feat(01-03): create TBE.Contracts event records and TBE.Common MassTransit extension — FOUND
- a020476: feat(01-03): scaffold BookingService.API with MassTransit outbox and TestBookingConsumer — FOUND
