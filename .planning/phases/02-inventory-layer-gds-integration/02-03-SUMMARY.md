---
phase: 02-inventory-layer-gds-integration
plan: "03"
subsystem: hotel-car-adapters
tags:
  - inventory
  - hotelbeds
  - hmac
  - amadeus
  - car-hire
  - refit
dependency_graph:
  requires:
    - "02-01: IHotelAvailabilityProvider, ICarAvailabilityProvider interfaces in TBE.Contracts"
    - "02-01: UnifiedHotelOffer, UnifiedCarOffer, PriceBreakdown canonical models"
  provides:
    - "HotelbedsProvider — Hotelbeds REST adapter implementing IHotelAvailabilityProvider"
    - "HotelbedsHmacHandler — HMAC-SHA256 DelegatingHandler for all Hotelbeds requests"
    - "AmadeusCarProvider — Amadeus Transfer Search adapter implementing ICarAvailabilityProvider"
    - "POST /hotels/search and POST /cars/search endpoints on HotelConnectorService.API"
  affects:
    - "02-04: SearchService fan-out can now call HotelConnectorService for hotel and car offers"
tech_stack:
  added:
    - "Refit 10.1.6 — HTTP client generation for Hotelbeds and Amadeus Transfer APIs"
    - "Refit.HttpClientFactory 10.1.6 — DI integration (added to both Application and API projects)"
    - "Microsoft.Extensions.Http.Resilience 10.4.0 — standard resilience pipeline on both HTTP clients"
  patterns:
    - "HotelbedsHmacHandler: SHA256(apiKey+sharedSecret+ToUnixTimeSeconds()) via System.Security.Cryptography.SHA256.HashData"
    - "AmadeusCarAuthHandler: OAuth2 client_credentials with SemaphoreSlim double-check locking (identical to AmadeusAuthHandler)"
    - "AddSingleton<IHotelAvailabilityProvider, HotelbedsProvider> and AddSingleton<ICarAvailabilityProvider, AmadeusCarProvider>"
    - "Both controllers validate IATA codes with ^[A-Z]{3}$ regex before calling providers"
key_files:
  created:
    - src/services/HotelConnectorService/HotelConnectorService.Application/Hotelbeds/HotelbedsOptions.cs
    - src/services/HotelConnectorService/HotelConnectorService.Application/Hotelbeds/HotelbedsHmacHandler.cs
    - src/services/HotelConnectorService/HotelConnectorService.Application/Hotelbeds/IHotelbedsApi.cs
    - src/services/HotelConnectorService/HotelConnectorService.Application/Hotelbeds/HotelbedsProvider.cs
    - src/services/HotelConnectorService/HotelConnectorService.Application/Hotelbeds/Models/HotelbedsAvailabilityRequest.cs
    - src/services/HotelConnectorService/HotelConnectorService.Application/Hotelbeds/Models/HotelbedsAvailabilityResponse.cs
    - src/services/HotelConnectorService/HotelConnectorService.Application/Car/AmadeusCarOptions.cs
    - src/services/HotelConnectorService/HotelConnectorService.Application/Car/AmadeusCarAuthHandler.cs
    - src/services/HotelConnectorService/HotelConnectorService.Application/Car/IAmadeusTransferApi.cs
    - src/services/HotelConnectorService/HotelConnectorService.Application/Car/AmadeusCarProvider.cs
    - src/services/HotelConnectorService/HotelConnectorService.Application/Car/Models/AmadeusTransferOffersResponse.cs
    - src/services/HotelConnectorService/HotelConnectorService.API/Controllers/HotelSearchController.cs
    - src/services/HotelConnectorService/HotelConnectorService.API/Controllers/CarSearchController.cs
    - tests/TBE.Tests.Unit/HotelConnectorService/HotelbedsHmacTests.cs
    - tests/TBE.Tests.Unit/HotelConnectorService/HotelbedsProviderTests.cs
    - tests/TBE.Tests.Unit/HotelConnectorService/CarProviderTests.cs
  modified:
    - src/services/HotelConnectorService/HotelConnectorService.Application/HotelConnectorService.Application.csproj
    - src/services/HotelConnectorService/HotelConnectorService.API/HotelConnectorService.API.csproj
    - src/services/HotelConnectorService/HotelConnectorService.API/Program.cs
    - tests/TBE.Tests.Unit/TBE.Tests.Unit.csproj
decisions:
  - "MapOffer made public (not internal) on both HotelbedsProvider and AmadeusCarProvider to serve as test seam — same rationale as AmadeusFlightProvider in Plan 01"
  - "AmadeusCarAuthHandler is a separate class from AmadeusAuthHandler despite identical logic — avoids coupling HotelConnectorService to FlightConnectorService internals; each service owns its own auth lifecycle"
  - "Car adapter placed in HotelConnectorService (not a separate CarConnectorService) because Phase 1 only scaffolded FlightConnectorService and HotelConnectorService; car is a lighter product sharing Hotel's scope"
  - "Explicit Microsoft.Extensions.Options version pin removed from Application.csproj — caused NU1605 downgrade conflict when Refit.HttpClientFactory 10.1.6 transitively required Options 9.0.3"
metrics:
  duration_minutes: 6
  completed_date: "2026-04-15"
  tasks_completed: 2
  tasks_total: 2
  files_created: 16
  files_modified: 4
---

# Phase 02 Plan 03: Hotelbeds Hotel Adapter & Amadeus Car Adapter Summary

**One-liner:** Hotelbeds REST adapter with HMAC-SHA256 auth (ToUnixTimeSeconds, SharedSecret never logged) and Amadeus Transfer Search car adapter; HotelConnectorService.API exposes POST /hotels/search and POST /cars/search; 8 unit tests green.

## What Was Built

**Task 1 — Hotelbeds HMAC adapter (HotelbedsProvider):**

- `HotelbedsHmacHandler`: DelegatingHandler computing `SHA256(apiKey + sharedSecret + ToUnixTimeSeconds())` hex-lowercase via `System.Security.Cryptography.SHA256.HashData`. SharedSecret is never passed to any logger. Docker clock-skew pitfall documented in code.
- `HotelbedsProvider`: implements `IHotelAvailabilityProvider`; maps Hotelbeds availability response to `UnifiedHotelOffer` with `PropertyName`, `RoomType`, `CancellationPolicy` (builds description from `CancellationPolicies` list; falls back to "Non-refundable" when list is empty), and `PriceBreakdown` (Hotelbeds net rate is all-in; `Surcharges` and `Taxes` are empty lists).
- `IHotelbedsApi`: Refit `[Post("/hotels")]` interface with `Accept: application/json` and `Content-Type: application/json` headers.
- `HotelbedsAvailabilityRequest/Response` models: full JSON-mapped model tree for stay/occupancy/destination/hotel/room/rate/cancellation-policy.
- `HotelSearchController`: `POST /hotels/search`; validates `DestinationCode` against `^[A-Z]{3}$`; validates `CheckOut > CheckIn`.
- `TBE.Tests.Unit`: 3 HMAC tests + 2 provider mapping tests = 5 tests passing.

**Task 2 — Amadeus Transfer Search car adapter (AmadeusCarProvider):**

- `AmadeusCarProvider`: implements `ICarAvailabilityProvider`; `Name = "amadeus-transfers"`; calls `GET /v1/shopping/availability/transfer-offers`; maps `AmadeusTransferOffer` to `UnifiedCarOffer` with `VehicleCategory`, `VehicleDescription`, `SupplierName`, and `PriceBreakdown` (base from `quotation.base`, taxes mapped to `PriceComponent("TAX", amount)` list, `GrandTotal` verified by test).
- `AmadeusCarAuthHandler`: identical OAuth2 `client_credentials` pattern with SemaphoreSlim double-check locking; token never logged; separate class from `AmadeusAuthHandler` in FlightConnectorService to avoid cross-service coupling.
- `IAmadeusTransferApi`: Refit `[Get("/shopping/availability/transfer-offers")]` with 5 `[AliasAs]` query parameters.
- `CarSearchController`: `POST /cars/search`; validates both `PickupLocationCode` and `DropoffLocationCode` against `^[A-Z]{3}$`.
- No Duffel dependency anywhere in `src/services/HotelConnectorService/`.
- `TBE.Tests.Unit`: 3 car tests passing (implements ICarAvailabilityProvider, Source field, GrandTotal).

## Commits

| Task | Hash | Message |
|------|------|---------|
| 1 | 8f02120 | feat(02-03): implement Hotelbeds HMAC adapter and HotelConnectorService endpoints |
| 2 | 34479ca | feat(02-03): implement Amadeus Transfer Search car adapter (AmadeusCarProvider) |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Microsoft.Extensions.Options version downgrade conflict**
- **Found during:** Task 1 — first `dotnet test` run after adding packages
- **Issue:** Application.csproj pinned `Microsoft.Extensions.Options 8.0.2` explicitly, but `Refit.HttpClientFactory 10.1.6` transitively requires `Microsoft.Extensions.Http 9.0.3` which requires `Options >= 9.0.3`. NuGet raised NU1605 (Warning As Error) blocking restore.
- **Fix:** Removed the explicit `Microsoft.Extensions.Options` PackageReference from Application.csproj. Refit's transitive graph resolves to 9.0.3 automatically.
- **Files modified:** `HotelConnectorService.Application.csproj`
- **Commit:** 8f02120

**2. [Rule 1 - Bug] MapOffer declared internal — inaccessible from test project**
- **Found during:** Task 1 implementation (applying same lesson from Plan 01 deviation)
- **Issue:** Plan code had `internal static UnifiedHotelOffer MapOffer(...)` on HotelbedsProvider and `internal static UnifiedCarOffer MapOffer(...)` on AmadeusCarProvider. Test project is a separate assembly; `CS0117` would occur at compile time.
- **Fix:** Changed both `MapOffer` methods to `public static` — same rationale as `AmadeusFlightProvider.MapOffer` in Plan 01. Pure mapping function, no side effects, appropriate as public test seam.
- **Files modified:** `HotelbedsProvider.cs`, `AmadeusCarProvider.cs`
- **Commit:** 8f02120, 34479ca

## Known Stubs

The pre-existing stub test files from Plan 01 (`HotelbedsAdapterTests.cs`, `CarHireAdapterTests.cs`) still exist with `Assert.True(true)` bodies. They are superseded by the real tests created in this plan (`HotelbedsHmacTests.cs`, `HotelbedsProviderTests.cs`, `CarProviderTests.cs`) but not deleted — deletion is not required and they continue to pass harmlessly.

## Threat Surface

All threat mitigations from the plan's threat register were applied:

| Threat ID | Mitigation Applied |
|-----------|-------------------|
| T-02-03-01 | `HotelbedsOptions` bound from `builder.Configuration.GetSection("Hotelbeds")` (env vars); no Hotelbeds credentials in `appsettings.json`; SharedSecret never referenced in any log call in HotelbedsHmacHandler |
| T-02-03-02 | `DateTimeOffset.UtcNow.ToUnixTimeSeconds()` used in HotelbedsHmacHandler; comment in code explicitly warns against DateTime.Now / ticks / milliseconds (Docker clock-skew Pitfall 5) |
| T-02-03-03 | `HotelSearchController` validates `DestinationCode` against `^[A-Z]{3}$` before calling provider; returns 400 on invalid input |
| T-02-03-04 | Accepted — SharedSecret held in `HotelbedsOptions` object (in-process only); not passed to any external system |

## Self-Check: PASSED

Files exist:
- src/services/HotelConnectorService/HotelConnectorService.Application/Hotelbeds/HotelbedsHmacHandler.cs: FOUND
- src/services/HotelConnectorService/HotelConnectorService.Application/Hotelbeds/HotelbedsProvider.cs: FOUND
- src/services/HotelConnectorService/HotelConnectorService.Application/Car/AmadeusCarProvider.cs: FOUND
- src/services/HotelConnectorService/HotelConnectorService.Application/Car/AmadeusCarAuthHandler.cs: FOUND
- src/services/HotelConnectorService/HotelConnectorService.API/Controllers/HotelSearchController.cs: FOUND
- src/services/HotelConnectorService/HotelConnectorService.API/Controllers/CarSearchController.cs: FOUND
- tests/TBE.Tests.Unit/HotelConnectorService/HotelbedsHmacTests.cs: FOUND
- tests/TBE.Tests.Unit/HotelConnectorService/HotelbedsProviderTests.cs: FOUND
- tests/TBE.Tests.Unit/HotelConnectorService/CarProviderTests.cs: FOUND

Commits exist:
- 8f02120: FOUND
- 34479ca: FOUND

Tests: 8 passed, 0 failed (HotelbedsHmac=3, HotelbedsProvider=2, CarProvider=3).
Build: HotelConnectorService.API 0 errors, 0 warnings.
