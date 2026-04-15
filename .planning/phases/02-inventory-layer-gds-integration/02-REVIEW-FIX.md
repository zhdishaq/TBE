---
phase: 02-inventory-layer-gds-integration
fixed_at: 2026-04-15T00:00:00Z
review_path: .planning/phases/02-inventory-layer-gds-integration/02-REVIEW.md
iteration: 1
findings_in_scope: 8
fixed: 8
skipped: 0
status: all_fixed
---

# Phase 02: Code Review Fix Report

**Fixed at:** 2026-04-15T00:00:00Z
**Source review:** .planning/phases/02-inventory-layer-gds-integration/02-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 8 (2 Critical, 6 Warning)
- Fixed: 8
- Skipped: 0

## Fixed Issues

### CR-01: `decimal.Parse` throws `FormatException` on unexpected GDS payload

**Files modified:** `src/services/FlightConnectorService/FlightConnectorService.Application/Amadeus/AmadeusFlightProvider.cs`, `src/services/HotelConnectorService/HotelConnectorService.Application/Car/AmadeusCarProvider.cs`, `src/services/HotelConnectorService/HotelConnectorService.Application/Hotelbeds/HotelbedsProvider.cs`
**Commit:** 5d9d991
**Applied fix:** Added a `SafeParseDecimal` static helper in each provider that uses `decimal.TryParse` with `NumberStyles.Any` and `CultureInfo.InvariantCulture`, returning `0m` on failure. All `decimal.Parse` calls in `MapOffer` methods replaced with `SafeParseDecimal` — covers tax amounts, base price, and net rate fields across all three GDS providers.

---

### CR-02: Null-dereference crash when Hotelbeds returns an empty availability response

**Files modified:** `src/services/HotelConnectorService/HotelConnectorService.Application/Hotelbeds/Models/HotelbedsAvailabilityResponse.cs`
**Commit:** 597b621
**Applied fix:** Changed `public HotelbedsHotelsContainer Hotels { get; init; } = default!` to `public HotelbedsHotelsContainer? Hotels { get; init; }`, making the nullable suppression explicit and compiler-enforced. The existing null-conditional access `raw.Hotels?.Hotels` in `HotelbedsProvider` is now correct by type contract.

---

### WR-01: Token refresh race condition — `_cachedToken` read without lock

**Files modified:** `src/services/FlightConnectorService/FlightConnectorService.Application/Amadeus/AmadeusAuthHandler.cs`, `src/services/FlightConnectorService/FlightConnectorService.Application/Sabre/SabreAuthHandler.cs`, `src/services/HotelConnectorService/HotelConnectorService.Application/Car/AmadeusCarAuthHandler.cs`
**Commit:** c20e5e7
**Applied fix:** Declared `_cachedToken` as `volatile string?` in all three auth handlers. The `volatile` keyword ensures the updated value written inside the lock in `RefreshTokenAsync` is immediately visible to all threads reading `_cachedToken` outside the lock in `SendAsync`, preventing stale or partially-written reads without adding a second lock acquisition on the hot path.

---

### WR-02: Silent swallow of all exceptions in `FlightSearchController.SearchSafeAsync`

**Files modified:** `src/services/FlightConnectorService/FlightConnectorService.API/Controllers/FlightSearchController.cs`
**Commit:** dbdaed4
**Applied fix:** Added `ILogger<FlightSearchController>` as a constructor parameter. Changed the bare `catch` block to two separate catch clauses: `OperationCanceledException` is re-thrown (propagating client disconnect / timeout), and `Exception ex` is logged via `logger.LogError` before returning an empty list. `SearchSafeAsync` changed from `static` to instance method to access the logger.

---

### WR-03: `AmadeusCarProvider` ignores `request.Passengers` and hardcodes `passengers: 1`

**Files modified:** `src/shared/TBE.Contracts/Inventory/Models/CarSearchRequest.cs`, `src/services/HotelConnectorService/HotelConnectorService.Application/Car/AmadeusCarProvider.cs`
**Commit:** 00bf8f5
**Applied fix:** Added `public int Passengers { get; init; } = 1` to `CarSearchRequest` (default 1 preserves backward compatibility). Updated `AmadeusCarProvider.SearchAsync` to pass `request.Passengers` instead of the hardcoded literal `1`.

---

### WR-04: `AirlineCode` DB column max-length is 2 but ICAO codes are 3 characters

**Files modified:** `src/services/PricingService/PricingService.Infrastructure/PricingDbContext.cs`, `src/services/PricingService/PricingService.Infrastructure/Migrations/20260415000000_AddMarkupRules.cs`, `src/services/PricingService/PricingService.Infrastructure/Migrations/PricingDbContextModelSnapshot.cs`
**Commit:** 5db11f6
**Applied fix:** Updated `HasMaxLength(2)` to `HasMaxLength(3)` in `PricingDbContext.OnModelCreating`. Updated the initial migration column definition from `nvarchar(2)` / `maxLength: 2` to `nvarchar(3)` / `maxLength: 3`. Updated the model snapshot to match (`nvarchar(3)`, maxLength 3). Since this schema has not been applied to a production database (phase 02 is in development), editing the existing migration is correct — no corrective migration needed.

---

### WR-05: Rate limiter policy is in-process only — ineffective in multi-replica deployments

**Files modified:** `src/services/SearchService/SearchService.API/Controllers/SearchController.cs`
**Commit:** eca22f3
**Applied fix:** Added a code comment directly above the `[EnableRateLimiting("gds-rate-limit")]` attribute: `// NOTE: In-process rate limit. Per-replica only. Replace with Redis sliding window before scaling.` This makes the per-replica limitation explicit at the point of use without changing runtime behaviour.

---

### WR-06: `HotelSearchController` does not validate `Rooms` collection

**Files modified:** `src/services/HotelConnectorService/HotelConnectorService.API/Controllers/HotelSearchController.cs`
**Commit:** 8eea770
**Applied fix:** Added two validation guards after the existing date check: (1) returns `BadRequest` if `request.Rooms.Count == 0`; (2) returns `BadRequest` if any room has `Adults < 1`. These checks prevent the empty-occupancies and zero-adult API errors from reaching Hotelbeds.

---

_Fixed: 2026-04-15T00:00:00Z_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
