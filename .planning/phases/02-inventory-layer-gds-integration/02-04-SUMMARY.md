---
phase: 02-inventory-layer-gds-integration
plan: "04"
subsystem: pricing-markup-hybridcache-booking-tokens
tags:
  - inventory
  - pricing
  - markup
  - hybridcache
  - redis
  - booking-tokens
  - rate-limiting
dependency_graph:
  requires:
    - "02-01: IFlightAvailabilityProvider, UnifiedFlightOffer, PriceBreakdown, TBE.Contracts"
    - "02-02: FlightSearchOrchestrator, SearchController HTTP client pattern"
    - "02-03: HotelConnectorService, PricingDbContext (MassTransit outbox already in place)"
  provides:
    - "IPricingRulesEngine + MarkupRulesEngine: Percentage/FixedAmount markup with MaxAmount cap and carrier specificity"
    - "PricingController POST /pricing/apply: applies markup rules to UnifiedFlightOffer[]"
    - "ISearchCacheService + SearchCacheService: HybridCache Browse TTL (10 min) + Selection TTL (90 sec)"
    - "Booking token storage: booking-token:{sessionId} in Redis with offer.ExpiresAt TTL"
    - "GDS rate-limit guard: in-process sliding window 8 req/sec on SearchController"
    - "PriceBreakdown extended with GrossSellingPrice and MarkupApplied fields"
    - "AddMarkupRules EF migration"
  affects:
    - "03-*: Booking saga can retrieve stored fare snapshot via GET /search/flights/token/{sessionId}"
    - "05-*: B2B pricing channel uses Channel=B2B in PricingContext"
tech_stack:
  added:
    - "Microsoft.Extensions.Caching.Hybrid 9.3.0 — L1+L2 tiered cache with stampede protection"
    - "Microsoft.Extensions.Caching.StackExchangeRedis 9.0.5 — Redis L2 backend for HybridCache"
    - "Microsoft.Extensions.Caching.Abstractions 9.0.3 — transitive upgrade for HybridCache 9.x"
    - "Microsoft.EntityFrameworkCore.Design 8.0.14 — EF tools design-time support in PricingService.API"
    - "Microsoft.EntityFrameworkCore.InMemory 8.0.14 — in-memory EF provider for unit tests"
  patterns:
    - "MarkupRulesEngine in Infrastructure layer (not Application) to avoid circular Application↔Infrastructure dependency"
    - "IDesignTimeDbContextFactory<PricingDbContext> for EF tooling independence from runtime DI"
    - "HybridCache.GetOrCreateAsync with ValueTask<T> factory — required by HybridCache 9.x API"
    - "IDistributedCache for booking tokens (not HybridCache) for precise TTL from offer.ExpiresAt"
    - "PricingServiceClient inner DTO in SearchController — no cross-service project reference (D-08)"
    - "In-process SlidingWindowLimiter for Phase 2; distributed upgrade planned Phase 7"
key_files:
  created:
    - src/services/PricingService/PricingService.Application/Rules/Models/MarkupType.cs
    - src/services/PricingService/PricingService.Application/Rules/Models/MarkupRule.cs
    - src/services/PricingService/PricingService.Application/Rules/Models/PricingContext.cs
    - src/services/PricingService/PricingService.Application/Rules/Models/PricedOffer.cs
    - src/services/PricingService/PricingService.Application/Rules/IPricingRulesEngine.cs
    - src/services/PricingService/PricingService.Infrastructure/Rules/MarkupRulesEngine.cs
    - src/services/PricingService/PricingService.Infrastructure/PricingDbContextFactory.cs
    - src/services/PricingService/PricingService.Infrastructure/Migrations/20260415000000_AddMarkupRules.cs
    - src/services/PricingService/PricingService.Infrastructure/Migrations/PricingDbContextModelSnapshot.cs
    - src/services/PricingService/PricingService.API/Controllers/PricingController.cs
    - src/services/SearchService/SearchService.Application/Cache/ISearchCacheService.cs
    - src/services/SearchService/SearchService.Application/Cache/SearchCacheService.cs
    - tests/TBE.Tests.Unit/PricingService/MarkupRulesEngineTests.cs
  modified:
    - src/services/PricingService/PricingService.Infrastructure/PricingDbContext.cs
    - src/services/PricingService/PricingService.Application/PricingService.Application.csproj
    - src/services/PricingService/PricingService.API/PricingService.API.csproj
    - src/services/PricingService/PricingService.API/Program.cs
    - src/services/SearchService/SearchService.Application/SearchService.Application.csproj
    - src/services/SearchService/SearchService.API/Controllers/SearchController.cs
    - src/services/SearchService/SearchService.API/Program.cs
    - src/services/SearchService/SearchService.API/SearchService.API.csproj
    - src/shared/TBE.Contracts/Inventory/Models/PriceBreakdown.cs
    - tests/TBE.Tests.Unit/SearchService/RedisCacheTests.cs
    - tests/TBE.Tests.Unit/TBE.Tests.Unit.csproj
decisions:
  - "MarkupRulesEngine placed in Infrastructure layer (not Application) — Application.csproj cannot reference Infrastructure.csproj (circular: Infrastructure already references Application); EF-dependent services belong in Infrastructure"
  - "EF migration scaffolded manually — dotnet-ef 10.x global tool incompatible with EF Core SqlServer 8.0.25 packages (Method get_LockReleaseBehavior missing); hand-crafted migration is functionally equivalent and avoids a premature package upgrade"
  - "IDesignTimeDbContextFactory added — allows EF tooling to create DbContext without startup project runtime resolution"
  - "HybridCache NuGet upgraded to 9.3.0 with matching Caching.Abstractions 9.0.3 and Logging.Abstractions 9.0.3 — 9.x HybridCache refuses to resolve against 8.x transitive dependencies"
  - "ISearchCacheService.GetOrSearchAsync factory parameter changed to ValueTask<T> — HybridCache 9.x GetOrCreateAsync requires ValueTask, not Task"
  - "Booking tokens stored in IDistributedCache (not HybridCache) — HybridCache does not support explicit per-entry TTL from external value (offer.ExpiresAt); IDistributedCache.SetAsync with AbsoluteExpirationRelativeToNow provides precise GDS-expiry-based TTL"
metrics:
  duration_minutes: 22
  completed_date: "2026-04-15"
  tasks_completed: 2
  tasks_total: 2
  files_created: 13
  files_modified: 11
---

# Phase 02 Plan 04: Pricing Markup Engine & HybridCache Pipeline Summary

**One-liner:** MarkupRulesEngine with Percentage/FixedAmount/MaxCap/carrier-specificity rules in Infrastructure layer; PricingController POST /pricing/apply; SearchCacheService HybridCache with Browse (10 min) and Selection (90 sec) TTLs; Redis booking token storage keyed by session UUID with offer.ExpiresAt TTL; in-process sliding-window rate-limit guard at 8 req/sec; 9 new unit tests green (36 total).

## What Was Built

**Task 1 — Pricing Service markup rules engine:**

`MarkupType` enum (Percentage/FixedAmount), `MarkupRule` entity (ProductType, AirlineCode, RouteOrigin, Channel, Value, MaxAmount, IsActive), `PricingContext` record (Channel, AgencyId, ProductType, CarrierCode, RouteOrigin), `PricedOffer` record (OfferId, NetFare, MarkupAmount, GrossSelling, Currency, AppliedRuleId, OriginalOffer). `IPricingRulesEngine` interface in Application layer.

`MarkupRulesEngine` in Infrastructure layer: loads active rules from DB filtered by ProductType and Channel, selects most-specific rule via `OrderByDescending(AirlineCode!=null?2:0 + RouteOrigin!=null?1:0)`, computes markup with MaxAmount cap, returns `PricedOffer` with correct GrossSelling. Returns passthrough (GrossSelling==NetFare, AppliedRuleId==null) when no rule matches.

`PricingDbContext` extended with `MarkupRules` DbSet and EF model configuration (indexes on ProductType/Channel/IsActive). `AddMarkupRules` migration scaffolded manually due to EF tools 10.x / EF Core packages 8.x incompatibility. `IDesignTimeDbContextFactory<PricingDbContext>` added for tooling. `PricingController` POST /pricing/apply processes `IReadOnlyList<UnifiedFlightOffer>` with parallel `Task.WhenAll`. `IPricingRulesEngine` registered as `AddScoped<>` in Program.cs.

5 unit tests: percentage markup, fixed markup, MaxAmount cap, no-rule passthrough, airline-specific carrier matching.

**Task 2 — HybridCache tiered TTL + Redis booking tokens + rate-limit guard:**

`ISearchCacheService` with `GetOrSearchAsync` (ValueTask factory, isSelection flag), `StoreBookingTokenAsync`, `GetBookingTokenAsync`. `SearchCacheService`: BrowseTtl (Expiration=10 min, LocalCacheExpiration=2 min), SelectionTtl (Expiration=90 sec, LocalCacheExpiration=30 sec) via `HybridCacheEntryOptions`. Booking tokens stored in `IDistributedCache` (not HybridCache) with key `booking-token:{sessionId}` and TTL from `offer.ExpiresAt`.

`SearchController` refactored: `[EnableRateLimiting("gds-rate-limit")]` on SearchFlights, `ISearchCacheService` injected, pricing-service HttpClient call inside factory, `PricedOfferDto` deserialization, `GrossSellingPrice`/`MarkupApplied=true` mapped back onto raw offer before cache write. New `SelectFlight` (POST /search/flights/select) and `GetBookingToken` (GET /search/flights/token/{sessionId}) endpoints.

`PriceBreakdown.cs` extended with `GrossSellingPrice (decimal?)` and `MarkupApplied (bool)`. SearchService Program.cs: `AddStackExchangeRedisCache`, `AddHybridCache`, `AddSingleton<ISearchCacheService>`, `AddRateLimiter` with sliding window `gds-rate-limit` (8 req/sec, 4 segments, QueueLimit=0), `app.UseRateLimiter()` before `app.MapControllers()`.

4 unit tests: factory called once on cache hit, booking token roundtrip, null for unknown session, empty sessionId throws ArgumentException.

## Commits

| Task | Hash | Message |
|------|------|---------|
| 1 | 2addbe1 | feat(02-04): implement PricingService markup rules engine and PricingController |
| 2 | 3452e27 | feat(02-04): HybridCache tiered TTL, Redis booking tokens, rate-limit guard, pricing wiring |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] MarkupRulesEngine circular dependency — moved to Infrastructure layer**
- **Found during:** Task 1 build
- **Issue:** Plan placed `MarkupRulesEngine` in `PricingService.Application` using `PricingDbContext` from `PricingService.Infrastructure`. Infrastructure already references Application (standard pattern), so adding Application → Infrastructure creates a circular dependency that the compiler rejects.
- **Fix:** Moved `MarkupRulesEngine` to `PricingService.Infrastructure/Rules/` namespace `TBE.PricingService.Infrastructure.Rules`. Interface `IPricingRulesEngine` stays in Application. This is the standard layered architecture: EF-dependent concrete implementations belong in Infrastructure.
- **Files modified:** `MarkupRulesEngine.cs` (created in Infrastructure, deleted from Application), `PricingService.Application.csproj` (EF Core package reference removed)
- **Commit:** 2addbe1

**2. [Rule 3 - Blocking] EF migration tools version incompatibility**
- **Found during:** Task 1 EF migration generation
- **Issue:** Global `dotnet-ef` tool was 8.0.11, upgraded to 10.0.6 during fix attempt, but EF Core SqlServer packages are 8.0.25 — EF tools 10.x requires `get_LockReleaseBehavior` method not present in EF Core SqlServer 8.0.25. Cannot downgrade global tool below current installed version.
- **Fix:** Scaffolded `20260415000000_AddMarkupRules.cs` and `PricingDbContextModelSnapshot.cs` by hand. Added `IDesignTimeDbContextFactory<PricingDbContext>` for future tooling use. Added `Microsoft.EntityFrameworkCore.Design` to PricingService.API.csproj.
- **Files modified:** Migration files (created manually), `PricingDbContextFactory.cs` (new), `PricingService.API.csproj`
- **Commit:** 2addbe1

**3. [Rule 1 - Bug] HybridCache 9.x requires ValueTask factory, not Task**
- **Found during:** Task 2 build
- **Issue:** `HybridCache.GetOrCreateAsync` in HybridCache 9.x requires `Func<CancellationToken, ValueTask<T>>` — the plan showed a `Task<T>` factory which does not compile.
- **Fix:** Changed `ISearchCacheService.GetOrSearchAsync` factory parameter to `Func<CancellationToken, ValueTask<IReadOnlyList<UnifiedFlightOffer>>>`. Updated `SearchCacheService` implementation and `RedisCacheTests` factory lambdas to use `ValueTask`.
- **Files modified:** `ISearchCacheService.cs`, `SearchCacheService.cs`, `RedisCacheTests.cs`
- **Commit:** 3452e27

**4. [Rule 3 - Blocking] HybridCache 9.x transitive package version conflicts**
- **Found during:** Task 2 build (restore step)
- **Issue:** `Microsoft.Extensions.Caching.Hybrid 9.3.0` requires `Microsoft.Extensions.Caching.Abstractions >= 9.0.3` and `Microsoft.Extensions.Logging.Abstractions >= 9.0.3`, but `SearchService.Application.csproj` pinned 8.x versions — NuGet NU1605 downgrade detection blocked restore.
- **Fix:** Upgraded `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.Caching.Abstractions` to `9.0.3` and `Microsoft.Extensions.Caching.Memory` in test project to `9.0.3` in `SearchService.Application.csproj` and `TBE.Tests.Unit.csproj`.
- **Files modified:** `SearchService.Application.csproj`, `TBE.Tests.Unit.csproj`
- **Commit:** 3452e27

**5. [Rule 1 - Bug] Duplicate file-scoped namespace in SearchController.cs**
- **Found during:** Task 2 SearchService.API build
- **Issue:** `SearchController.cs` had a second `namespace TBE.SearchService.API.Controllers;` declaration after the first file-scoped namespace — C# CS8954 error.
- **Fix:** Removed the duplicate `namespace` line from the `PricingServiceClient` inner class section. The class inherits the file-scoped namespace automatically.
- **Files modified:** `SearchController.cs`
- **Commit:** 3452e27

## Known Stubs

None — all stub test files from Plan 01 that this plan was responsible for have been replaced:

| File | Was stub for | Status |
|------|-------------|--------|
| `PricingService/MarkupRulesEngineTests.cs` | INV-07 (was `PricingEngineTests.cs` stub) | Replaced with 5 real tests |
| `SearchService/RedisCacheTests.cs` | INV-08, INV-09 | Replaced with 4 real tests |

Remaining stubs from earlier plans are out of scope for this plan.

## Threat Surface

All threat mitigations from the plan's threat register were applied:

| Threat ID | Mitigation Applied |
|-----------|-------------------|
| T-02-04-01 | Cache keys computed server-side from IATA-validated inputs: `search:flights:{origin}:{dest}:{date}:{adults}:{class}` — never includes raw user string |
| T-02-04-02 | Session ID is `Guid.NewGuid()` generated server-side; key pattern `booking-token:{guid}` not guessable; client receives UUID only |
| T-02-04-03 | `[EnableRateLimiting("gds-rate-limit")]` on `SearchFlights` endpoint; `AddSlidingWindowLimiter` at 8 req/sec; `OnRejected` returns HTTP 429 with `Retry-After: 1` header |
| T-02-04-04 | Accepted — markup applied server-side; raw net fare never returned to client (GrandTotal is pre-markup; GrossSellingPrice is post-markup) |
| T-02-04-05 | `MarkupRulesEngine` reads rules from DB only; `MaxAmount` cap prevents unbounded markup |

## Self-Check: PASSED

Files exist:
- src/services/PricingService/PricingService.Application/Rules/IPricingRulesEngine.cs: FOUND
- src/services/PricingService/PricingService.Application/Rules/Models/MarkupRule.cs: FOUND
- src/services/PricingService/PricingService.Infrastructure/Rules/MarkupRulesEngine.cs: FOUND
- src/services/PricingService/PricingService.Infrastructure/Migrations/20260415000000_AddMarkupRules.cs: FOUND
- src/services/PricingService/PricingService.API/Controllers/PricingController.cs: FOUND
- src/services/SearchService/SearchService.Application/Cache/ISearchCacheService.cs: FOUND
- src/services/SearchService/SearchService.Application/Cache/SearchCacheService.cs: FOUND
- src/shared/TBE.Contracts/Inventory/Models/PriceBreakdown.cs: FOUND
- tests/TBE.Tests.Unit/PricingService/MarkupRulesEngineTests.cs: FOUND
- tests/TBE.Tests.Unit/SearchService/RedisCacheTests.cs: FOUND

Commits exist:
- 2addbe1: FOUND
- 3452e27: FOUND

Tests: 36 passed (all Category=Unit), 0 failed.
Builds: PricingService.API 0 errors, SearchService.API 0 errors.
