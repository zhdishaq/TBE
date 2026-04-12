---
phase: 01-infrastructure-foundation
plan: 04
subsystem: infrastructure
tags: [db-migrator, mssql, ef-core, serilog, redis, health-checks, microservices, outbox]
requires: [docker-compose-stack]
provides: [db-migrator, service-scaffolds, structured-logging, health-checks]
affects: [all-services]
tech_stack_added:
  - MassTransit.EntityFrameworkCore-9.1.0
  - AspNetCore.HealthChecks.SqlServer-9.0.0
  - AspNetCore.HealthChecks.Rabbitmq-9.0.0
  - AspNetCore.HealthChecks.Redis-9.0.0
  - StackExchange.Redis-2.12.14
  - Microsoft.EntityFrameworkCore.SqlServer-8.0.25
tech_stack_patterns:
  - db-migrator-console-app-exit-codes
  - ef-core-outbox-per-service-dbcontext
  - serilog-compact-json-formatter
  - health-checks-sql-rabbitmq-redis
  - worker-sdk-for-notification-service
key_files_created:
  - src/tools/TBE.DbMigrator/TBE.DbMigrator.csproj
  - src/tools/TBE.DbMigrator/Program.cs
  - src/tools/TBE.DbMigrator/Dockerfile
  - src/services/PaymentService/PaymentService.API/Program.cs
  - src/services/PaymentService/PaymentService.Infrastructure/PaymentDbContext.cs
  - src/services/PricingService/PricingService.API/Program.cs
  - src/services/PricingService/PricingService.Infrastructure/PricingDbContext.cs
  - src/services/NotificationService/NotificationService.API/Program.cs
  - src/services/NotificationService/NotificationService.Infrastructure/NotificationDbContext.cs
  - src/services/CrmService/CrmService.API/Program.cs
  - src/services/CrmService/CrmService.Infrastructure/CrmDbContext.cs
  - src/services/BackofficeService/BackofficeService.API/Program.cs
  - src/services/BackofficeService/BackofficeService.Infrastructure/BackofficeDbContext.cs
  - src/services/SearchService/SearchService.API/Program.cs
  - src/services/FlightConnectorService/FlightConnectorService.API/Program.cs
  - src/services/HotelConnectorService/HotelConnectorService.API/Program.cs
  - src/shared/TBE.Contracts/Events/BookingEvents.cs
  - src/shared/TBE.Common/Messaging/MassTransitServiceExtensions.cs
  - src/services/BookingService/BookingService.Infrastructure/BookingDbContext.cs
key_files_modified:
  - TBE.slnx
decisions:
  - "MassTransit.EntityFrameworkCore namespace does not expose AddEntityFrameworkOutbox in its own namespace — extension lives under MassTransit namespace; removed invalid using MassTransit.EntityFrameworkCore directive"
  - "AspNetCore.HealthChecks.Rabbitmq 9.0.0 uses factory: parameter name (not rabbitConnectionFactory); returns Task<IConnection> directly without GetAwaiter().GetResult()"
  - "ProjectReference paths from service APIs to src/shared/ require ../../../../src/shared/ (4 levels up to repo root then back into src/shared), not ../../../../shared/ as plan specified"
  - "TBE.Contracts and TBE.Common stubs created in this worktree so service APIs compile in wave 1; plan 03 (wave 2) will complete these with full event contracts and MassTransit extension"
  - "BookingService.Infrastructure stub created for DbMigrator project reference; plan 03 overwrites this with full implementation"
  - "Microsoft.Extensions.Configuration 8.0.0 conflicts with Serilog.AspNetCore 10.0.0 dependency chain; removed explicit pin, replaced with EnvironmentVariables 9.0.0 only"
metrics:
  duration: "35 minutes"
  completed: "2026-04-12"
  tasks_completed: 2
  tasks_total: 2
  files_created: 98
  files_modified: 1
---

# Phase 01 Plan 04: MSSQL Migrations + Shared Service Scaffolds Summary

**One-liner:** TBE.DbMigrator console app migrates 6 service databases and ensures 3 stateless databases exist on startup; 8 remaining service APIs scaffolded with EF Core outbox DbContexts, CompactJsonFormatter Serilog, /health endpoints with SQL/RabbitMQ/Redis checks, and Dockerfiles — full TBE.slnx builds clean with 29 projects.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Create TBE.DbMigrator console app | fac7ae6 | TBE.DbMigrator.csproj, Program.cs, Dockerfile |
| 2 | Scaffold remaining 8 service projects with Serilog, Redis, health checks | dd0b03e | 98 files across 8 services + shared stubs |

## Acceptance Criteria Status

| Criterion | Status |
|-----------|--------|
| dotnet build TBE.slnx exits code 0 | PASS |
| TBE.DbMigrator/Program.cs contains 6 MigrateAsync< calls | PASS |
| TBE.DbMigrator/Program.cs contains 3 EnsureDbExistsAsync calls | PASS |
| TBE.DbMigrator/Program.cs contains return 0 and return 1 | PASS |
| TBE.DbMigrator.csproj references 6 Infrastructure ProjectReferences | PASS |
| src/tools/TBE.DbMigrator/Dockerfile exists with dotnet/sdk:8.0 | PASS |
| TBE.DbMigrator.csproj in TBE.slnx | PASS |
| All 5 DbContexts have modelBuilder.AddOutboxMessageEntity() | PASS |
| Every service Program.cs has WriteTo.Console(new CompactJsonFormatter()) | PASS |
| Every service Program.cs has app.MapHealthChecks("/health") | PASS |
| NotificationService.API.csproj uses Microsoft.NET.Sdk.Worker | PASS |
| FlightConnectorService has no Infrastructure project | PASS |
| HotelConnectorService has no Infrastructure project | PASS |
| dotnet sln TBE.slnx list shows at least 29 projects | PASS (29) |

## Project Count Added to TBE.slnx

Total projects in TBE.slnx: **29**

- 1 gateway (TBE.Gateway)
- 2 shared (TBE.Contracts, TBE.Common — stubs; plan 03 completes)
- 1 migrator (TBE.DbMigrator)
- BookingService: 2 projects (Application + Infrastructure stubs; API in plan 03)
- PaymentService: 3 projects (API + Application + Infrastructure)
- SearchService: 2 projects (API + Application, no Infrastructure)
- FlightConnectorService: 2 projects (API + Application, no Infrastructure)
- HotelConnectorService: 2 projects (API + Application, no Infrastructure)
- PricingService: 3 projects (API + Application + Infrastructure)
- NotificationService: 3 projects (API + Application + Infrastructure)
- CrmService: 3 projects (API + Application + Infrastructure)
- BackofficeService: 3 projects (API + Application + Infrastructure)

## Services with Infrastructure vs. Stateless

**With Infrastructure + DbContext (5 services):**
- PaymentService → PaymentDbContext → PaymentDb
- PricingService → PricingDbContext → PricingDb
- NotificationService → NotificationDbContext → NotificationDb
- CrmService → CrmDbContext → CrmDb
- BackofficeService → BackofficeDbContext → BackofficeDb

**Stateless / No Infrastructure (3 services):**
- SearchService — Redis-only, migrator runs EnsureCreatedAsync for SearchDb
- FlightConnectorService — no DB, migrator runs EnsureCreatedAsync for FlightConnectorDb
- HotelConnectorService — no DB, migrator runs EnsureCreatedAsync for HotelConnectorDb

**BookingService stub only (plan 03 completes):**
- BookingDbContext stub created for DbMigrator reference, plan 03 owns full implementation

## NuGet Package Versions Confirmed

| Package | Version |
|---------|---------|
| Microsoft.EntityFrameworkCore.SqlServer | 8.0.25 |
| Microsoft.EntityFrameworkCore.Tools | 8.0.25 |
| MassTransit.EntityFrameworkCore | 9.1.0 |
| StackExchange.Redis | 2.12.14 |
| Serilog.AspNetCore | 10.0.0 |
| Serilog.Formatting.Compact | 3.0.0 |
| AspNetCore.HealthChecks.SqlServer | 9.0.0 |
| AspNetCore.HealthChecks.Rabbitmq | 9.0.0 |
| AspNetCore.HealthChecks.Redis | 9.0.0 |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] TBE.Contracts and TBE.Common stubs created in wave-1 worktree**
- **Found during:** Task 2 build
- **Issue:** Service API Program.cs files reference `TBE.Common.Messaging.AddTbeMassTransitWithRabbitMq` and `TBE.Contracts.Events.*`, but plan 03 (wave 2) owns these projects. Without them the build fails in wave 1.
- **Fix:** Created minimal TBE.Contracts with 4 event records and TBE.Common with MassTransitServiceExtensions stub so the wave-1 solution compiles. Plan 03 will overwrite with full implementation.
- **Files modified:** src/shared/TBE.Contracts/, src/shared/TBE.Common/
- **Commit:** dd0b03e

**2. [Rule 3 - Blocking] BookingService.Infrastructure stub for DbMigrator reference**
- **Found during:** Task 1 — TBE.DbMigrator.csproj references BookingService.Infrastructure which plan 03 creates
- **Fix:** Created minimal BookingService.Application and BookingService.Infrastructure with BookingDbContext stub so DbMigrator compiles. Plan 03 will overwrite.
- **Files modified:** src/services/BookingService/BookingService.Infrastructure/, src/services/BookingService/BookingService.Application/
- **Commit:** dd0b03e

**3. [Rule 1 - Bug] Wrong ProjectReference path to src/shared/**
- **Found during:** Task 2 build — all service APIs failed to resolve TBE.Common namespace
- **Issue:** Plan specified `..\..\..\..\shared\TBE.Common\` but shared projects are at `src/shared/` so correct path is `..\..\..\..\src\shared\TBE.Common\`
- **Fix:** Updated all 8 service API csproj files with correct relative path using sed
- **Files modified:** All 8 service API .csproj files
- **Commit:** dd0b03e

**4. [Rule 1 - Bug] AspNetCore.HealthChecks.Rabbitmq 9.0.0 API change**
- **Found during:** Task 2 build
- **Issue:** Plan used `rabbitConnectionFactory:` parameter name but 9.0.0 API uses `factory:` and returns `Task<IConnection>` (not `.GetAwaiter().GetResult()`)
- **Fix:** Updated all Program.cs files to use `factory:` parameter and async lambda
- **Files modified:** All 8 service API Program.cs files
- **Commit:** dd0b03e

**5. [Rule 1 - Bug] MassTransit.EntityFrameworkCore has no separate namespace**
- **Found during:** Task 2 build
- **Issue:** `using MassTransit.EntityFrameworkCore;` causes CS0234 — extension methods live under `MassTransit` namespace, not a sub-namespace
- **Fix:** Removed invalid using directive; `using MassTransit;` alone brings in all extension methods when assembly is referenced
- **Files modified:** 5 DB service Program.cs files (Payment, Pricing, Notification, Crm, Backoffice)
- **Commit:** dd0b03e

**6. [Rule 1 - Bug] Microsoft.Extensions.Configuration version conflict**
- **Found during:** Task 1 build
- **Issue:** TBE.DbMigrator pinned `Microsoft.Extensions.Configuration` to 8.0.0 but `Serilog.AspNetCore` 10.0.0 transitively requires 10.0.0; NU1605 error
- **Fix:** Removed explicit `Microsoft.Extensions.Configuration` 8.0.0 pin; kept only `Microsoft.Extensions.Configuration.EnvironmentVariables` 9.0.0
- **Files modified:** src/tools/TBE.DbMigrator/TBE.DbMigrator.csproj
- **Commit:** fac7ae6

## Known Stubs

| File | Stub Type | Reason |
|------|-----------|--------|
| src/shared/TBE.Contracts/ | Partial implementation | Plan 03 owns full event contracts; these stubs enable wave-1 compilation |
| src/shared/TBE.Common/ | Partial implementation | Plan 03 owns MassTransitServiceExtensions with full host/connection config; these stubs enable wave-1 compilation |
| src/services/BookingService/BookingService.Infrastructure/ | Stub | Plan 03 owns full BookingService implementation; stub enables DbMigrator reference |
| All Infrastructure Class1.cs files | Generated placeholder | dotnet new classlib generates Class1.cs; harmless, not used |

## Threat Flags

None — no new network endpoints, auth paths, or trust boundary changes beyond what the plan's threat model already covers.

## Self-Check: PASSED

Files verified on disk:
- src/tools/TBE.DbMigrator/Program.cs — FOUND
- src/tools/TBE.DbMigrator/Dockerfile — FOUND
- src/services/PaymentService/PaymentService.Infrastructure/PaymentDbContext.cs — FOUND
- src/services/PricingService/PricingService.Infrastructure/PricingDbContext.cs — FOUND
- src/services/NotificationService/NotificationService.Infrastructure/NotificationDbContext.cs — FOUND
- src/services/CrmService/CrmService.Infrastructure/CrmDbContext.cs — FOUND
- src/services/BackofficeService/BackofficeService.Infrastructure/BackofficeDbContext.cs — FOUND

Commits verified in git log:
- fac7ae6: feat(01-04): create TBE.DbMigrator — FOUND
- dd0b03e: feat(01-04): scaffold 8 remaining service projects — FOUND

Build result: `dotnet build TBE.slnx -c Release` — 0 Error(s)
