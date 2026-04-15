---
phase: 02-inventory-layer-gds-integration
plan: "01"
subsystem: inventory-contracts-amadeus-adapter
tags:
  - inventory
  - contracts
  - amadeus
  - gds
  - refit
  - oauth2
dependency_graph:
  requires:
    - "01-04: TBE.Contracts project scaffold (Class1.cs stub replaced)"
  provides:
    - "IFlightAvailabilityProvider, IHotelAvailabilityProvider, ICarAvailabilityProvider interfaces"
    - "Unified canonical offer models (flight, hotel, car)"
    - "AmadeusFlightProvider — first working GDS call in the system"
    - "POST /flights/search HTTP endpoint on FlightConnectorService"
  affects:
    - "02-02: Second GDS adapter implements IFlightAvailabilityProvider"
    - "02-03: Hotelbeds implements IHotelAvailabilityProvider"
    - "02-04: SearchService fan-out calls FlightConnectorService via HTTP"
tech_stack:
  added:
    - "Refit 10.1.6 — HTTP client generation for Amadeus REST API"
    - "Refit.HttpClientFactory 10.1.6 — DI integration for Refit clients"
    - "Microsoft.Extensions.Http.Resilience 10.4.0 — standard resilience pipeline on Amadeus HTTP client"
    - "xunit 2.9.3, NSubstitute 5.3.0, FluentAssertions 8.2.0 — unit test stack"
  patterns:
    - "DelegatingHandler for OAuth2 token injection (AmadeusAuthHandler)"
    - "Keyed DI: AddKeyedSingleton<IFlightAvailabilityProvider, AmadeusFlightProvider>(\"amadeus\")"
    - "SemaphoreSlim double-check locking for token cache refresh"
    - "YQ/YR separation: carrier surcharges split from government taxes at adapter boundary"
key_files:
  created:
    - src/shared/TBE.Contracts/Inventory/IFlightAvailabilityProvider.cs
    - src/shared/TBE.Contracts/Inventory/IHotelAvailabilityProvider.cs
    - src/shared/TBE.Contracts/Inventory/ICarAvailabilityProvider.cs
    - src/shared/TBE.Contracts/Inventory/Models/PriceBreakdown.cs
    - src/shared/TBE.Contracts/Inventory/Models/UnifiedFlightOffer.cs
    - src/shared/TBE.Contracts/Inventory/Models/UnifiedHotelOffer.cs
    - src/shared/TBE.Contracts/Inventory/Models/UnifiedCarOffer.cs
    - src/shared/TBE.Contracts/Inventory/Models/FlightSearchRequest.cs
    - src/shared/TBE.Contracts/Inventory/Models/FlightSegment.cs
    - src/shared/TBE.Contracts/Inventory/Models/FareRule.cs
    - src/shared/TBE.Contracts/Inventory/Models/HotelSearchRequest.cs
    - src/shared/TBE.Contracts/Inventory/Models/CarSearchRequest.cs
    - src/services/FlightConnectorService/FlightConnectorService.Application/Amadeus/AmadeusOptions.cs
    - src/services/FlightConnectorService/FlightConnectorService.Application/Amadeus/AmadeusTokenResponse.cs
    - src/services/FlightConnectorService/FlightConnectorService.Application/Amadeus/AmadeusAuthHandler.cs
    - src/services/FlightConnectorService/FlightConnectorService.Application/Amadeus/AmadeusFlightProvider.cs
    - src/services/FlightConnectorService/FlightConnectorService.Application/Amadeus/IAmadeusFlightApi.cs
    - src/services/FlightConnectorService/FlightConnectorService.Application/Amadeus/Models/AmadeusFlightOffersResponse.cs
    - src/services/FlightConnectorService/FlightConnectorService.API/Controllers/FlightSearchController.cs
    - tests/TBE.Tests.Unit/TBE.Tests.Unit.csproj
    - tests/TBE.Tests.Unit/FlightConnectorService/AmadeusProviderTests.cs
    - tests/TBE.Tests.Unit/FlightConnectorService/InventoryConnectorTests.cs
    - tests/TBE.Tests.Unit/FlightConnectorService/FanOutTests.cs
    - tests/TBE.Tests.Unit/FlightConnectorService/AmadeusAdapterTests.cs
    - tests/TBE.Tests.Unit/FlightConnectorService/SecondGDSAdapterTests.cs
    - tests/TBE.Tests.Unit/HotelConnectorService/HotelbedsAdapterTests.cs
    - tests/TBE.Tests.Unit/HotelConnectorService/CarHireAdapterTests.cs
    - tests/TBE.Tests.Unit/PricingService/PricingEngineTests.cs
    - tests/TBE.Tests.Unit/SearchService/RedisCacheTests.cs
  modified:
    - src/services/FlightConnectorService/FlightConnectorService.API/Program.cs
    - src/services/FlightConnectorService/FlightConnectorService.API/FlightConnectorService.API.csproj
    - src/services/FlightConnectorService/FlightConnectorService.Application/FlightConnectorService.Application.csproj
    - TBE.slnx
decisions:
  - "MapOffer made public (not internal) to serve as test seam — pure mapping function with no side effects; InternalsVisibleTo avoided to keep test boundary clean"
  - "Refit.HttpClientFactory added to API.csproj (not just Application.csproj) because Program.cs calls AddRefitClient directly — extension method must be in scope"
  - "YQ/YR comment removed from PriceBreakdown.cs to keep separation logic entirely in the adapter layer, satisfying grep acceptance criterion"
metrics:
  duration_minutes: 9
  completed_date: "2026-04-15"
  tasks_completed: 3
  tasks_total: 3
  files_created: 29
  files_modified: 4
---

# Phase 02 Plan 01: Inventory Contracts & Amadeus Adapter Summary

**One-liner:** Three provider interfaces + full canonical offer models in TBE.Contracts; Amadeus OAuth2 adapter with YQ/YR-correct tax mapping; FlightConnectorService HTTP endpoint; 14 unit tests green.

## What Was Built

**Task 0 — Nyquist stub tests:** Created `TBE.Tests.Unit` project scaffold (xunit, NSubstitute, FluentAssertions) and 8 stub test files covering INV-01 through INV-09 requirements. Added project to `TBE.slnx`.

**Task 1 — Canonical inventory contracts:** Defined `IFlightAvailabilityProvider`, `IHotelAvailabilityProvider`, and `ICarAvailabilityProvider` in `TBE.Contracts.Inventory`. Created all canonical models: `UnifiedFlightOffer`, `UnifiedHotelOffer`, `UnifiedCarOffer`, `PriceBreakdown` (with separate `Surcharges` and `Taxes` lists), `FlightSegment`, `FareRule`, `FlightSearchRequest`, `HotelSearchRequest`, `CarSearchRequest`. TBE.Contracts has zero external NuGet dependencies.

**Task 2 — Amadeus adapter:** Implemented `AmadeusAuthHandler` (OAuth2 client_credentials with SemaphoreSlim double-check cache, token never logged), `IAmadeusFlightApi` (Refit interface, 11 `[AliasAs]` params), `AmadeusFlightProvider` (maps YQ/YR taxes to `Surcharges`, all others to `Taxes`). Added `FlightSearchController` with IATA regex validation. Registered `AmadeusFlightProvider` as keyed singleton `"amadeus"`. All 14 unit tests pass.

## Commits

| Task | Hash | Message |
|------|------|---------|
| 0 | cae5475 | chore(02-01): add Nyquist stub test files and test project scaffold |
| 1 | 231e4c2 | feat(02-01): define canonical inventory contracts in TBE.Contracts |
| 2 | 5763383 | feat(02-01): implement Amadeus OAuth2 adapter and FlightConnector endpoint |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] AddRefitClient not found in API project**
- **Found during:** Task 2 build
- **Issue:** `Program.cs` calls `AddRefitClient<>()` but `FlightConnectorService.API.csproj` did not reference `Refit.HttpClientFactory` — the extension method was only in the Application project's transitive graph, not the API's direct compile scope.
- **Fix:** Added `Refit.HttpClientFactory 10.1.6` package reference to `FlightConnectorService.API.csproj`; added `using Refit;` to `Program.cs`.
- **Files modified:** `FlightConnectorService.API.csproj`, `Program.cs`
- **Commit:** 5763383

**2. [Rule 1 - Bug] MapOffer inaccessible from test project**
- **Found during:** Task 2 test run
- **Issue:** `MapOffer` was declared `internal static` but the test project is a separate assembly with no `InternalsVisibleTo` attribute — tests failed with CS0117.
- **Fix:** Changed `MapOffer` to `public static`. The method is a pure mapping function with no side effects; making it public is appropriate and avoids coupling the test project to assembly internals via `InternalsVisibleTo`.
- **Files modified:** `AmadeusFlightProvider.cs`
- **Commit:** 5763383

**3. [Rule 2 - Missing critical] YQ/YR comment in PriceBreakdown.cs violated acceptance criterion**
- **Found during:** Task 1 acceptance criteria verification
- **Issue:** Plan acceptance criterion states `grep -rn "YQ\|YR" src/shared/TBE.Contracts/` must return zero matches (separation logic must be in the adapter only). The original comment `// YQ, YR codes only` on `Surcharges` triggered the grep.
- **Fix:** Replaced comment with `// carrier surcharges only` to keep the semantic intent without referencing specific tax codes in the model.
- **Files modified:** `PriceBreakdown.cs`
- **Commit:** 231e4c2

## Known Stubs

The following test files contain `Assert.True(true)` stub bodies — intentional per plan design. Full implementations will replace them in subsequent plans:

| File | Stub for | Replaced in |
|------|----------|-------------|
| `AmadeusAdapterTests.cs` | INV-01, INV-02 (duplicates) | Already superseded by `AmadeusProviderTests.cs` real tests |
| `SecondGDSAdapterTests.cs` | INV-03 | Plan 02-02 |
| `HotelbedsAdapterTests.cs` | INV-04 | Plan 02-03 |
| `CarHireAdapterTests.cs` | INV-05 | Plan 02-03 |
| `FanOutTests.cs` | INV-06 | Plan 02-04 |
| `PricingEngineTests.cs` | INV-07 | Plan 02-04 |
| `RedisCacheTests.cs` | INV-08, INV-09 | Plan 02-04 |

These stubs do not block this plan's goal — the canonical contracts and Amadeus adapter are fully implemented and tested.

## Threat Surface

All threat mitigations from the plan's threat register were applied:

| Threat ID | Mitigation Applied |
|-----------|-------------------|
| T-02-01-01 | Bearer token held in `_cachedToken` private field only; `LogInformation` logs only expiry timestamp, never token value |
| T-02-01-02 | `AmadeusOptions` bound from `builder.Configuration.GetSection("Amadeus")` (env vars); `appsettings.json` contains no `Amadeus:ApiKey` key |
| T-02-01-03 | `FlightSearchController` validates Origin and Destination with `^[A-Z]{3}$` regex; returns 400 on invalid input |
| T-02-01-04 | Accepted — 30-second pre-expiry window with SemaphoreSlim double-check is sufficient |

## Self-Check: PASSED

Files exist:
- src/shared/TBE.Contracts/Inventory/IFlightAvailabilityProvider.cs: FOUND
- src/shared/TBE.Contracts/Inventory/Models/PriceBreakdown.cs: FOUND
- src/services/FlightConnectorService/FlightConnectorService.Application/Amadeus/AmadeusFlightProvider.cs: FOUND
- src/services/FlightConnectorService/FlightConnectorService.Application/Amadeus/AmadeusAuthHandler.cs: FOUND
- src/services/FlightConnectorService/FlightConnectorService.API/Controllers/FlightSearchController.cs: FOUND
- tests/TBE.Tests.Unit/TBE.Tests.Unit.csproj: FOUND

Commits exist:
- cae5475: FOUND
- 231e4c2: FOUND
- 5763383: FOUND

Tests: 14 passed, 0 failed.
Builds: TBE.Contracts 0 errors, FlightConnectorService.API 0 errors.
