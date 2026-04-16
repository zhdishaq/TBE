# Phase 2: Inventory Layer & GDS Integration - Research

**Researched:** 2026-04-12
**Domain:** GDS/API adapters, canonical inventory models, Redis caching, pricing rules engine — .NET 8 / C#
**Confidence:** HIGH (core NuGet versions verified against registry; Amadeus API spec verified against official OpenAPI spec; keyed DI verified against official Microsoft docs; HybridCache verified against official ASP.NET Core docs)

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| INV-01 | Unified `IInventoryConnector` abstraction normalizes results from all GDS/API sources into a canonical search model | Architecture Patterns § Interface Design; canonical model section |
| INV-02 | Amadeus REST API adapter searches flights with real-time availability and pricing | Amadeus API section; Refit + OAuth2 DelegatingHandler pattern |
| INV-03 | Sabre or Galileo adapter as second GDS source | Sabre section; keyed DI registration pattern |
| INV-04 | Hotel aggregator adapter (Hotelbeds or equivalent) | Hotelbeds HMAC section; HotelConnectorService adapter pattern |
| INV-05 | Car hire aggregator adapter (Duffel or equivalent) | Duffel section; note on product availability |
| INV-06 | Parallel fan-out search via `Task.WhenAll` | Architecture Patterns § Fan-out; pitfall: exception handling in WhenAll |
| INV-07 | Search results cached in Redis with tiered TTL | HybridCache section; TTL strategy |
| INV-08 | Source booking token preserved in Redis through booking saga | Booking token pattern; key naming strategy |
| INV-09 | Pricing Service applies markup/commission rules to raw prices | Pricing Service section; markup rules engine |
</phase_requirements>

---

## Summary

Phase 2 builds the inventory pipeline that Phase 3's booking saga depends on. It spans three layers: (1) GDS/API adapters behind `IFlightAvailabilityProvider` / `IHotelAvailabilityProvider` / `ICarAvailabilityProvider` interfaces, (2) a fan-out orchestration layer in SearchService that queries all adapters in parallel and returns a unified `UnifiedFlightOffer` / `UnifiedHotelOffer` canonical model, and (3) the PricingService markup rules engine + Redis tiered caching with HybridCache.

The most important architectural insight is that **no official .NET GDS SDKs exist** (confirmed from project memory and verified: the Amadeus .NET SDK is community-maintained with an explicit unsupported warning). All GDS adapters must be hand-rolled using Refit + `DelegatingHandler` for OAuth2 or HMAC-SHA256 authentication, wrapped behind domain interfaces. This is not a weakness — it is the intended design, and the `DelegatingHandler` middleware pattern in Refit is clean and well-established for this use case.

The second critical insight is the **Amadeus API model split**: Amadeus exposes `GET/POST /v2/shopping/flight-offers` (search) and `POST /v1/shopping/flight-offers/pricing` (price reconfirmation before booking). Phase 2 only needs the search endpoint; Phase 3 adds price reconfirmation. The pricing response already separates `base`, `fees[]`, and `taxes[]` — this maps directly to the `YQ/YR surcharges modeled separately from government taxes` architectural decision.

For Redis caching, **HybridCache** (`Microsoft.Extensions.Caching.Hybrid` 10.4.0) is the correct choice over raw `IDistributedCache` for new code: it provides cache-stampede protection, in-process L1 + Redis L2 layers, and tag-based invalidation — all needed for the tiered TTL strategy (browse 10 min, selection 90 sec, booking token).

**Primary recommendation:** Implement adapters in FlightConnectorService and HotelConnectorService (the correct project home per the D-03 structure from Phase 1), use keyed DI to register multiple `IFlightAvailabilityProvider` implementations, fan-out in SearchService via `Task.WhenAll` + `WhenEach` deduplication, and use HybridCache with per-entry `HybridCacheEntryOptions` for TTL control.

---

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Refit | 10.1.6 | Typed REST client over HttpClient — turns C# interfaces into GDS HTTP clients | Eliminates boilerplate; DelegatingHandler chain handles OAuth2/HMAC injection cleanly; official .NET 8/9/10 support |
| Refit.HttpClientFactory | 10.1.6 | Integrates Refit with `IHttpClientFactory` | Required for proper HttpClient lifetime management in DI; pairs with `AddRefitClient<T>()` |
| Microsoft.Extensions.Http.Resilience | 10.4.0 | Polly v8-based retry/circuit-breaker/timeout for HttpClient | Replaces deprecated `Polly.Extensions.Http 3.0.0`; built-in to .NET 8 resilience stack |
| Microsoft.Extensions.Caching.Hybrid | 10.4.0 | HybridCache = L1 in-memory + L2 Redis, stampede protection, tag invalidation | Correct choice over raw IDistributedCache for new code in .NET 8+ |
| Microsoft.Extensions.Caching.StackExchangeRedis | 10.0.5 | Redis IDistributedCache backing for HybridCache L2 | Required alongside HybridCache when using Redis as secondary store |
| StackExchange.Redis | 2.12.14 | Direct Redis connection for booking tokens and rate-limit guards | Already in Phase 1 stack |
| RedisRateLimiting | 1.2.1 | Distributed sliding-window rate limiter using Redis backplane | Implements GDS rate-limit guard across multi-node without hand-rolling Lua scripts |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| System.Text.Json | (in-box .NET 8) | JSON deserialization of GDS REST responses | Default; no additional package needed |
| System.ServiceModel.Http | Latest stable | SOAP client for Travelport uAPI (Galileo) if chosen as second GDS | Only if Galileo/Travelport uAPI is selected over Sabre REST |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Refit | RestSharp / raw HttpClient | Refit is more maintainable; interface-based design matches the IInventoryConnector abstraction perfectly |
| HybridCache | IDistributedCache directly | HybridCache is strictly superior for new code: stampede protection, L1+L2, same API surface |
| Microsoft.Extensions.Http.Resilience | Polly.Extensions.Http 3.0.0 | Polly.Extensions.Http is deprecated — do not use |
| RedisRateLimiting | Hand-rolled Lua ZSET script | Library is tested, maintained, and handles edge cases in distributed rate limiting |

### Installation
```bash
dotnet add package Refit --version 10.1.6
dotnet add package Refit.HttpClientFactory --version 10.1.6
dotnet add package Microsoft.Extensions.Http.Resilience --version 10.4.0
dotnet add package Microsoft.Extensions.Caching.Hybrid --version 10.4.0
dotnet add package Microsoft.Extensions.Caching.StackExchangeRedis --version 10.0.5
dotnet add package RedisRateLimiting --version 1.2.1
```

**Version verification:** All versions above verified against NuGet registry as of 2026-04-12. [VERIFIED: nuget.org registry via WebFetch]

---

## Amadeus REST API

### OAuth2 Client Credentials Flow

[VERIFIED: amadeus-open-api-specification GitHub repo, FlightOffersSearch_v2_swagger_specification.json]

The Amadeus Self-Service APIs use OAuth2 client credentials flow. There is no official, supported .NET SDK — the community `amadeus-dotnet` package carries an explicit "not maintained by Amadeus team" warning. Use Refit with a custom `DelegatingHandler`.

**Token endpoint (sandbox):** `https://test.api.amadeus.com/v1/security/oauth2/token`
**Token endpoint (production):** `https://api.amadeus.com/v1/security/oauth2/token`

Token request body (`application/x-www-form-urlencoded`):
```
grant_type=client_credentials
client_id={API_KEY}
client_secret={API_SECRET}
```

Token response includes `access_token` (Bearer), `expires_in` (seconds), `token_type: "Bearer"`.

**DelegatingHandler pattern for token caching:**
```csharp
// Source: Refit docs + Amadeus OAuth2 pattern [VERIFIED: reactiveui/refit README]
public class AmadeusAuthHandler(IHttpClientFactory httpClientFactory, IOptionsMonitor<AmadeusOptions> opts) 
    : DelegatingHandler
{
    private string? _cachedToken;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (DateTimeOffset.UtcNow >= _tokenExpiry.AddSeconds(-30))
            await RefreshTokenAsync(cancellationToken);

        request.Headers.Authorization = new("Bearer", _cachedToken);
        return await base.SendAsync(request, cancellationToken);
    }

    private async Task RefreshTokenAsync(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("amadeus-auth");
        var body = new FormUrlEncodedContent([
            new("grant_type", "client_credentials"),
            new("client_id", opts.CurrentValue.ApiKey),
            new("client_secret", opts.CurrentValue.ApiSecret),
        ]);
        var resp = await client.PostAsync("/v1/security/oauth2/token", body, ct);
        var json = await resp.Content.ReadFromJsonAsync<AmadeusTokenResponse>(ct);
        _cachedToken = json!.AccessToken;
        _tokenExpiry = DateTimeOffset.UtcNow.AddSeconds(json.ExpiresIn);
    }
}
```

### Flight Search Endpoint

**Sandbox base URL:** `https://test.api.amadeus.com/v2`
**Production base URL:** `https://api.amadeus.com/v2`

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/shopping/flight-offers` | Search by query string |
| POST | `/shopping/flight-offers` | Search by JSON body (supports multi-city) |

**Key request parameters (GET):**
| Parameter | Required | Description |
|-----------|----------|-------------|
| `originLocationCode` | Yes | IATA departure airport, e.g., `"LHR"` |
| `destinationLocationCode` | Yes | IATA arrival airport, e.g., `"BKK"` |
| `departureDate` | Yes | `YYYY-MM-DD` |
| `adults` | Yes | Passengers ≥12; range 1–9 |
| `returnDate` | No | For round-trip |
| `children` | No | Passengers 2–11 |
| `infants` | No | Passengers < 2 |
| `travelClass` | No | `ECONOMY`, `PREMIUM_ECONOMY`, `BUSINESS`, `FIRST` |
| `nonStop` | No | Boolean |
| `currencyCode` | No | ISO 4217 |
| `max` | No | Max offers returned (default varies) |

**Response pricing structure** (maps to the architectural decision that YQ/YR surcharges must be separate from government taxes):
```json
{
  "price": {
    "currency": "GBP",
    "grandTotal": "542.30",
    "base": "420.00",
    "fees": [
      { "amount": "0.00", "type": "TICKETING" },
      { "amount": "0.00", "type": "FORM_OF_PAYMENT" },
      { "amount": "0.00", "type": "SUPPLIER" }
    ],
    "taxes": [
      { "amount": "45.00", "code": "GB" },
      { "amount": "77.30", "code": "YQ" }
    ]
  }
}
```

**Key insight:** YQ/YR codes in the `taxes[]` array are carrier-imposed surcharges (fuel, security). Government taxes have other codes (GB, US, etc.). The canonical `UnifiedFlightOffer` model must preserve this distinction.

### Refit Interface for Amadeus

```csharp
// In FlightConnectorService.Application/Amadeus/IAmadeusFlightApi.cs
[Headers("Accept: application/json")]
public interface IAmadeusFlightApi
{
    [Get("/shopping/flight-offers")]
    Task<AmadeusFlightOffersResponse> SearchAsync(
        [AliasAs("originLocationCode")] string origin,
        [AliasAs("destinationLocationCode")] string destination,
        [AliasAs("departureDate")] string departureDate,
        [AliasAs("adults")] int adults,
        [AliasAs("returnDate")] string? returnDate = null,
        [AliasAs("children")] int? children = null,
        [AliasAs("infants")] int? infants = null,
        [AliasAs("travelClass")] string? travelClass = null,
        [AliasAs("nonStop")] bool? nonStop = null,
        [AliasAs("currencyCode")] string? currencyCode = null,
        CancellationToken cancellationToken = default);
}
```

---

## Sabre REST API (Second GDS)

[ASSUMED — Sabre developer portal content was not accessible in this session; details drawn from project memory and known GDS patterns]

Sabre REST APIs use **OAuth2 client credentials** flow (similar to Amadeus). The key flight search API is **Bargain Finder Max (BFM)**, available as both SOAP and REST. The REST endpoint supports JSON responses.

**Known sandbox base URL:** `https://api.havail.sabre.com` [ASSUMED]
**Token endpoint:** `https://api.havail.sabre.com/v2/auth/token` [ASSUMED]
**BFM REST endpoint:** `POST /v4.3.0/shop/flights/reqs` [ASSUMED]

**Registration complexity:** Sabre production credentials require a signed contract and PCC (Pseudo City Code) activation — the project memory notes this takes 4–8 weeks. For Phase 2, use the Sabre test environment. Apply for production credentials immediately if not already done.

**Alternative: Travelport (Galileo/Worldspan) via uAPI SOAP.** The Travelport uAPI is SOAP-based (`System.ServiceModel.Http` or `dotnet-svcutil`-generated proxies), not REST. SOAP adds complexity. If Sabre sandbox credentials can be obtained faster, prefer Sabre REST over Travelport SOAP for Phase 2. Defer Galileo to Phase 7 (hardening).

**Keyed DI registration for multiple GDS providers:**

```csharp
// Source: Microsoft official DI docs [VERIFIED: learn.microsoft.com/dotnet/core/extensions/dependency-injection]
services.AddKeyedSingleton<IFlightAvailabilityProvider, AmadeusFlightProvider>("amadeus");
services.AddKeyedSingleton<IFlightAvailabilityProvider, SabreFlightProvider>("sabre");

// Resolution in SearchService fan-out:
public class FlightSearchService(
    [FromKeyedServices("amadeus")] IFlightAvailabilityProvider amadeus,
    [FromKeyedServices("sabre")] IFlightAvailabilityProvider sabre)
{ ... }
```

---

## Hotelbeds REST API

[MEDIUM confidence — endpoint structure confirmed from official Hotelbeds developer portal; HMAC formula confirmed from multiple community sources but not from official docs directly]

Hotelbeds uses **HMAC-SHA256** authentication. No official .NET SDK exists.

### HMAC Signature Formula

```
signature = SHA256(apiKey + sharedSecret + unixTimestampSeconds)
```

The Unix timestamp must be current (within ~5 minutes of server time). Signature is hex-encoded lowercase.

**Required headers on every request:**
```
Api-key: {your-api-key}
X-Signature: {sha256-hex-of-apiKey+sharedSecret+unixTs}
Accept: application/json
Accept-Encoding: gzip
```

### DelegatingHandler for Hotelbeds HMAC

```csharp
// Source: Hotelbeds developer portal authentication docs [MEDIUM - portal content not fully accessible]
public class HotelbedsHmacHandler(IOptionsMonitor<HotelbedsOptions> opts) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var o = opts.CurrentValue;
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var raw = $"{o.ApiKey}{o.SharedSecret}{ts}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLower();

        request.Headers.Add("Api-key", o.ApiKey);
        request.Headers.Add("X-Signature", hash);
        return base.SendAsync(request, cancellationToken);
    }
}
```

### Key Hotelbeds Endpoints

**Sandbox base URL:** `https://api.test.hotelbeds.com/hotel-api/1.0` [MEDIUM - from community sources]
**Production base URL:** `https://api.hotelbeds.com/hotel-api/1.0`

| Method | Path | Purpose |
|--------|------|---------|
| POST | `/hotels` | Availability search — returns hotels with room types, rates, cancellation policies |
| POST | `/checkrates` | Re-check rate for `recheck`-type offers before booking |
| POST | `/bookings` | Confirm booking |

**Availability request body key fields:**
- `stay.checkIn` / `stay.checkOut` — ISO 8601 dates
- `occupancies[]` — rooms with adult/child counts
- `hotels.hotel[]` — filter by hotel codes (optional)
- `destination.code` — IATA destination city code

---

## Duffel API (Car Hire / Ground Transport)

[VERIFIED: duffel.com docs page 2026-04-12]

**Important finding:** As of 2026-04-12, the Duffel API documentation lists **Flights**, **Stays**, and **Payments** as products. **Car hire/ground transport is not listed** as an available Duffel product. [VERIFIED: duffel.com/docs/api]

**Implication for INV-05:** The requirement specifies "Duffel or equivalent." Since Duffel does not appear to offer car hire, the planner should select an alternative. Candidates:

| Provider | Type | Notes |
|----------|------|-------|
| Rentalcars.com API | REST | B2B car hire aggregator; requires commercial agreement |
| CarTrawler API | REST | Large B2B car hire aggregator used by many OTAs; requires agreement |
| Travelport (uAPI) | SOAP | Includes car hire via GDS; SOAP complexity |
| Amadeus Car Search | REST | `GET /shopping/availability/transfer-offers` — available in Self-Service |

**Recommended alternative:** Use **Amadeus Transfer Search** (`/v1/shopping/availability/transfer-offers`) available in the same Amadeus Self-Service account, or defer car hire to Phase 4/5 if a commercial B2B agreement with CarTrawler/Rentalcars takes time to establish. The planner should flag this as an open question requiring user input.

---

## Architecture Patterns

### Service Responsibility Split

Phase 2 spans three existing services (from Phase 1 scaffold):

| Service | Responsibility |
|---------|----------------|
| `FlightConnectorService` | GDS adapter implementations (`AmadeusFlightProvider`, `SabreFlightProvider`) |
| `HotelConnectorService` | Hotel adapter implementation (`HotelbedsProvider`) |
| `SearchService` | Fan-out orchestration, deduplication, HybridCache, MassTransit search request consumers |
| `PricingService` | Markup/commission rules engine; transforms raw net fares to gross selling prices |

The interface contracts (`IFlightAvailabilityProvider`, `IHotelAvailabilityProvider`, `ICarAvailabilityProvider`) belong in `TBE.Contracts` (shared project) or in a new `TBE.Domain.Contracts` folder within `TBE.Contracts`. Phase 1 already has `TBE.Contracts` with booking events — inventory interfaces extend the same project.

### Recommended Project Structure

```
src/shared/TBE.Contracts/
├── Events/
│   └── BookingEvents.cs         (Phase 1 - exists)
├── Inventory/
│   ├── IFlightAvailabilityProvider.cs
│   ├── IHotelAvailabilityProvider.cs
│   ├── ICarAvailabilityProvider.cs
│   └── Models/
│       ├── UnifiedFlightOffer.cs
│       ├── UnifiedHotelOffer.cs
│       ├── UnifiedCarOffer.cs
│       ├── FlightSearchRequest.cs
│       └── PriceBreakdown.cs    (base + surcharges[] + taxes[])
└── Search/
    └── SearchRequested.cs       (MassTransit message for async search)

src/services/FlightConnectorService/
├── FlightConnectorService.API/
│   └── Program.cs               (Phase 1 - exists, keyed DI registration here)
└── FlightConnectorService.Application/
    ├── Amadeus/
    │   ├── IAmadeusFlightApi.cs  (Refit interface)
    │   ├── AmadeusFlightProvider.cs  (IFlightAvailabilityProvider impl)
    │   ├── AmadeusAuthHandler.cs
    │   └── Models/              (Amadeus-specific response DTOs)
    └── Sabre/
        ├── ISabreFlightApi.cs
        ├── SabreFlightProvider.cs
        ├── SabreAuthHandler.cs
        └── Models/

src/services/HotelConnectorService/
└── HotelConnectorService.Application/
    └── Hotelbeds/
        ├── IHotelbedsApi.cs
        ├── HotelbedsProvider.cs
        ├── HotelbedsHmacHandler.cs
        └── Models/

src/services/SearchService/
└── SearchService.Application/
    ├── FlightSearch/
    │   ├── FlightSearchOrchestrator.cs   (Task.WhenAll fan-out)
    │   └── FlightOfferDeduplicator.cs
    └── Cache/
        └── SearchCacheService.cs        (HybridCache wrapper)

src/services/PricingService/
└── PricingService.Application/
    └── Rules/
        ├── IPricingRulesEngine.cs
        ├── MarkupRulesEngine.cs
        └── Models/
            ├── MarkupRule.cs
            └── PricedOffer.cs
```

### Canonical Model Design

The `UnifiedFlightOffer` model must:
- Separate `Base` fare from `Surcharges[]` (YQ/YR carrier charges) from `Taxes[]` (government taxes)
- Include `GrandTotal` for display
- Carry a `SourceRef` (opaque string from each GDS — stored in Redis as booking token)
- Include `ExpiresAt` (when the offer expires — typically 30 min for Amadeus)

```csharp
// In TBE.Contracts/Inventory/Models/UnifiedFlightOffer.cs
public sealed record UnifiedFlightOffer
{
    public Guid OfferId { get; init; } = Guid.NewGuid();
    public string Source { get; init; } = default!;       // "amadeus" | "sabre"
    public string SourceRef { get; init; } = default!;     // opaque token for booking
    public DateTimeOffset ExpiresAt { get; init; }
    public PriceBreakdown Price { get; init; } = default!;
    public IReadOnlyList<FlightSegment> Segments { get; init; } = [];
    public IReadOnlyList<FareRule> FareRules { get; init; } = [];
    public string CabinClass { get; init; } = default!;
}

public sealed record PriceBreakdown
{
    public string Currency { get; init; } = default!;
    public decimal Base { get; init; }
    public IReadOnlyList<PriceComponent> Surcharges { get; init; } = [];  // YQ, YR codes
    public IReadOnlyList<PriceComponent> Taxes { get; init; } = [];       // GB, US, etc.
    public decimal GrandTotal => Base + Surcharges.Sum(s => s.Amount) + Taxes.Sum(t => t.Amount);
}

public sealed record PriceComponent(string Code, decimal Amount);
```

### Pattern 1: Fan-Out with `Task.WhenAll` + Graceful Degradation

```csharp
// Source: .NET Task Parallel Library pattern [ASSUMED - standard .NET pattern]
public async Task<IReadOnlyList<UnifiedFlightOffer>> SearchAllAsync(
    FlightSearchRequest request, CancellationToken ct)
{
    var providers = _providers; // IEnumerable<IFlightAvailabilityProvider>
    var tasks = providers.Select(p => SearchSafeAsync(p, request, ct));
    var results = await Task.WhenAll(tasks);
    return results
        .SelectMany(r => r)
        .DistinctBy(o => o.SourceRef)   // deduplicate identical offers across GDS
        .OrderBy(o => o.Price.GrandTotal)
        .ToList();
}

private static async Task<IReadOnlyList<UnifiedFlightOffer>> SearchSafeAsync(
    IFlightAvailabilityProvider provider, FlightSearchRequest request, CancellationToken ct)
{
    try { return await provider.SearchAsync(request, ct); }
    catch (Exception ex)
    {
        // Log and return empty — one GDS failure should NOT fail the whole search
        _logger.LogWarning(ex, "GDS provider {Name} failed", provider.Name);
        return [];
    }
}
```

**Critical:** Do NOT use `Task.WhenAll` without wrapping each task in a try/catch. An unhandled exception in any task will surface as an `AggregateException` and return NO results to the user.

### Pattern 2: HybridCache Tiered TTL

```csharp
// Source: Microsoft official HybridCache docs [VERIFIED: learn.microsoft.com/aspnet/core/performance/caching/hybrid]
public class SearchCacheService(HybridCache cache, IDistributedCache redis)
{
    private static readonly HybridCacheEntryOptions BrowseTtl = new()
        { Expiration = TimeSpan.FromMinutes(10), LocalCacheExpiration = TimeSpan.FromMinutes(2) };

    private static readonly HybridCacheEntryOptions SelectionTtl = new()
        { Expiration = TimeSpan.FromSeconds(90), LocalCacheExpiration = TimeSpan.FromSeconds(30) };

    public async Task<IReadOnlyList<UnifiedFlightOffer>> GetOrSearchAsync(
        string cacheKey, Func<CancellationToken, Task<IReadOnlyList<UnifiedFlightOffer>>> factory,
        bool isSelection, CancellationToken ct)
    {
        var opts = isSelection ? SelectionTtl : BrowseTtl;
        return await cache.GetOrCreateAsync(cacheKey, factory, opts, cancellationToken: ct);
    }

    // Booking tokens: stored directly in Redis (not HybridCache) — precise TTL control
    public async Task StoreBookingTokenAsync(string sessionId, UnifiedFlightOffer offer, CancellationToken ct)
    {
        var key = $"booking-token:{sessionId}";
        var value = JsonSerializer.SerializeToUtf8Bytes(offer);
        await redis.SetAsync(key, value, new DistributedCacheEntryOptions
            { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2) }, ct);
    }
}
```

### Pattern 3: Keyed DI for Multiple GDS Providers

```csharp
// Source: Microsoft official DI docs [VERIFIED: learn.microsoft.com/dotnet/core/extensions/dependency-injection]
// In FlightConnectorService.API/Program.cs:
services.AddKeyedSingleton<IFlightAvailabilityProvider, AmadeusFlightProvider>("amadeus");
services.AddKeyedSingleton<IFlightAvailabilityProvider, SabreFlightProvider>("sabre");

// In SearchService, inject as IEnumerable<IFlightAvailabilityProvider>
// OR use GetKeyedService per provider name
services.AddSingleton<IFlightSearchOrchestrator>(sp => new FlightSearchOrchestrator(
    sp.GetKeyedService<IFlightAvailabilityProvider>("amadeus")!,
    sp.GetKeyedService<IFlightAvailabilityProvider>("sabre")!
));
```

**Note:** For cross-service fan-out: SearchService calls FlightConnectorService via HTTP (service boundary), not direct DI injection (D-08: no cross-service project references). The keyed DI applies within FlightConnectorService. SearchService calls FlightConnectorService's HTTP API. This is the correct architecture.

### Pattern 4: Refit Client Registration with DelegatingHandler

```csharp
// Source: Refit 10.x docs [VERIFIED: nuget.org/packages/Refit]
services
    .AddTransient<AmadeusAuthHandler>()
    .AddRefitClient<IAmadeusFlightApi>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri(config["Amadeus:BaseUrl"]!))
    .AddHttpMessageHandler<AmadeusAuthHandler>()
    .AddStandardResilienceHandler();   // from Microsoft.Extensions.Http.Resilience
```

### Anti-Patterns to Avoid

- **Service-to-service DI injection:** SearchService must call FlightConnectorService via HTTP API — never via shared project reference (D-08)
- **Task.WhenAll without individual try/catch:** Any provider exception will propagate as AggregateException and wipe all results
- **Mutable PriceBreakdown:** The canonical model must be immutable records; pricing correctness errors are catastrophic
- **Using `Polly.Extensions.Http`:** This package is deprecated as of 2026; use `Microsoft.Extensions.Http.Resilience` instead
- **Storing booking tokens only in memory:** Booking tokens must survive service restarts — Redis is mandatory
- **Merging YQ/YR into government taxes:** EU/UK regulation requires surcharges shown separately; the canonical model must preserve the distinction
- **Using the `amadeus-dotnet` community SDK:** Explicitly not supported by Amadeus; hand-roll with Refit instead

---

## Pricing Service Design

The PricingService applies markup/commission rules to raw net fares before returning gross selling prices.

### Markup Rules Engine

```csharp
// Simple rules engine pattern [ASSUMED - standard enterprise pattern]
public interface IPricingRulesEngine
{
    Task<PricedOffer> ApplyAsync(UnifiedFlightOffer rawOffer, PricingContext context, CancellationToken ct);
}

public record MarkupRule
{
    public Guid Id { get; init; }
    public string ProductType { get; init; } = default!;  // "flight" | "hotel" | "car"
    public string? AirlineCode { get; init; }             // null = all airlines
    public string? RouteOrigin { get; init; }             // null = all origins
    public MarkupType Type { get; init; }                 // Percentage | FixedAmount
    public decimal Value { get; init; }                   // 5.0 = 5%
    public decimal? MaxAmount { get; init; }              // cap
    public bool IsActive { get; init; }
}
```

The PricingService database (already scaffolded in Phase 1 as `PricingDbContext`) needs a `MarkupRules` table. The PricingService exposes an HTTP endpoint that SearchService calls after fan-out but before caching results.

**Pricing call sequence:**
1. SearchService receives raw offers from FlightConnectorService
2. SearchService calls PricingService `POST /pricing/apply` with offers + channel context (B2C vs B2B)
3. PricingService returns gross prices with markup applied per rule
4. SearchService caches priced offers in HybridCache

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| HTTP resilience (retry, circuit breaker) | Custom retry loop | `Microsoft.Extensions.Http.Resilience` | Polly v8 + exponential backoff built in; `AddStandardResilienceHandler()` covers all GDS transient errors |
| Redis rate limiting with sliding window | Lua ZSET scripts | `RedisRateLimiting 1.2.1` | Distributed-safe, tested, maps to .NET 7/8 `RateLimiter` abstraction |
| HybridCache stampede protection | Double-check locking | `HybridCache.GetOrCreateAsync` | Built-in; prevents thundering herd when cache expires during high traffic |
| OAuth2 token caching/refresh | Manual expiry logic | `DelegatingHandler` + `IOptionsMonitor` | Standard pattern; easier to test; thread-safe with SemaphoreSlim |
| JSON deserialization of GDS responses | Manual string parsing | `System.Text.Json` with `[JsonPropertyName]` attributes | GDS APIs return consistent JSON; hand-parsing is fragile |

**Key insight:** Every GDS authentication mechanism (OAuth2 client credentials, HMAC-SHA256) can be cleanly encapsulated in a single `DelegatingHandler` class of < 40 lines. Resist the urge to build abstractions on top of abstractions — the handler IS the abstraction.

---

## Common Pitfalls

### Pitfall 1: `Task.WhenAll` Swallows All Results on Single Failure
**What goes wrong:** If one GDS provider throws an unhandled exception, `Task.WhenAll` re-throws it and discards results from all other providers.
**Why it happens:** `WhenAll` waits for all tasks but propagates the first exception on `await`.
**How to avoid:** Wrap each provider call in a try/catch that returns an empty list on failure and logs the error.
**Warning signs:** Search returning zero results intermittently, AggregateException in logs.

### Pitfall 2: MSSQL Tools18 Path (Phase 1 Carryover)
**What goes wrong:** Health checks or migrator fail because `/opt/mssql-tools18/bin/sqlcmd` was the wrong path.
**Mitigation:** Already documented in Phase 1 RESEARCH.md. Carried forward for awareness only.

### Pitfall 3: Booking Token TTL Mismatch with GDS Offer Expiry
**What goes wrong:** The Redis booking token TTL is set to 90 seconds but the Amadeus offer only guarantees pricing for 30 minutes. If the token is retrieved after 31 minutes, the re-price at booking time will return a different price.
**How to avoid:** Set `ExpiresAt` from the GDS response; use this as the booking token Redis TTL. Never use a fixed 90-second TTL for pricing guarantees — 90 sec is for the selection step only. The booking token stored after selection should use the offer's `ExpiresAt` as its TTL.
**Warning signs:** Saga failing at price reconfirmation step with "fare no longer available."

### Pitfall 4: YQ/YR Surcharges Merged with Government Taxes
**What goes wrong:** The canonical model sums all Amadeus `taxes[]` entries into a single "Taxes" field.
**Why it happens:** The Amadeus `taxes[]` array mixes YQ/YR (carrier surcharges) with government taxes — they look identical in the JSON.
**How to avoid:** At mapping time, check the tax code: YQ and YR are ALWAYS carrier surcharges. All other codes are government taxes. Map them to separate lists in `PriceBreakdown`.
**Warning signs:** Refund calculations incorrect; EU/UK display compliance failure.

### Pitfall 5: Hotelbeds Timestamp Clock Skew
**What goes wrong:** HMAC authentication fails with 401 even though the formula is correct.
**Why it happens:** Hotelbeds rejects signatures generated more than ~5 minutes from server time. In Docker Compose, container clocks can drift from host time after a VM sleep/resume.
**How to avoid:** Use `DateTimeOffset.UtcNow.ToUnixTimeSeconds()` (not ticks, not milliseconds). Ensure NTP sync on the dev machine. Consider adding `Clock: UTC` in docker-compose service config.
**Warning signs:** Intermittent 401s from Hotelbeds that self-resolve after container restart.

### Pitfall 6: Amadeus Sandbox Rate Limits
**What goes wrong:** Sandbox throws 429 during development/testing.
**Why it happens:** Amadeus Self-Service sandbox has strict rate limits (undocumented but approximately 10 req/sec). Running integration tests in parallel exceeds this.
**How to avoid:** Use the RedisRateLimiting sliding window guard before calling Amadeus. Set limit to 8 req/sec in config. Cache aggressively during development.
**Warning signs:** 429 errors with `X-RateLimit-Remaining: 0` header.

### Pitfall 7: Cross-Service Project References (D-08 Violation)
**What goes wrong:** SearchService directly references `FlightConnectorService.Application` to call provider implementations.
**Why it happens:** Tempting shortcut during development.
**How to avoid:** SearchService communicates with FlightConnectorService only via HTTP API. The `IFlightAvailabilityProvider` interface belongs in `TBE.Contracts` (shared), but implementations stay in FlightConnectorService.
**Warning signs:** `<ProjectReference>` from SearchService to FlightConnectorService in .csproj.

---

## Runtime State Inventory

> This is a greenfield phase — no rename/refactor involved. All service scaffolds from Phase 1 are stubs with empty Application layers.

**Nothing found in any category** — verified by inspecting Phase 1 scaffolded files: `SearchService.Application/Class1.cs`, `FlightConnectorService.Application/Class1.cs` contain only placeholder `Class1` stubs. No domain state exists to migrate.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | All service builds | Yes | 10.0.200-preview (backwards-compatible with net8.0) | — |
| Docker / Docker Compose | Phase 1 infrastructure | Assumed yes (Phase 1 completed) | From Phase 1 | — |
| Redis | HybridCache L2, booking tokens, rate limiter | Assumed yes (Phase 1 completed) | From Phase 1 | — |
| Amadeus Self-Service sandbox credentials | INV-02 | Unknown — must verify | Same-day via self-service signup | No fallback for INV-02 |
| Sabre sandbox credentials | INV-03 | Unknown — likely not yet applied | 4–8 week lead time [CITED: project STATE.md] | Defer INV-03 to Phase 7 if credentials not available |
| Hotelbeds test credentials | INV-04 | Unknown | Requires registration at developer.hotelbeds.com | Defer hotel adapter if no credentials |
| Duffel car hire | INV-05 | Not applicable — product doesn't exist | [VERIFIED: duffel.com/docs/api] | Amadeus Transfer Search or defer |

**Missing dependencies with no fallback:**
- **Amadeus Self-Service sandbox credentials** — must be created at https://developers.amadeus.com before Phase 2 Plan 1 can be executed. Registration is same-day. This blocks INV-02.

**Missing dependencies with fallback:**
- **Sabre credentials** — if not available, use Amadeus as the only GDS for Phase 2 and plan Sabre for Phase 7. Mark INV-03 as Phase 7.
- **Hotelbeds credentials** — if registration is delayed, defer hotel adapter stub; implement interface + mock provider for testing.
- **Car hire adapter** — Duffel does not offer car hire; use Amadeus Transfer Search or defer.

---

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit (standard for .NET 8 microservices) + NSubstitute for mocking |
| Config file | none — see Wave 0 |
| Quick run command | `dotnet test --filter "Category=Unit" --no-build` |
| Full suite command | `dotnet test` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| INV-01 | `UnifiedFlightOffer` canonical model compiles and properties are correctly structured | unit | `dotnet test --filter "INV01"` | ❌ Wave 0 |
| INV-02 | Amadeus adapter maps raw response to `UnifiedFlightOffer` correctly (including YQ/YR separation) | unit | `dotnet test --filter "INV02"` | ❌ Wave 0 |
| INV-02 | Amadeus adapter calls token endpoint and retries with new token on 401 | unit | `dotnet test --filter "INV02_Auth"` | ❌ Wave 0 |
| INV-03 | Sabre adapter maps response to same canonical model as Amadeus | unit | `dotnet test --filter "INV03"` | ❌ Wave 0 |
| INV-04 | Hotelbeds HMAC signature is correctly generated (known inputs → known output) | unit | `dotnet test --filter "INV04_Hmac"` | ❌ Wave 0 |
| INV-06 | Fan-out returns combined results when both providers succeed | unit | `dotnet test --filter "INV06_Happy"` | ❌ Wave 0 |
| INV-06 | Fan-out returns partial results when one provider fails (graceful degradation) | unit | `dotnet test --filter "INV06_Degraded"` | ❌ Wave 0 |
| INV-07 | Second search within TTL window returns cached result (no provider call) | unit (mock HybridCache) | `dotnet test --filter "INV07"` | ❌ Wave 0 |
| INV-08 | Booking token stored in Redis is retrievable by session ID | integration | `dotnet test --filter "INV08" --filter "Category=Integration"` | ❌ Wave 0 |
| INV-09 | Pricing engine applies percentage markup to raw net fare correctly | unit | `dotnet test --filter "INV09"` | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test --filter "Category=Unit" --no-build`
- **Per wave merge:** `dotnet test`
- **Phase gate:** Full suite green before `/gsd-verify-work`

### Wave 0 Gaps
- [ ] `tests/TBE.Tests.Unit/` directory — covers INV-01 through INV-09 unit tests
- [ ] `tests/TBE.Tests.Integration/` directory — covers INV-08 (requires running Redis)
- [ ] Test project csproj files with xUnit + NSubstitute dependencies
- [ ] `tests/TBE.Tests.Unit/FlightConnectorService/AmadeusProviderTests.cs` — INV-02
- [ ] `tests/TBE.Tests.Unit/FlightConnectorService/HotelbedsHmacTests.cs` — INV-04
- [ ] `tests/TBE.Tests.Unit/SearchService/FanOutTests.cs` — INV-06
- [ ] `tests/TBE.Tests.Unit/PricingService/MarkupRulesEngineTests.cs` — INV-09

---

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No (API gateway handles; GDS credentials are service-to-service) | — |
| V3 Session Management | Yes (booking tokens in Redis) | Redis key expiry + session ID from JWT sub claim |
| V4 Access Control | Yes (SearchService endpoints require valid JWT) | YARP gateway enforces JWT; `[Authorize]` on internal endpoints |
| V5 Input Validation | Yes (search parameters) | Validate IATA codes, date formats, passenger counts before calling GDS |
| V6 Cryptography | Yes (Hotelbeds HMAC) | `System.Security.Cryptography.SHA256` — never hand-roll |

### Known Threat Patterns

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| GDS credential exposure | Information Disclosure | Store in `.env` / environment secrets only (D-12, D-13 from Phase 1 CONTEXT.md); never in appsettings.json committed to git |
| Redis booking token enumeration | Elevation of Privilege | Use opaque UUIDs as session IDs, not sequential or predictable keys |
| Search parameter injection | Tampering | Validate IATA airport codes against regex `^[A-Z]{3}$`; reject invalid inputs before calling GDS |
| Cache poisoning | Tampering | Cache keys generated server-side from validated inputs; never use raw user input as cache key |
| 429 / GDS rate limit abuse | DoS | RedisRateLimiting sliding window guard prevents abuse from downstream clients |

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Sabre sandbox base URL is `https://api.havail.sabre.com` | Sabre REST API section | Integration tests fail to connect; correct URL needed from Sabre developer portal |
| A2 | Sabre BFM REST endpoint is `POST /v4.3.0/shop/flights/reqs` | Sabre REST API section | Wrong endpoint; need to verify against Sabre API docs after credentials obtained |
| A3 | Hotelbeds sandbox base URL is `https://api.test.hotelbeds.com/hotel-api/1.0` | Hotelbeds section | 404 errors; verify at developer.hotelbeds.com after registration |
| A4 | Hotelbeds HMAC formula is `SHA256(apiKey + sharedSecret + unixTimestampSeconds)` | Hotelbeds HMAC section | Auth failures; verify exact formula from official docs after registration |
| A5 | MarkupRulesEngine simple percentage/fixed amount pattern | Pricing Service section | Rules engine insufficient if business requires complex combinatorial rules; clarify with user |
| A6 | SearchService calls FlightConnectorService via HTTP (not via shared DI) | Architecture Patterns | If team decides to merge services, this changes significantly |

---

## Open Questions

1. **Sabre vs Galileo as second GDS**
   - What we know: Sabre has REST API; Galileo (Travelport uAPI) is SOAP-only; both require 4–8 week production certification
   - What's unclear: Which has faster sandbox onboarding? Does the business have an existing relationship with either?
   - Recommendation: Default to Sabre REST for Phase 2 (REST simpler than SOAP). If Sabre credentials delayed, implement with mock provider, defer to Phase 7.

2. **Car hire adapter: Duffel does not offer car hire**
   - What we know: Duffel lists Flights, Stays, Payments only as of 2026-04-12
   - What's unclear: Does the user have an existing relationship with CarTrawler, Rentalcars.com, or another car hire aggregator?
   - Recommendation: Use Amadeus Transfer Search (same API account) as a lightweight alternative, or explicitly defer car hire to Phase 5/7. Plan 3 of Phase 2 should be adjusted.

3. **Pricing rules storage: DB vs config file**
   - What we know: PricingDbContext exists (Phase 1); markup rules could be stored in DB or in appsettings
   - What's unclear: Do markup rules need runtime editing (backoffice UI), or are they deploy-time config?
   - Recommendation: If rules need runtime editing → DB table with a simple admin endpoint. If static → `IOptions<List<MarkupRule>>` from appsettings. Phase 2 plan should clarify.

4. **Amadeus Self-Service credentials: created?**
   - What we know: Same-day sandbox registration; project STATE.md says to apply immediately
   - What's unclear: Whether credentials have been created and tested
   - Recommendation: Verify before Plan 1 execution begins. Add Wave 0 task to create credentials and test token endpoint manually with curl/Postman.

---

## Sources

### Primary (HIGH confidence)
- `https://raw.githubusercontent.com/amadeus4dev/amadeus-open-api-specification/main/spec/json/FlightOffersSearch_v2_swagger_specification.json` — Amadeus API endpoints, request parameters, response pricing structure
- `https://learn.microsoft.com/dotnet/core/extensions/dependency-injection` — Keyed services: `AddKeyedSingleton`, `FromKeyedServices` pattern
- `https://learn.microsoft.com/aspnet/core/performance/caching/hybrid` — HybridCache API, TTL options, tag invalidation
- `https://nuget.org/packages/Refit` — Version 10.1.6, DelegatingHandler pattern
- `https://nuget.org/packages/Refit.HttpClientFactory` — Version 10.1.6
- `https://nuget.org/packages/Microsoft.Extensions.Http.Resilience` — Version 10.4.0, replaces deprecated Polly.Extensions.Http
- `https://nuget.org/packages/Microsoft.Extensions.Caching.Hybrid` — Version 10.4.0
- `https://nuget.org/packages/Microsoft.Extensions.Caching.StackExchangeRedis` — Version 10.0.5
- `https://nuget.org/packages/StackExchange.Redis` — Version 2.12.14
- `https://nuget.org/packages/Polly.Extensions.Http` — Deprecated, do not use

### Secondary (MEDIUM confidence)
- `https://nuget.org/packages/RedisRateLimiting` — Version 1.2.1, December 2025
- `https://duffel.com/docs/api` — Duffel product list (Flights, Stays, Payments — car hire not available)
- `https://github.com/cristipufu/aspnetcore-redis-rate-limiting` — RedisRateLimiting library capabilities
- Hotelbeds `/hotels`, `/checkrates`, `/bookings` endpoint structure — from official developer portal navigation

### Tertiary (LOW confidence / ASSUMED)
- Sabre REST API URLs and BFM endpoint — not verifiable without sandbox credentials; drawn from project memory and community sources

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all NuGet versions verified against registry
- Amadeus API: HIGH — verified against official OpenAPI specification
- Sabre API details: LOW — developer portal not accessible in this session; URLs are assumed
- Hotelbeds HMAC formula: MEDIUM — formula confirmed from multiple community sources; not from official docs
- Architecture patterns: HIGH — keyed DI, HybridCache, Refit DelegatingHandler all verified from official Microsoft/library docs
- Pitfalls: HIGH (most are based on well-known .NET patterns) / MEDIUM (Hotelbeds clock skew)

**Research date:** 2026-04-12
**Valid until:** 2026-05-12 (stable libraries); verify Sabre URLs immediately when credentials are obtained
