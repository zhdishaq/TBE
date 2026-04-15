---
phase: 02-inventory-layer-gds-integration
plan: "02"
subsystem: sabre-adapter-fanout-orchestrator
tags:
  - inventory
  - sabre
  - gds
  - fan-out
  - orchestrator
  - search-service
dependency_graph:
  requires:
    - "02-01: IFlightAvailabilityProvider, UnifiedFlightOffer, AmadeusFlightProvider, TBE.Tests.Unit scaffold"
  provides:
    - "SabreFlightProvider — second GDS adapter implementing IFlightAvailabilityProvider"
    - "FlightSearchOrchestrator — Task.WhenAll fan-out with per-provider graceful degradation"
    - "FlightOfferDeduplicator — DistinctBy(SourceRef) + OrderBy(GrandTotal)"
    - "SearchController — calls FlightConnectorService via HTTP (D-08 compliant)"
  affects:
    - "02-03: HotelbedsAdapter implements IHotelAvailabilityProvider"
    - "02-04: SearchService Redis caching wraps FlightSearchOrchestrator"
tech_stack:
  added:
    - "Microsoft.Extensions.Logging.Abstractions 8.0.0 — added to SearchService.Application (ILogger<T> dependency)"
  patterns:
    - "Task.WhenAll with per-provider SearchSafeAsync try/catch — bare WhenAll avoided"
    - "FlightOfferDeduplicator static class: DistinctBy(SourceRef).OrderBy(GrandTotal)"
    - "Named HttpClient 'flight-connector' in SearchService — no cross-service project reference (D-08)"
    - "IEnumerable<IFlightAvailabilityProvider> registered as singleton from keyed services — controller fan-out"
key_files:
  created:
    - src/services/FlightConnectorService/FlightConnectorService.Application/Sabre/SabreOptions.cs
    - src/services/FlightConnectorService/FlightConnectorService.Application/Sabre/SabreTokenResponse.cs
    - src/services/FlightConnectorService/FlightConnectorService.Application/Sabre/SabreAuthHandler.cs
    - src/services/FlightConnectorService/FlightConnectorService.Application/Sabre/SabreFlightProvider.cs
    - src/services/FlightConnectorService/FlightConnectorService.Application/Sabre/ISabreFlightApi.cs
    - src/services/FlightConnectorService/FlightConnectorService.Application/Sabre/Models/SabreBfmResponse.cs
    - src/services/FlightConnectorService/FlightConnectorService.Application/Sabre/Models/SabreBfmRequest.cs
    - src/services/SearchService/SearchService.Application/FlightSearch/IFlightSearchOrchestrator.cs
    - src/services/SearchService/SearchService.Application/FlightSearch/FlightSearchOrchestrator.cs
    - src/services/SearchService/SearchService.Application/FlightSearch/FlightOfferDeduplicator.cs
    - src/services/SearchService/SearchService.API/Controllers/SearchController.cs
    - tests/TBE.Tests.Unit/FlightConnectorService/SabreProviderTests.cs
    - tests/TBE.Tests.Unit/SearchService/FanOutTests.cs
  modified:
    - src/services/FlightConnectorService/FlightConnectorService.API/Program.cs
    - src/services/FlightConnectorService/FlightConnectorService.API/Controllers/FlightSearchController.cs
    - src/services/SearchService/SearchService.Application/SearchService.Application.csproj
    - src/services/SearchService/SearchService.API/Program.cs
    - tests/TBE.Tests.Unit/TBE.Tests.Unit.csproj
  deleted:
    - src/services/SearchService/SearchService.Application/Class1.cs
decisions:
  - "MapItinerary made public (not internal) to serve as test seam — same rationale as AmadeusFlightProvider.MapOffer in Plan 01"
  - "IEnumerable<IFlightAvailabilityProvider> registered as singleton from keyed services in Program.cs — avoids keyed service injection in controller; controller receives the full provider list and filters by source name"
  - "Microsoft.Extensions.Logging.Abstractions added to SearchService.Application.csproj — required for ILogger<FlightSearchOrchestrator>; API project's transitive graph does not satisfy direct Application compilation"
  - "FlightSearchController updated from single [FromKeyedServices] injection to IEnumerable<IFlightAvailabilityProvider> — enables optional ?source= filter for per-GDS routing from SearchService"
metrics:
  duration_minutes: 12
  completed_date: "2026-04-15"
  tasks_completed: 2
  tasks_total: 2
  files_created: 13
  files_modified: 5
---

# Phase 02 Plan 02: Sabre Adapter & Fan-Out Orchestrator Summary

**One-liner:** Sabre BFM REST adapter with OAuth2 token handler and YQ/YR tax separation; Task.WhenAll fan-out orchestrator in SearchService with per-provider graceful degradation and DistinctBy deduplication; SearchController calling FlightConnectorService via named HttpClient; 7 new unit tests green.

## What Was Built

**Task 1 — Sabre BFM REST adapter:**
Implemented `SabreAuthHandler` (OAuth2 client_credentials, SemaphoreSlim double-check, token never logged), `ISabreFlightApi` (Refit POST /v4.3.0/shop/flights/reqs), `SabreFlightProvider` (implements `IFlightAvailabilityProvider`, YQ/YR carrier surcharge separation identical to Amadeus, `MapItinerary` public for test seam). Typed DTOs in `SabreBfmResponse.cs` and `SabreBfmRequest.cs` cover the grouped itinerary response structure. `SabreFlightProvider` registered as keyed singleton `"sabre"` alongside existing `"amadeus"` in `Program.cs`. `FlightSearchController` refactored to accept `IEnumerable<IFlightAvailabilityProvider>` with optional `?source=` query filter; all providers also exposed as singleton `IEnumerable` for the controller.

**Task 2 — Fan-out orchestrator in SearchService:**
`FlightSearchOrchestrator` fans out to all registered `IFlightAvailabilityProvider` instances via `Task.WhenAll`; each provider wrapped in `SearchSafeAsync` try/catch that returns `[]` on failure and logs a warning — bare `Task.WhenAll` is explicitly avoided. `FlightOfferDeduplicator` applies `DistinctBy(o => o.SourceRef).OrderBy(o => o.Price.GrandTotal)`. `SearchController` calls FlightConnectorService via named `HttpClient("flight-connector")` — no `ProjectReference` to FlightConnectorService (D-08 compliance). Deleted `Class1.cs` stub. Added `Microsoft.Extensions.Logging.Abstractions` to `SearchService.Application.csproj`.

## Commits

| Task | Hash | Message |
|------|------|---------|
| 1 | 3dfcdb3 | feat(02-02): implement Sabre BFM REST adapter (SabreFlightProvider) |
| 2 | 7db84a7 | feat(02-02): fan-out orchestrator in SearchService with graceful degradation |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Microsoft.Extensions.Logging.Abstractions missing from SearchService.Application**
- **Found during:** Task 2 build
- **Issue:** `FlightSearchOrchestrator` uses `ILogger<T>` from `Microsoft.Extensions.Logging`. `SearchService.Application.csproj` had no reference to this package, and the API project's transitive graph does not satisfy direct Application project compilation.
- **Fix:** Added `Microsoft.Extensions.Logging.Abstractions 8.0.0` NuGet reference to `SearchService.Application.csproj`.
- **Files modified:** `SearchService.Application.csproj`
- **Commit:** 7db84a7

### Planned Changes Applied as Designed

**FlightSearchController refactored from single keyed injection to IEnumerable:**
The plan specified updating the controller to accept an optional `?source=` parameter and fan-out across all providers. The existing controller injected only `[FromKeyedServices("amadeus")]`. The controller was rewritten to inject `IEnumerable<IFlightAvailabilityProvider>` (registered as singleton from keyed services) and filter by source name when the parameter is provided.

## Known Stubs

The following test files from Plan 01 still contain `Assert.True(true)` stub bodies. The new `FanOutTests.cs` in `SearchService/` replaces the `FanOutTests.cs` stub that was in `FlightConnectorService/`. That stub remains as a passing placeholder but is now superseded:

| File | Stub for | Replaced in |
|------|----------|-------------|
| `FlightConnectorService/FanOutTests.cs` | INV06-stub | Superseded by `SearchService/FanOutTests.cs` (this plan) |
| `HotelbedsAdapterTests.cs` | INV-04 | Plan 02-03 |
| `CarHireAdapterTests.cs` | INV-05 | Plan 02-03 |
| `PricingEngineTests.cs` | INV-07 | Plan 02-04 |
| `RedisCacheTests.cs` | INV-08, INV-09 | Plan 02-04 |

These stubs do not block this plan's goal — both GDS adapters and the fan-out orchestrator are fully implemented and tested.

## Threat Surface

All threat mitigations from the plan's threat register were applied:

| Threat ID | Mitigation Applied |
|-----------|-------------------|
| T-02-02-01 | `_cachedToken` held in private field; `LogInformation` logs only expiry timestamp, never token value |
| T-02-02-02 | `SabreOptions` bound from `builder.Configuration.GetSection("Sabre")`; env vars `SABRE__CLIENTID`/`SABRE__CLIENTSECRET` override |
| T-02-02-03 | Each provider call in `SearchSafeAsync` wrapped with try/catch returning `[]`; `CancellationToken` passed through |
| T-02-02-04 | Accepted — `DistinctBy` is stable (first occurrence kept); SourceRef collision probability negligible |

## Self-Check: PASSED

Files exist:
- src/services/FlightConnectorService/FlightConnectorService.Application/Sabre/SabreFlightProvider.cs: FOUND
- src/services/FlightConnectorService/FlightConnectorService.Application/Sabre/SabreAuthHandler.cs: FOUND
- src/services/SearchService/SearchService.Application/FlightSearch/FlightSearchOrchestrator.cs: FOUND
- src/services/SearchService/SearchService.Application/FlightSearch/FlightOfferDeduplicator.cs: FOUND
- src/services/SearchService/SearchService.API/Controllers/SearchController.cs: FOUND
- tests/TBE.Tests.Unit/FlightConnectorService/SabreProviderTests.cs: FOUND
- tests/TBE.Tests.Unit/SearchService/FanOutTests.cs: FOUND

Commits exist:
- 3dfcdb3: FOUND
- 7db84a7: FOUND

Tests: 21 passed (all Category=Unit), 0 failed.
Builds: FlightConnectorService.API 0 errors, SearchService.API 0 errors.
