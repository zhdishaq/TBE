---
phase: 02-inventory-layer-gds-integration
reviewed: 2026-04-15T00:00:00Z
depth: standard
files_reviewed: 67
files_reviewed_list:
  - src/services/FlightConnectorService/FlightConnectorService.API/Controllers/FlightSearchController.cs
  - src/services/FlightConnectorService/FlightConnectorService.API/Program.cs
  - src/services/FlightConnectorService/FlightConnectorService.Application/Amadeus/AmadeusAuthHandler.cs
  - src/services/FlightConnectorService/FlightConnectorService.Application/Amadeus/AmadeusFlightProvider.cs
  - src/services/FlightConnectorService/FlightConnectorService.Application/Amadeus/AmadeusOptions.cs
  - src/services/FlightConnectorService/FlightConnectorService.Application/Amadeus/AmadeusTokenResponse.cs
  - src/services/FlightConnectorService/FlightConnectorService.Application/Amadeus/IAmadeusFlightApi.cs
  - src/services/FlightConnectorService/FlightConnectorService.Application/Amadeus/Models/AmadeusFlightOffersResponse.cs
  - src/services/FlightConnectorService/FlightConnectorService.Application/Sabre/ISabreFlightApi.cs
  - src/services/FlightConnectorService/FlightConnectorService.Application/Sabre/Models/SabreBfmRequest.cs
  - src/services/FlightConnectorService/FlightConnectorService.Application/Sabre/Models/SabreBfmResponse.cs
  - src/services/FlightConnectorService/FlightConnectorService.Application/Sabre/SabreAuthHandler.cs
  - src/services/FlightConnectorService/FlightConnectorService.Application/Sabre/SabreFlightProvider.cs
  - src/services/FlightConnectorService/FlightConnectorService.Application/Sabre/SabreOptions.cs
  - src/services/FlightConnectorService/FlightConnectorService.Application/Sabre/SabreTokenResponse.cs
  - src/services/HotelConnectorService/HotelConnectorService.API/Controllers/CarSearchController.cs
  - src/services/HotelConnectorService/HotelConnectorService.API/Controllers/HotelSearchController.cs
  - src/services/HotelConnectorService/HotelConnectorService.API/Program.cs
  - src/services/HotelConnectorService/HotelConnectorService.Application/Car/AmadeusCarAuthHandler.cs
  - src/services/HotelConnectorService/HotelConnectorService.Application/Car/AmadeusCarOptions.cs
  - src/services/HotelConnectorService/HotelConnectorService.Application/Car/AmadeusCarProvider.cs
  - src/services/HotelConnectorService/HotelConnectorService.Application/Car/IAmadeusTransferApi.cs
  - src/services/HotelConnectorService/HotelConnectorService.Application/Car/Models/AmadeusTransferOffersResponse.cs
  - src/services/HotelConnectorService/HotelConnectorService.Application/Hotelbeds/HotelbedsHmacHandler.cs
  - src/services/HotelConnectorService/HotelConnectorService.Application/Hotelbeds/HotelbedsOptions.cs
  - src/services/HotelConnectorService/HotelConnectorService.Application/Hotelbeds/HotelbedsProvider.cs
  - src/services/HotelConnectorService/HotelConnectorService.Application/Hotelbeds/IHotelbedsApi.cs
  - src/services/HotelConnectorService/HotelConnectorService.Application/Hotelbeds/Models/HotelbedsAvailabilityRequest.cs
  - src/services/HotelConnectorService/HotelConnectorService.Application/Hotelbeds/Models/HotelbedsAvailabilityResponse.cs
  - src/services/PricingService/PricingService.API/Controllers/PricingController.cs
  - src/services/PricingService/PricingService.API/Program.cs
  - src/services/PricingService/PricingService.Application/Rules/IPricingRulesEngine.cs
  - src/services/PricingService/PricingService.Application/Rules/Models/MarkupRule.cs
  - src/services/PricingService/PricingService.Application/Rules/Models/MarkupType.cs
  - src/services/PricingService/PricingService.Application/Rules/Models/PricedOffer.cs
  - src/services/PricingService/PricingService.Application/Rules/Models/PricingContext.cs
  - src/services/PricingService/PricingService.Infrastructure/Migrations/20260415000000_AddMarkupRules.cs
  - src/services/PricingService/PricingService.Infrastructure/Migrations/PricingDbContextModelSnapshot.cs
  - src/services/PricingService/PricingService.Infrastructure/PricingDbContext.cs
  - src/services/PricingService/PricingService.Infrastructure/PricingDbContextFactory.cs
  - src/services/PricingService/PricingService.Infrastructure/Rules/MarkupRulesEngine.cs
  - src/services/SearchService/SearchService.API/Controllers/SearchController.cs
  - src/services/SearchService/SearchService.API/Program.cs
  - src/services/SearchService/SearchService.Application/Cache/ISearchCacheService.cs
  - src/services/SearchService/SearchService.Application/Cache/SearchCacheService.cs
  - src/services/SearchService/SearchService.Application/FlightSearch/FlightOfferDeduplicator.cs
  - src/services/SearchService/SearchService.Application/FlightSearch/FlightSearchOrchestrator.cs
  - src/services/SearchService/SearchService.Application/FlightSearch/IFlightSearchOrchestrator.cs
  - src/shared/TBE.Contracts/Inventory/ICarAvailabilityProvider.cs
  - src/shared/TBE.Contracts/Inventory/IFlightAvailabilityProvider.cs
  - src/shared/TBE.Contracts/Inventory/IHotelAvailabilityProvider.cs
  - src/shared/TBE.Contracts/Inventory/Models/CarSearchRequest.cs
  - src/shared/TBE.Contracts/Inventory/Models/FareRule.cs
  - src/shared/TBE.Contracts/Inventory/Models/FlightSearchRequest.cs
  - src/shared/TBE.Contracts/Inventory/Models/FlightSegment.cs
  - src/shared/TBE.Contracts/Inventory/Models/HotelSearchRequest.cs
  - src/shared/TBE.Contracts/Inventory/Models/PriceBreakdown.cs
  - src/shared/TBE.Contracts/Inventory/Models/UnifiedCarOffer.cs
  - src/shared/TBE.Contracts/Inventory/Models/UnifiedFlightOffer.cs
  - src/shared/TBE.Contracts/Inventory/Models/UnifiedHotelOffer.cs
  - tests/TBE.Tests.Unit/FlightConnectorService/AmadeusProviderTests.cs
  - tests/TBE.Tests.Unit/FlightConnectorService/SabreProviderTests.cs
  - tests/TBE.Tests.Unit/HotelConnectorService/HotelbedsHmacTests.cs
  - tests/TBE.Tests.Unit/HotelConnectorService/HotelbedsProviderTests.cs
  - tests/TBE.Tests.Unit/HotelConnectorService/CarProviderTests.cs
  - tests/TBE.Tests.Unit/PricingService/MarkupRulesEngineTests.cs
  - tests/TBE.Tests.Unit/SearchService/FanOutTests.cs
  - tests/TBE.Tests.Unit/SearchService/RedisCacheTests.cs
findings:
  critical: 2
  warning: 6
  info: 5
  total: 13
status: issues_found
---

# Phase 02: Code Review Report

**Reviewed:** 2026-04-15T00:00:00Z
**Depth:** standard
**Files Reviewed:** 67
**Status:** issues_found

## Summary

This phase delivers the inventory layer: GDS adapters (Amadeus flights, Sabre flights, Hotelbeds hotels, Amadeus Transfers cars), a fan-out orchestrator, a Redis HybridCache layer, and a markup-based PricingService backed by SQL Server. The architecture is structurally sound — token caching, graceful degradation, YQ/YR surcharge separation, and deduplication are all present and have corresponding tests.

Two critical issues require attention before this phase is considered done: (1) unsafe `decimal.Parse` calls that will crash on unexpected GDS payloads, and (2) a null-dereference path in `HotelbedsAvailabilityResponse` deserialization. Six warnings cover token-refresh race conditions, missing request body validation, and an incorrectly scoped rate limiter. Five info items cover naming, magic numbers, and a hardcoded connection string in the design-time factory.

---

## Critical Issues

### CR-01: `decimal.Parse` throws `FormatException` on unexpected GDS payload

**File:** `src/services/FlightConnectorService/FlightConnectorService.Application/Amadeus/AmadeusFlightProvider.cs:38-43`
**Issue:** `decimal.Parse(t.Amount)` and `decimal.Parse(o.Price.Base)` are called without try/catch or `TryParse`. The Amadeus API is known to occasionally return `"N/A"`, `""`, or locale-formatted numbers (e.g. `"1,234.00"`) for price fields during rate-limit or availability errors. A single bad payload will throw `FormatException`, propagating through `Task.WhenAll` and causing the entire search response to be empty (the outer `SearchSafeAsync` catch will swallow it, but the user gets zero results for both providers instead of just one).

The same pattern is repeated in:
- `AmadeusCarProvider.cs:27-28` — `decimal.Parse(t.MonetaryAmount)` and `decimal.Parse(o.Quotation.Base.MonetaryAmount)`
- `HotelbedsProvider.cs:57` — `decimal.Parse(rate.Net)`

**Fix:**
```csharp
// Replace every decimal.Parse(s) with:
if (!decimal.TryParse(s, System.Globalization.NumberStyles.Any,
        System.Globalization.CultureInfo.InvariantCulture, out var value))
{
    logger.LogWarning("Unexpected price format from GDS: {Raw}", s);
    value = 0m; // or skip the offer
}
```
For the `MapOffer` static methods that have no logger, consider using `decimal.TryParse` with `InvariantCulture` and returning `0m` (or filtering out the offer at the call site).

---

### CR-02: Null-dereference crash when Hotelbeds returns an empty availability response

**File:** `src/services/HotelConnectorService/HotelConnectorService.Application/Hotelbeds/HotelbedsProvider.cs:31-33`
**Issue:** The `HotelbedsAvailabilityResponse.Hotels` property is typed as `HotelbedsHotelsContainer` with `= default!` (nullable suppressed). When Hotelbeds returns a response body that omits the `"hotels"` key entirely (which it does for zero-result destinations), `raw.Hotels` deserializes as `null`. The null-conditional `raw.Hotels?.Hotels` short-circuits correctly at the outer level, but `HotelbedsHotelsContainer.Hotels` is typed `List<HotelbedsRoom>` which is already initialized to `[]` — so _that_ part is safe. However `raw.Hotels` itself being null while the property is declared non-nullable (`HotelbedsHotelsContainer Hotels`) means any future direct access without `?.` will NRE. More immediately, if any code path accesses `raw.Hotels.Hotels` without the null-conditional (e.g., in a future refactor), a NRE will occur in production.

**File:** `src/services/HotelConnectorService/HotelConnectorService.Application/Hotelbeds/Models/HotelbedsAvailabilityResponse.cs:7`
**Fix:** Mark the `Hotels` property as nullable to match actual API behavior:
```csharp
// In HotelbedsAvailabilityResponse:
[JsonPropertyName("hotels")] public HotelbedsHotelsContainer? Hotels { get; init; }
```
This makes the null-conditional in `HotelbedsProvider.cs:31` (`raw.Hotels?.Hotels`) both correct and compiler-enforced.

---

## Warnings

### WR-01: Token refresh race condition — `_cachedToken` read without lock

**File:** `src/services/FlightConnectorService/FlightConnectorService.Application/Amadeus/AmadeusAuthHandler.cs:19-23`
**Issue:** `SendAsync` checks `DateTimeOffset.UtcNow >= _tokenExpiry.AddSeconds(-30)` and then awaits `RefreshTokenAsync` (which acquires `_lock`). Between the check and the lock acquisition by thread A, thread B may have already refreshed `_cachedToken`. More critically, after `RefreshTokenAsync` returns, `_cachedToken` is read from the field without a memory barrier or lock. On platforms where the JIT may reorder reads, this could return a stale or partially-written value. The pattern is duplicated in `SabreAuthHandler.cs` and `AmadeusCarAuthHandler.cs`.

**Fix:** The double-check inside `RefreshTokenAsync` is correct, but `_cachedToken` should be read inside the lock (or made `volatile`) to guarantee the updated value is visible:
```csharp
// Option A: read token inside lock after refresh
protected override async Task<HttpResponseMessage> SendAsync(
    HttpRequestMessage request, CancellationToken cancellationToken)
{
    if (DateTimeOffset.UtcNow >= _tokenExpiry.AddSeconds(-30))
        await RefreshTokenAsync(cancellationToken);

    string token;
    await _lock.WaitAsync(cancellationToken);
    try { token = _cachedToken!; }
    finally { _lock.Release(); }

    request.Headers.Authorization = new("Bearer", token);
    return await base.SendAsync(request, cancellationToken);
}
```
Or simpler — declare `_cachedToken` as `volatile string?`.

---

### WR-02: Silent swallow of all exceptions in `FlightSearchController.SearchSafeAsync`

**File:** `src/services/FlightConnectorService/FlightConnectorService.API/Controllers/FlightSearchController.cs:44-46`
**Issue:** The catch block is bare (`catch { return []; }`) — it swallows every exception including `OperationCanceledException` (client disconnect / timeout). Swallowing `OperationCanceledException` means requests that the client has abandoned will still complete their GDS calls rather than short-circuiting. It also means any internal coding error (e.g., `NullReferenceException`) is silently ignored, making debugging very difficult.

The `FlightSearchOrchestrator` has a better pattern (logs the exception before swallowing). The controller version does neither.

**Fix:**
```csharp
private static async Task<IReadOnlyList<UnifiedFlightOffer>> SearchSafeAsync(
    IFlightAvailabilityProvider provider, FlightSearchRequest request, CancellationToken ct)
{
    try
    {
        return await provider.SearchAsync(request, ct);
    }
    catch (OperationCanceledException)
    {
        throw; // propagate cancellation — do not swallow
    }
    catch (Exception ex)
    {
        // log ex via ILogger injected into the controller
        return [];
    }
}
```

---

### WR-03: `AmadeusCarProvider` ignores `request.Passengers` and hardcodes `passengers: 1`

**File:** `src/services/HotelConnectorService/HotelConnectorService.Application/Car/AmadeusCarProvider.cs:19`
**Issue:** The API call always passes `passengers: 1` regardless of what was requested. `CarSearchRequest` does not currently carry a passenger count field, so this is a contract gap. For a transfer service, passenger count directly affects vehicle category availability and pricing. Queries for groups will always receive single-passenger offers, leading to incorrect capacity results.

**Fix:** Add `Passengers` to `CarSearchRequest` and pass it through:
```csharp
// In CarSearchRequest:
public int Passengers { get; init; } = 1;

// In AmadeusCarProvider.SearchAsync:
passengers: request.Passengers,
```

---

### WR-04: `AirlineCode` DB column max-length is 2 but ICAO codes are 3 characters

**File:** `src/services/PricingService/PricingService.Infrastructure/Migrations/20260415000000_AddMarkupRules.cs:21`
**Issue:** `AirlineCode` is capped at `nvarchar(2)`, which fits IATA 2-letter airline codes (e.g. `"BA"`) but not ICAO 3-letter codes. More importantly, the `MarkupRule.AirlineCode` field is compared against `PricingContext.CarrierCode` which is sourced from `FlightSegment.CarrierCode`. Sabre and Amadeus both return IATA 2-letter marketing carrier codes for domestic flights (fine), but some codeshare and charter scenarios return 3-character codes. If an admin tries to create a rule with a 3-character code it will be silently truncated by SQL Server or throw a string-too-long error.

**Fix:** Change the column type to `nvarchar(3)` in a new migration, and update `PricingDbContext.OnModelCreating` to `.HasMaxLength(3)`.

---

### WR-05: Rate limiter policy is in-process only — ineffective in multi-replica deployments

**File:** `src/services/SearchService/SearchService.API/Program.cs:59-76`
**Issue:** The `AddSlidingWindowLimiter("gds-rate-limit", ...)` uses the built-in ASP.NET Core in-process rate limiter. With 8 req/s limit and even 2 replicas, the effective rate is 16 req/s to the GDS — above most sandbox quotas. The comment acknowledges this ("upgrade to distributed in Phase 7") but the `[EnableRateLimiting("gds-rate-limit")]` attribute on the controller gives a false impression of rate protection. This is a warning rather than critical because it is documented, but it should be tracked.

**Fix:** Add a code comment on the `[EnableRateLimiting]` attribute in `SearchController.cs` noting the per-replica limitation and that it must be replaced with a Redis-backed rate limiter before multi-replica deployment:
```csharp
// NOTE: In-process rate limit. Per-replica only. Replace with Redis sliding window before scaling.
[EnableRateLimiting("gds-rate-limit")]
```

---

### WR-06: `HotelSearchController` does not validate `Rooms` collection

**File:** `src/services/HotelConnectorService/HotelConnectorService.API/Controllers/HotelSearchController.cs:12-21`
**Issue:** The hotel search controller validates `DestinationCode` and `CheckOut > CheckIn`, but does not validate the `Rooms` collection. If `request.Rooms` is empty, `HotelbedsProvider` will send an `occupancies: []` array to Hotelbeds which returns an API error, propagating a 5xx to the caller. If `Adults` is 0 in a room, the Hotelbeds API also rejects the request.

**Fix:**
```csharp
if (request.Rooms.Count == 0)
    return BadRequest("At least one room occupancy is required.");
if (request.Rooms.Any(r => r.Adults < 1))
    return BadRequest("Each room must have at least 1 adult.");
```

---

## Info

### IN-01: Hardcoded localhost connection string in design-time factory

**File:** `src/services/PricingService/PricingService.Infrastructure/PricingDbContextFactory.cs:14`
**Issue:** `"Server=localhost;Database=PricingDb;Trusted_Connection=True;"` is hardcoded. This is a design-time-only factory (used by `dotnet-ef migrations`), so it will never run in production, but it will fail for any developer whose SQL Server instance is not named `localhost`. Consider reading from an environment variable or a local `appsettings.Development.json`.

**Fix:**
```csharp
var connectionString = Environment.GetEnvironmentVariable("PricingDb__ConnectionString")
    ?? "Server=localhost;Database=PricingDb;Trusted_Connection=True;";
```

---

### IN-02: `NumberOfStops` computed property can return negative for empty segment list

**File:** `src/shared/TBE.Contracts/Inventory/Models/UnifiedFlightOffer.cs:12`
**Issue:** `public int NumberOfStops => Segments.Count - 1;` returns `-1` when `Segments` is empty (which is valid for test offers — see `FanOutTests.cs:28`). Any consumer that uses `NumberOfStops` as an array index or treats it as a non-negative count will get unexpected behaviour.

**Fix:**
```csharp
public int NumberOfStops => Math.Max(0, Segments.Count - 1);
```

---

### IN-03: `FlightSearchController` duplicates fan-out logic already in `FlightSearchOrchestrator`

**File:** `src/services/FlightConnectorService/FlightConnectorService.API/Controllers/FlightSearchController.cs:31-34`
**Issue:** The controller manually fans out to providers via `Task.WhenAll` and its own `SearchSafeAsync`, while `FlightSearchOrchestrator` (in SearchService) provides the same fan-out with deduplication and structured logging. The `FlightConnectorService` controller bypasses the orchestrator entirely. This means deduplication is not applied at the connector level, and the connector controller's `SearchSafeAsync` has weaker error handling (WR-02). If more providers are added, both fan-out paths must be maintained.

**Fix:** Inject `IFlightSearchOrchestrator` into the controller (it is already registered in SearchService but the same interface could be reused), or at minimum inject `FlightSearchOrchestrator` within the FlightConnectorService's own application layer so the fan-out logic is not duplicated.

---

### IN-04: `PricingController` only supports `UnifiedFlightOffer` — hotel/car pricing not reachable

**File:** `src/services/PricingService/PricingService.API/Controllers/PricingController.cs:12-35`
**Issue:** The `ApplyPricingRequest` model and `IPricingRulesEngine` interface only operate on `UnifiedFlightOffer`. The `MarkupRule` model has `ProductType` supporting `"hotel"` and `"car"`, and the DB migration and seeding infrastructure supports those types, but there is no corresponding `/pricing/apply/hotels` or `/pricing/apply/cars` endpoint. The hotel and car pricing rules in the DB are dead configuration until this is implemented.

**Fix:** Extend `IPricingRulesEngine` with overloads for `UnifiedHotelOffer` and `UnifiedCarOffer`, or generalise the input to a discriminated union/base type, and add corresponding controller endpoints.

---

### IN-05: `SabreBfmRequest` default `RequestType.Name = "200ITINS"` is a magic string

**File:** `src/services/FlightConnectorService/FlightConnectorService.Application/Sabre/Models/SabreBfmRequest.cs:49`
**Issue:** `"200ITINS"` is a Sabre-specific IntelliSell transaction code that controls the number of itineraries returned. It is not documented inline and changing it requires knowing what the string means. Given the response model note that this structure is assumed pending real credentials, this could silently produce incorrect results.

**Fix:** Define it as a named constant:
```csharp
private const string DefaultIntelliSellType = "200ITINS"; // Sabre: request up to 200 itineraries
public sealed class SabreRequestType
{
    [JsonPropertyName("Name")]
    public string Name { get; init; } = DefaultIntelliSellType;
}
```

---

_Reviewed: 2026-04-15T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
