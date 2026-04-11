# Stack Research: Travel Booking Engine

**Project:** Full travel booking engine (B2C, B2B, backoffice, CRM)
**Researched:** 2026-04-12
**Note:** Web search tools were unavailable. All findings are from training data (cutoff August 2025).
Version numbers marked [VERIFY] must be confirmed against nuget.org before pinning.

---

## Backend (.NET/C#)

### GDS / Inventory SDKs

#### Amadeus

**Situation:** Amadeus does not publish an official .NET SDK on NuGet for their REST API (NDC/Self-Service). Their officially-maintained SDKs are for Node.js, Python, Java, PHP, and Ruby only (as of mid-2025).

**What to do — two approaches:**

**Option A: Hand-roll an Amadeus REST client (recommended for production)**

Use `Refit` (typed REST client generator) over `HttpClient` to call the Amadeus Self-Service REST API directly.

```
Refit                       ~7.0.0    [VERIFY]
Microsoft.Extensions.Http   8.0.x
Polly / Microsoft.Extensions.Http.Resilience  8.x
```

- Auth: OAuth2 client-credentials flow to `https://test.api.amadeus.com/v1/security/oauth2/token`
- Key endpoints you'll integrate: `/v2/shopping/flight-offers`, `/v1/booking/flight-orders`, `/v2/shopping/hotel-offers`
- Wrap each GDS as its own adapter class behind a shared `IFlightAvailabilityProvider` interface. This is critical — you will swap or combine GDS providers.

**Option B: Unofficial Amadeus .NET community wrapper**

Search NuGet for `Amadeus` — several community packages exist but none are officially maintained. Do not use these in production without auditing the source.

**Confidence: HIGH** — Absence of an official .NET SDK is a documented, persistent gap in the Amadeus developer portal.

---

#### Sabre

Sabre offers REST APIs (Sabre Dev Studio) and older SOAP-based APIs. No official .NET NuGet package.

- For REST (SynXis, NDC): Same Refit approach as Amadeus.
- For legacy SOAP (GDS PNR manipulation, cryptic fare shopping): Use `System.ServiceModel.Http` (WCF client) — auto-generate from WSDL.
- Package: `System.ServiceModel.Http` 6.x (ships with .NET, no extra install needed on .NET 8).

**Confidence: HIGH** — Sabre's SOAP API set is well-established; REST is available but legacy systems still require SOAP.

---

#### Galileo / Travelport

Travelport Universal API (uAPI) is SOAP-based. Use `System.ServiceModel.Http` with WSDL-generated proxies. Travelport has begun migrating to Travelport+ (JSON REST) but most integrations in production as of 2025 are still on uAPI SOAP.

**Confidence: MEDIUM** — Travelport+ adoption is accelerating but uAPI is still predominant.

---

#### Duffel (NDC airline aggregator)

Duffel does not maintain an official .NET SDK. Use Refit against their REST API (`https://api.duffel.com`).

```
Refit  ~7.0.0  [VERIFY]
```

Their API uses `Duffel-Version` header versioning — pin this in your Refit client.

**Confidence: HIGH** — Confirmed from Duffel developer docs as of 2025 (no .NET SDK listed).

---

#### Hotelbeds (hotel aggregator)

No official .NET NuGet. REST JSON API with HMAC-SHA256 signature authentication.

Use `Refit` + custom `DelegatingHandler` for HMAC signing. The signature pattern:
```
SHA256(ApiKey + SharedSecret + UnixTimestampSeconds)
```
This handler should be registered as a named `HttpClient` in DI.

**Confidence: HIGH** — Hotelbeds API auth is well-documented; .NET SDK absence is confirmed.

---

#### Shared adapter pattern (prescriptive)

Define provider-agnostic domain interfaces in a `TBE.Domain.Contracts` project:

```csharp
public interface IFlightAvailabilityProvider { Task<FlightSearchResult> SearchAsync(FlightSearchRequest request, CancellationToken ct); }
public interface IHotelAvailabilityProvider  { Task<HotelSearchResult>  SearchAsync(HotelSearchRequest  request, CancellationToken ct); }
```

Each GDS/aggregator lives in its own adapter assembly (`TBE.Adapters.Amadeus`, `TBE.Adapters.Duffel`, etc.) registered as keyed services in .NET 8 DI. The inventory aggregation service calls multiple providers in parallel with `Task.WhenAll` and merges/deduplicates results.

---

### API Gateway

**Recommendation: YARP (Yet Another Reverse Proxy)**

| | YARP | Ocelot |
|---|---|---|
| Maintainer | Microsoft | Community |
| .NET 8 support | First-class | Supported but slower updates |
| Config style | Code + JSON | JSON only |
| Performance | Higher (Kestrel-native) | Lower |
| Active development | YES (2025) | Slowing down |

**Package:**
```
Yarp.ReverseProxy  ~2.1.0  [VERIFY]
```

Install in a dedicated `TBE.ApiGateway` project:

```bash
dotnet add package Yarp.ReverseProxy
```

Use YARP for:
- Route all B2C traffic through one gateway, B2B through another (or use route matching).
- Rate limiting per client (`Microsoft.AspNetCore.RateLimiting` — built into .NET 8, no extra package).
- JWT validation at the gateway (delegate to Keycloak JWKS endpoint).
- Header forwarding, circuit breaking via Polly.

**Do NOT use Ocelot** for new projects in 2025. It has not kept pace with .NET 8 features and active maintainers are few.

**Confidence: HIGH** — YARP is the Microsoft-backed solution with active 2025 releases.

---

### Messaging (RabbitMQ / MassTransit)

**Recommendation: MassTransit 8.x over RabbitMQ**

```
MassTransit                 ~8.3.x  [VERIFY]
MassTransit.RabbitMQ        ~8.3.x  [VERIFY]
```

Do NOT use RabbitMQ.Client directly — MassTransit gives you:
- Consumer/producer abstractions decoupled from transport
- Saga state machines (critical for booking workflows: search → hold → payment → confirm → ticket)
- Outbox pattern (prevents dual-write between DB and message broker)
- Dead-letter queue handling out of the box
- Easy swap to Azure Service Bus in future if needed

**Key patterns for travel:**

**Booking saga (use MassTransit Saga / StateMachine):**
```
BookingInitiated → PriceReconfirmed → PaymentProcessed → SupplierBooked → TicketIssued
                                    → PaymentFailed → Released
                                    → SupplierBookingFailed → Refunded
```

Use `MassTransit.EntityFrameworkCore` for saga persistence:
```
MassTransit.EntityFrameworkCore  ~8.3.x  [VERIFY]
```

**Outbox:**
```csharp
services.AddMassTransit(x => {
    x.AddEntityFrameworkOutbox<BookingDbContext>(o => {
        o.UseSqlServer();
        o.UseBusOutbox();
    });
});
```

**Key exchanges to define:**
- `booking.commands` — commands from API to booking service
- `booking.events` — events consumed by notification, CRM, reporting services
- `inventory.search.requests` — fan-out to GDS adapters
- `pricing.events` — price reconfirmation results

**Confidence: HIGH** — MassTransit 8.x with .NET 8 is the de facto standard for .NET microservice messaging in 2025.

---

### Auth

**Recommendation: Keycloak (external) + `Microsoft.AspNetCore.Authentication.JwtBearer`**

Do NOT use Duende IdentityServer for a new build in 2025. Duende requires a commercial license for production use (enforced from v6+, ~$1,500/year minimum). For a single-tenant product you own, Keycloak is free and operationally equivalent.

**Keycloak setup:**
- Run as Docker container (official image: `quay.io/keycloak/keycloak:25.x`).
- Realms: `tbe-b2c` (customers), `tbe-b2b` (agents), `tbe-backoffice` (staff).
- OIDC/OAuth2 with PKCE for the Next.js frontend.
- Client credentials flow for service-to-service calls.

**NuGet packages for service-side JWT validation:**
```
Microsoft.AspNetCore.Authentication.JwtBearer  8.0.x  (built into .NET 8 SDK, no separate install)
```

**NuGet for acquiring tokens in service-to-service calls:**
```
Microsoft.Identity.Client  ~4.x  [VERIFY] — or use HttpClient directly for client_credentials
```

For simpler service-to-service token acquisition:
```
Duende.AccessTokenManagement  ~3.x  [VERIFY] — token management WITHOUT IdentityServer server license
```
This library handles token caching and refresh for outbound calls and does NOT require a Duende server license.

**Next.js frontend auth:**
```
next-auth  ~5.0.x (Auth.js v5)  [VERIFY]
```
Use the Keycloak OIDC provider. Auth.js v5 has a stable API on Next.js 14/15.

**Role model:**
- Keycloak roles: `customer`, `agent`, `agent-manager`, `backoffice-operator`, `backoffice-admin`, `finance`
- Map to .NET claims via JWT role claims and use `[Authorize(Roles = "...")]`.

**Confidence: HIGH (Keycloak approach), MEDIUM (specific Duende.AccessTokenManagement version)**

---

### Payment

**Recommendation: Stripe.net for B2C; custom credit wallet for B2B**

**B2C — Stripe:**
```
Stripe.net  ~46.x  [VERIFY]
```

Use Stripe Payment Intents (not Charges — Charges API is legacy):
- 3D Secure handled automatically by Payment Intents.
- Use Stripe webhooks for asynchronous confirmation (never trust client-side redirect alone).
- Store only `stripe_payment_intent_id` and `stripe_customer_id` in your DB, never raw card data.

```csharp
// Webhook validation
var stripeEvent = EventUtility.ConstructEvent(
    json, Request.Headers["Stripe-Signature"], webhookSecret);
```

Register a background webhook processor via MassTransit consumers — do not process in the HTTP handler.

**B2B — Credit Wallet:**

Build this in-house. Schema:
```sql
AgentWallets (WalletId, AgentId, CreditLimit, AvailableBalance, Currency)
WalletTransactions (TxId, WalletId, BookingId, Amount, Type, Timestamp, BalanceBefore, BalanceAfter)
```

Use serializable transactions or optimistic concurrency with `xact_abort` to prevent overdrafts:
```sql
BEGIN TRANSACTION WITH (SERIALIZABLE)
  SELECT AvailableBalance FROM AgentWallets WHERE AgentId = @id
  IF @balance >= @amount
    UPDATE ...
    INSERT WalletTransaction ...
COMMIT
```

Do not use EF Core for wallet deductions — use stored procedures or raw SQL to guarantee isolation level.

**Confidence: HIGH (Stripe), HIGH (custom wallet pattern)**

---

## Frontend (Next.js)

### Search UI Patterns

**Framework:** Next.js 14/15 with App Router. Do not use Pages Router for new builds.

**Search availability results — client-side fetch, not SSR**

This is the most important architectural decision for search UIs:

- SSR (Server Components) for: initial page shell, SEO landing pages, static fare displays, destination pages.
- Client-side fetch for: live availability search results, price polling, seat maps, hotel room grid.

**Why not SSR for availability:**
- GDS availability calls take 2–15 seconds. Server-side streaming helps but does not eliminate the wait.
- Results must be re-fetchable (filter/sort without full page reload).
- Price reconfirmation requires client-side re-fetch.
- GDS rate limits are per-session — server-side calls lose session context.

**Recommended pattern:**

```
Search page (Server Component — renders form, metadata, SEO shell)
  └── <SearchResultsPanel> (Client Component — "use client")
        └── SWR or TanStack Query (manages fetch, loading, error, refetch)
```

Use `SWR` or `TanStack Query (React Query) v5` — both work well, TanStack Query has richer cache control.

```
@tanstack/react-query  ~5.x  [VERIFY]
```

**Progressive disclosure pattern:**
1. Show skeleton loaders immediately.
2. Stream partial results as GDS adapters return (use Server-Sent Events or WebSocket from your inventory service).
3. Show "X more results loading..." while secondary GDS completes.

For streaming, use Next.js Route Handlers with `ReadableStream` + `text/event-stream` — no additional packages.

**Confidence: HIGH**

---

### State Management

**Recommendation: Zustand for global UI state + TanStack Query for server state**

Do not use Redux for this. Travel search UIs have complex ephemeral state (search params, selected itinerary legs, passenger details in multi-step booking flow) — Zustand handles this cleanly.

```
zustand          ~4.x  [VERIFY]
@tanstack/react-query  ~5.x  [VERIFY]
```

**Store split:**
- `useSearchStore` — current search parameters, selected flights/hotels, filter state
- `useBookingStore` — passenger details, payment method, booking step
- `useSessionStore` — agent credentials, selected customer (B2B context)

TanStack Query owns all server data (availability, pricing, booking status). Zustand owns UI-only state that doesn't need to be fetched.

**Do NOT put availability results in Zustand.** They belong in TanStack Query cache with a short TTL (5–10 minutes for GDS pricing).

**Confidence: HIGH**

---

## Data Layer

### MSSQL Schema Approach

**Recommendation: Normalized + Event Sourcing for bookings specifically; normalized relational for everything else**

Full event sourcing for the entire domain is over-engineering for a single-tenant travel business. Use it only where audit trail and state replay genuinely matter — which is the booking lifecycle.

**Hybrid approach:**

**Booking service — event-sourced:**

```sql
-- Event store table
BookingEvents (
    EventId       UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    BookingId     UNIQUEIDENTIFIER NOT NULL,
    EventType     NVARCHAR(100) NOT NULL,   -- 'BookingCreated', 'PaymentReceived', etc.
    EventData     NVARCHAR(MAX) NOT NULL,   -- JSON payload
    OccurredAt    DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    SequenceNo    INT NOT NULL,
    INDEX IX_BookingEvents_BookingId (BookingId, SequenceNo)
)

-- Projection table (read model — rebuilt from events)
BookingReadModel (
    BookingId     UNIQUEIDENTIFIER PRIMARY KEY,
    Status        NVARCHAR(50) NOT NULL,
    TotalAmount   DECIMAL(18,4) NOT NULL,
    Currency      CHAR(3) NOT NULL,
    LastUpdated   DATETIME2 NOT NULL,
    SnapshotJson  NVARCHAR(MAX) NOT NULL   -- full booking state as JSON
)
```

**All other services — standard normalized relational:**

```sql
-- Customer service example
Customers (CustomerId, Email, FirstName, LastName, Title, DateOfBirth, NationalityCode, CreatedAt, UpdatedAt)
CustomerPassports (PassportId, CustomerId, Number, IssuingCountry, ExpiryDate, IsPrimary)
CustomerPaymentMethods (PmId, CustomerId, StripePaymentMethodId, Last4, Brand, IsDefault)

-- Agent/B2B
Agencies (AgencyId, Name, CreditLimit, CurrencyCode, IsActive)
Agents (AgentId, AgencyId, Email, Role, IsActive)
AgentWallets (WalletId, AgencyId, AvailableBalance, Currency)
WalletTransactions (TxId, WalletId, BookingId, Amount, Type, OccurredAt, BalanceBefore, BalanceAfter)

-- Product catalogue
Airports (IataCode PK, Name, CityCode, CountryCode, Timezone)
Airlines (IataCode PK, Name, AllianceCode)
Hotels (HotelCode PK, Source, Name, StarRating, Latitude, Longitude, DestinationId)
```

**ORM: Entity Framework Core 8**
```
Microsoft.EntityFrameworkCore.SqlServer  ~8.0.x  [VERIFY]
Microsoft.EntityFrameworkCore.Tools     ~8.0.x  [VERIFY]
```

For event store writes and wallet deductions, bypass EF Core and use Dapper for raw SQL with explicit transaction control:
```
Dapper  ~2.1.x  [VERIFY]
```

**Confidence: HIGH (pattern), MEDIUM (specific table structures — will evolve with GDS contract shapes)**

---

### Redis Patterns

**Package:**
```
StackExchange.Redis  ~2.7.x  [VERIFY]
```

Use via `Microsoft.Extensions.Caching.StackExchangeRedis` for the `IDistributedCache` abstraction where sufficient, or directly via `IConnectionMultiplexer` for advanced patterns.

**Pattern 1: GDS search result caching**

GDS availability is expensive (cost-per-call on some GDS contracts) and slow. Cache aggressively with short TTL.

```
Key pattern:  flight:avail:{origin}:{dest}:{departDate}:{cabinClass}:{paxType}:{paxCount}:{sourceGDS}
TTL:          5 minutes (pricing can shift; balance cost vs freshness)
Serialiser:   System.Text.Json or MessagePack (MessagePack is ~3x smaller/faster for large avail payloads)
```

```
MessagePack  ~2.5.x  [VERIFY]  — recommended over JSON for Redis values in high-volume search
```

Cache per GDS source separately — Amadeus and Sabre may return different fares for the same route.

**Pattern 2: Session management**

Use Redis as the ASP.NET Core distributed session store for the B2C portal's booking session (passenger details, selected itinerary, hold PNR reference):

```csharp
services.AddStackExchangeRedisCache(options => {
    options.Configuration = config["Redis:ConnectionString"];
    options.InstanceName = "tbe:session:";
});
services.AddSession(options => {
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
});
```

B2B agent sessions: use Keycloak tokens (JWTs) — no server-side session needed.

**Pattern 3: Rate limiting GDS calls**

GDS contracts specify rate limits (e.g., Amadeus Self-Service: 10 TPS on test, varies on prod). Use Redis sliding window rate limiter per GDS provider:

```
Key pattern:  ratelimit:gds:{providerName}:{window}
Algorithm:    Sliding window counter using Redis INCR + EXPIRE
```

Use the `RedisRateLimiter` available in community packages, or build one using `IDistributedCache` + Lua scripts for atomicity. The built-in `System.Threading.RateLimiting` (token bucket) can be layered as an in-process pre-check before the Redis check.

**Pattern 4: Price lock / hold tokens**

When a customer selects a fare before payment:
```
Key:   pricelock:{bookingSessionId}
Value: {fareSnapshotJson}
TTL:   15 minutes (or GDS fare hold expiry, whichever is shorter)
```

Check Redis before re-querying GDS at payment confirmation.

**Pattern 5: Idempotency keys for payment**

```
Key:   idempotency:{stripeIdempotencyKey}
TTL:   24 hours
```

Prevents double-charge if the client retries a payment request.

**Confidence: HIGH**

---

## Infrastructure

### Docker Strategy

**Recommendation: Docker Compose for development and staging; Kubernetes only when scaling requires it**

For a single-tenant travel business (own travel operations), Docker Compose is the right choice to start. Here is the honest assessment:

**Docker Compose — use now:**
- Services: API Gateway, FlightService, HotelService, BookingService, CrmService, BackofficeService, NotificationService
- Supporting: MSSQL, RabbitMQ, Redis, Keycloak
- Compose handles networking, volume mounts, dependency ordering
- Local dev parity with staging/production
- Operational complexity is low — one operator can manage it

**When to consider Kubernetes:**
- Multiple simultaneous customers on the platform (not applicable — single-tenant)
- Need for independent service scaling (e.g., FlightService taking 10x more traffic than CrmService in a high-traffic season)
- Team has DevOps capacity to manage it
- Current scale: if you process fewer than 500 bookings/day, Compose on a VM is sufficient

**Practical Compose structure for this project:**

```yaml
# docker-compose.yml (abbreviated)
services:
  api-gateway:
    build: ./src/TBE.ApiGateway
    ports: ["5000:8080"]
    depends_on: [rabbitmq, redis, keycloak]

  flight-service:
    build: ./src/TBE.Services.Flight
    depends_on: [mssql, rabbitmq, redis]

  hotel-service:
    build: ./src/TBE.Services.Hotel
    depends_on: [mssql, rabbitmq, redis]

  booking-service:
    build: ./src/TBE.Services.Booking
    depends_on: [mssql, rabbitmq, redis]

  notification-service:
    build: ./src/TBE.Services.Notification
    depends_on: [rabbitmq]

  mssql:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      ACCEPT_EULA: Y
      SA_PASSWORD: ${MSSQL_SA_PASSWORD}
    volumes: [mssql-data:/var/opt/mssql]

  rabbitmq:
    image: rabbitmq:3.13-management
    ports: ["5672:5672", "15672:15672"]
    volumes: [rabbitmq-data:/var/lib/rabbitmq]

  redis:
    image: redis:7.2-alpine
    command: redis-server --appendonly yes
    volumes: [redis-data:/data]

  keycloak:
    image: quay.io/keycloak/keycloak:25.0
    command: start-dev
    environment:
      KC_DB: mssql
      KC_DB_URL: jdbc:sqlserver://mssql:1433;databaseName=keycloak
      KC_DB_USERNAME: keycloak
      KC_DB_PASSWORD: ${KEYCLOAK_DB_PASSWORD}
      KEYCLOAK_ADMIN: admin
      KEYCLOAK_ADMIN_PASSWORD: ${KEYCLOAK_ADMIN_PASSWORD}
    ports: ["8080:8080"]
    depends_on: [mssql]

  nextjs-frontend:
    build: ./src/TBE.Web
    ports: ["3000:3000"]
    depends_on: [api-gateway]
```

Use `.env` files per environment (`docker-compose.override.yml` for local dev). Never commit secrets.

**Multi-stage Dockerfile for .NET services:**
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "TBE.Services.Flight.dll"]
```

Use `mcr.microsoft.com/dotnet/aspnet:8.0` (not `sdk`) as the runtime image — roughly 3x smaller.

**Confidence: HIGH**

---

## Confidence Notes

| Area | Confidence | Basis | Action Required |
|------|------------|-------|-----------------|
| Amadeus/Sabre/Duffel no .NET SDK | HIGH | Persistent, documented gap | Confirm on their dev portals before starting |
| Hotelbeds HMAC auth pattern | HIGH | Documented in Hotelbeds API docs | Test against sandbox early |
| YARP over Ocelot | HIGH | Microsoft backing, active releases | Pin version via nuget.org |
| MassTransit 8.x | HIGH | De facto standard, widely used | Confirm `~8.3.x` on nuget.org |
| MassTransit saga for booking | HIGH | Well-documented pattern | Prototype the saga early — it shapes the whole booking flow |
| Keycloak over IdentityServer | HIGH | Duende licensing constraint is real | Budget for Keycloak ops (it's an extra service) |
| next-auth v5 stability | MEDIUM | Was in beta/RC as of mid-2025 | Check current stable release — may need to pin to v4 |
| Stripe.net version | MEDIUM | API is stable; version number moves fast | Check nuget.org for current major |
| MessagePack for Redis serialisation | MEDIUM | Performance benefit documented; adds serialisation contract complexity | Benchmark vs System.Text.Json in context |
| EF Core 8 + Dapper hybrid | HIGH | Established pattern for mixed ORM/raw SQL | Standard — no risk |
| Docker Compose over K8s | HIGH | Single-tenant scale does not justify K8s overhead | Revisit if platform grows multi-tenant |
| Next.js App Router + TanStack Query | HIGH | Stable and widely adopted in 2025 | Confirm TanStack Query v5 compatibility with target Next.js version |
| Redis sliding window rate limiter | MEDIUM | Pattern is correct; implementation options vary | Test Lua atomicity approach under load |
| Event sourcing for booking only | HIGH | Full ES for whole domain is over-engineering at this scale | Validate event schema before going live |

### Versions to Validate on nuget.org Before Pinning

```
Yarp.ReverseProxy
MassTransit
MassTransit.RabbitMQ
MassTransit.EntityFrameworkCore
Microsoft.EntityFrameworkCore.SqlServer
Dapper
StackExchange.Redis
Stripe.net
Refit
MessagePack
Duende.AccessTokenManagement
```

### Versions to Validate on npmjs.com Before Pinning

```
next
next-auth (Auth.js)
@tanstack/react-query
zustand
```
