# Architecture Research: Travel Booking Engine (TBE)

**Domain:** Full-stack travel booking platform (B2C + B2B + Backoffice + CRM)
**Researched:** 2026-04-12
**Overall Confidence:** HIGH — patterns drawn from well-established travel platform architecture, MassTransit official docs, GDS integration industry practice, and .NET microservice patterns.

---

## Recommended Service Boundaries

Service boundaries follow **domain-driven design** (DDD) bounded contexts. Each service owns its data exclusively and exposes it through API or events — no cross-service database queries.

### Core Services (11 services)

---

#### 1. Identity Service
**Responsibility:** Authentication and authorization for all portals (B2C, B2B, backoffice).
**Owns:** Users, roles, permissions, agency memberships, session tokens, OAuth2 clients.
**DB:** `identity_db` (MSSQL)
**Exposes:** REST (token issuance, user CRUD), events (`UserRegistered`, `UserDeactivated`, `AgentApproved`)
**Tech:** ASP.NET Core + Keycloak (or IdentityServer/Duende) backed by MSSQL. Keycloak handles the OIDC/OAuth2 protocol surface so you don't rebuild it.
**Why separate:** Auth crosses all portals but must not be coupled to booking logic. Separate deployment allows hardening (WAF, rate limiting, MFA) independently.

---

#### 2. Search Service
**Responsibility:** Fan-out search requests to GDS connectors and third-party APIs, aggregate and normalize results, cache results.
**Owns:** Search session state, cached results. Does NOT own bookings.
**DB:** Redis only (no relational store — results are ephemeral). Search session IDs in Redis with 30-min TTL.
**Exposes:** REST (synchronous — search must return results to the browser)
**Tech:** ASP.NET Core, Polly (resilience/retry), Redis for result caching. Each GDS/API is a pluggable adapter behind an `IInventoryConnector` interface.
**Why separate:** Search is the highest-volume operation, needs independent scaling, and has completely different performance characteristics from transactional booking services. GDS search is rate-limited and expensive — isolation lets you cache aggressively without coupling to booking logic.
**Scaling note:** Scale out horizontally behind the API gateway. Redis result caching is mandatory — GDS charges per search transaction.

---

#### 3. Flight Connector Service (per GDS or grouped)
**Responsibility:** Speaks native GDS protocol (Amadeus REST API v2, Sabre REST/SOAP, Galileo Travelport Universal API). Translates raw GDS responses into the Unified Inventory Model (see Section 3).
**Owns:** Nothing persistent. Stateless adapter.
**Exposes:** Internal REST only (called by Search Service and Booking Service). Never exposed externally.
**Tech:** ASP.NET Core. Use Amadeus .NET SDK for Amadeus. Sabre/Galileo require direct SOAP or REST client construction.
**Why separate:** GDS credentials, rate limits, and protocol differences need independent management. If Amadeus is down, you can fail gracefully and fall back to Sabre without the Search Service knowing GDS internals.
**Note:** You may start with one connector per GDS (3 services: `tbe-connector-amadeus`, `tbe-connector-sabre`, `tbe-connector-galileo`) or group into a single `FlightConnectorService` that routes by preferred GDS. Separate deployments are safer for credential isolation and independent restarts.

---

#### 4. Hotel/Car Connector Service
**Responsibility:** Integrates third-party hotel aggregators (Hotelbeds, RateHawk, etc.) and car/transfer suppliers (Rentalcars, TBO, etc.). Same adapter pattern as flight connectors.
**Owns:** Nothing persistent. Stateless.
**Exposes:** Internal REST only.
**Tech:** ASP.NET Core, Refit (typed HTTP clients), Polly.
**Why separate:** Hotel/car APIs have different auth schemes (API keys, OAuth), rate limits, and response formats. Isolating them means their outage doesn't affect flights.

---

#### 5. Booking Service
**Responsibility:** Core booking lifecycle orchestrator. Creates and manages bookings from hold through confirmation. Owns the saga state machine.
**Owns:** `bookings` table (PNR reference, product snapshot, pricing snapshot, supplier references, status), `booking_items` (each product line), `booking_events` (event log for audit). This is the most critical data store.
**DB:** `booking_db` (MSSQL). Separate schema from all other services.
**Exposes:** REST (create booking, get booking, cancel, modify) + publishes events to RabbitMQ (`BookingCreated`, `BookingConfirmed`, `BookingCancelled`, `BookingFailed`, `TicketIssued`)
**Tech:** ASP.NET Core + MassTransit (saga state machine) + Entity Framework Core.
**Why separate:** Bookings are the core business asset. Isolation means payment failures, search overload, or CRM issues never corrupt booking state.

---

#### 6. Payment Service
**Responsibility:** Processes card payments (Stripe for B2C) and credit wallet debits (B2B). Handles refunds and wallet top-ups.
**Owns:** `payments` table (payment intent IDs, amounts, status, booking reference), `wallet_accounts` (B2B agent balances, transactions).
**DB:** `payment_db` (MSSQL). PCI-DSS scope isolation — this service is the only one that ever touches card data references.
**Exposes:** REST (internal only — called by Booking Service saga via HTTP) + subscribes to `BookingCancelled` to trigger refunds.
**Tech:** ASP.NET Core + Stripe.NET SDK. Idempotency keys on all Stripe calls (essential — network retries must not double-charge).
**Why separate:** PCI-DSS scoping. Isolating card-handling to one service dramatically reduces the PCI audit surface. No other service ever touches payment credentials or card tokens.
**Compliance:** Store only Stripe PaymentIntent IDs, never raw card numbers. Webhook endpoint for Stripe events must be authenticated with Stripe signature verification.

---

#### 7. Pricing & Fare Rules Service
**Responsibility:** Applies markup rules, agent commission calculations, promotional pricing, and tax breakdowns. Returns final sell prices from supplier net rates.
**Owns:** `markup_rules`, `agent_commission_profiles`, `promotions`, `tax_configs`.
**DB:** `pricing_db` (MSSQL)
**Exposes:** Internal REST only (called by Search Service to decorate results before returning to client, and by Booking Service to price-lock at booking time).
**Why separate:** Pricing logic changes frequently (seasonal markups, agent tier changes, promotions). Isolating it means pricing changes deploy without touching search or booking services. Agent-specific pricing (B2B) vs public pricing (B2C) lives entirely here.

---

#### 8. Notification Service
**Responsibility:** Sends booking confirmation emails, itinerary PDFs, modification and cancellation notices, agent wallet alerts.
**Owns:** `notification_log` (idempotent tracking — never send twice), `email_templates`.
**DB:** `notification_db` (MSSQL, lightweight)
**Exposes:** Consumes RabbitMQ events only. No REST API.
**Tech:** ASP.NET Core worker service + MassTransit consumer + SendGrid/SMTP. Generate itinerary PDFs with QuestPDF or similar.
**Why separate:** Email delivery is async, non-critical-path, and involves third-party SMTP services. Failure here must never block a booking from completing.

---

#### 9. CRM Service
**Responsibility:** Customer profiles, agent/agency profiles, booking history view (read-only projection), communication logs, follow-up tasks.
**Owns:** `customers`, `agents`, `agencies`, `communication_logs`, `booking_projections` (denormalized read model built from booking events).
**DB:** `crm_db` (MSSQL)
**Exposes:** REST (B2C account portal, B2B agent portal, backoffice CRM views) + subscribes to `BookingConfirmed`, `BookingCancelled`, `UserRegistered` events.
**Why separate:** CRM reads from booking events rather than querying the booking database directly. This decouples CRM reporting from booking transaction performance. CRM can have its own denormalized schema optimized for read.

---

#### 10. Backoffice Service
**Responsibility:** Manual booking entry, booking management for ops staff, supplier contract management, MIS reporting, reissue/refund workflows.
**Owns:** `manual_bookings`, `supplier_contracts`, `reissue_requests`.
**DB:** `backoffice_db` (MSSQL) — queries booking read-models via CRM projections or direct event subscription for MIS.
**Exposes:** REST (consumed by Next.js backoffice frontend).
**Why separate:** Backoffice has different availability requirements (internal tool — can tolerate maintenance windows), different auth (staff only), and different workflows (manual, offline) that must not slow or block the customer-facing booking path.

---

#### 11. API Gateway
**Responsibility:** Single ingress point. Routes, authenticates, rate-limits, and transforms traffic for B2C, B2B, and backoffice.
**Tech:** YARP (Yet Another Reverse Proxy) — Microsoft's first-party .NET reverse proxy. Alternatively: Ocelot (simpler, widely used in .NET microservices). For production scale, consider Kong or AWS API Gateway in front of YARP.
**Why YARP over Ocelot:** YARP is actively maintained by Microsoft, has better performance, supports middleware pipeline natively in ASP.NET Core. Ocelot is older and slower to receive updates.

---

### Service Summary Table

| Service | DB | Sync/Async | External Traffic |
|---|---|---|---|
| Identity Service | `identity_db` | REST (sync) | Yes (all portals) |
| Search Service | Redis | REST (sync) | Yes (B2C, B2B) |
| Flight Connectors (x3 GDS) | None | REST (sync, internal) | No |
| Hotel/Car Connectors | None | REST (sync, internal) | No |
| Booking Service | `booking_db` | REST + RabbitMQ | Yes (B2C, B2B) |
| Payment Service | `payment_db` | REST (internal) + webhook | Stripe webhook only |
| Pricing Service | `pricing_db` | REST (sync, internal) | No |
| Notification Service | `notification_db` | RabbitMQ (async only) | No |
| CRM Service | `crm_db` | REST + RabbitMQ | Yes (portals, backoffice) |
| Backoffice Service | `backoffice_db` | REST | Yes (backoffice only) |
| API Gateway | None | REST proxy | Yes (all) |

---

## Booking Saga Pattern

The booking flow in travel is inherently a **distributed transaction**: multiple external systems (GDS, payment provider, ticketing system) must all succeed or all be compensated. This is the textbook use case for the Saga pattern.

### Booking Flow Steps

```
1. SearchResults returned (cached in Redis, session ID issued)
2. Customer selects itinerary → POST /bookings (initiate saga)
3. [Hold/PNR Creation]   → Call GDS to hold seats/rooms (PNR created, time-limited ~15 min)
4. [Price Lock]          → Re-validate price with Pricing Service (price may have changed)
5. [Payment Capture]     → Charge card via Payment Service (Stripe PaymentIntent capture)
                           OR debit B2B wallet
6. [Supplier Confirm]    → Call GDS/supplier to confirm the booking (PNR moves to confirmed)
7. [Ticketing]           → Request e-ticket issuance (GDS ticketing command)
8. [Notify]              → Publish BookingConfirmed event → Notification Service sends email
9. [CRM Update]          → CRM Service consumes event, updates booking projection
```

### Compensation (Rollback) Steps

If any step fails, compensating transactions must undo previous steps:

```
Step 7 fails (ticketing) → Cancel supplier confirmation → Refund payment → Release PNR hold
Step 5 fails (payment)   → Release PNR hold → No refund needed (card not charged)
Step 3 fails (hold)      → Saga ends, surface error to customer, no compensations needed
```

### Orchestration with MassTransit

Use **MassTransit's SagaStateMachine** (orchestration pattern — see Section 7 for why orchestration beats choreography here).

**Saga State Machine Definition (C#):**

```csharp
public class BookingSagaState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; }

    // Booking data
    public Guid BookingId { get; set; }
    public string ProductType { get; set; }   // Flight, Hotel, Package, CarHire
    public string SupplierRef { get; set; }   // GDS PNR or supplier booking ref
    public decimal TotalAmount { get; set; }
    public string PaymentMethod { get; set; } // "card" or "wallet"
    public string PaymentIntentId { get; set; }
    public string CustomerId { get; set; }
    public string AgentId { get; set; }       // null for B2C

    // Timeouts
    public Guid HoldExpiryTokenId { get; set; }
}

public class BookingSagaStateMachine : MassTransitStateMachine<BookingSagaState>
{
    public State HoldPending { get; private set; }
    public State PriceValidating { get; private set; }
    public State PaymentPending { get; private set; }
    public State ConfirmationPending { get; private set; }
    public State TicketingPending { get; private set; }
    public State Confirmed { get; private set; }
    public State Failed { get; private set; }
    public State Compensating { get; private set; }

    public BookingSagaStateMachine()
    {
        InstanceState(x => x.CurrentState);

        // Transitions
        Initially(
            When(BookingInitiated)
                .Then(ctx => InitializeBooking(ctx))
                .Publish(ctx => new RequestInventoryHold { BookingId = ctx.Saga.BookingId, ... })
                .TransitionTo(HoldPending)
        );

        During(HoldPending,
            When(HoldSucceeded)
                .Publish(ctx => new RequestPriceValidation { ... })
                .TransitionTo(PriceValidating),
            When(HoldFailed)
                .Publish(ctx => new BookingFailed { Reason = "HoldFailed" })
                .TransitionTo(Failed)
                .Finalize()
        );

        During(PriceValidating,
            When(PriceValidated)
                .Publish(ctx => new RequestPayment { ... })
                .TransitionTo(PaymentPending),
            When(PriceChanged)
                .Publish(ctx => new ReleasePnrHold { ... }) // compensate
                .Publish(ctx => new BookingFailed { Reason = "PriceChanged" })
                .TransitionTo(Failed)
                .Finalize()
        );

        During(PaymentPending,
            When(PaymentSucceeded)
                .Publish(ctx => new RequestSupplierConfirm { ... })
                .TransitionTo(ConfirmationPending),
            When(PaymentFailed)
                .Publish(ctx => new ReleasePnrHold { ... }) // compensate
                .Publish(ctx => new BookingFailed { Reason = "PaymentFailed" })
                .TransitionTo(Failed)
                .Finalize()
        );

        During(ConfirmationPending,
            When(SupplierConfirmed)
                .Publish(ctx => new RequestTicketing { ... })
                .TransitionTo(TicketingPending),
            When(SupplierConfirmFailed)
                .Publish(ctx => new RefundPayment { ... })  // compensate
                .Publish(ctx => new ReleasePnrHold { ... }) // compensate
                .Publish(ctx => new BookingFailed { Reason = "SupplierConfirmFailed" })
                .TransitionTo(Compensating)
        );

        During(TicketingPending,
            When(TicketIssued)
                .Then(ctx => ctx.Saga.CurrentState = "Confirmed")
                .Publish(ctx => new BookingConfirmed { ... })
                .TransitionTo(Confirmed)
                .Finalize(),
            When(TicketingFailed)
                .Publish(ctx => new CancelSupplierBooking { ... }) // compensate
                .Publish(ctx => new RefundPayment { ... })          // compensate
                .TransitionTo(Compensating)
        );

        SetCompletedWhenFinalized();
    }
}
```

**MassTransit RabbitMQ Configuration:**

```csharp
// Program.cs in Booking Service
services.AddMassTransit(x =>
{
    x.AddSagaStateMachine<BookingSagaStateMachine, BookingSagaState>()
        .EntityFrameworkRepository(r =>
        {
            r.ConcurrencyMode = ConcurrencyMode.Pessimistic; // Essential for booking sagas
            r.AddDbContext<BookingSagaDbContext>((provider, builder) =>
                builder.UseSqlServer(connectionString));
        });

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host("rabbitmq://localhost", h =>
        {
            h.Username("tbe");
            h.Password("...");
        });
        cfg.ConfigureEndpoints(ctx);
    });
});
```

**Critical implementation notes:**
- Use **Pessimistic concurrency** for saga state (not optimistic) — concurrent booking attempts on same saga must not corrupt state.
- Persist saga state in **MSSQL** (not in-memory) — restarts must not lose in-flight bookings.
- Set **hold timeout** using MassTransit's `Schedule` — if payment is not received within 14 minutes (1 min before GDS hold expires), auto-cancel and release the PNR.
- Every message in the saga must be **idempotent** — RabbitMQ delivers at-least-once. Use `CorrelationId` + database upsert to prevent double-processing.
- Use **MassTransit's Outbox pattern** to guarantee saga events are published atomically with saga state saves (prevents ghost confirmations if the service crashes between DB write and RabbitMQ publish).

---

## Unified Search / Normalization Layer

### The Problem

Each inventory source returns results in a completely different format:
- Amadeus returns NDC/EDIFACT-derived JSON with fare families, segments, legs, fare bases.
- Sabre returns BargainFinder Max JSON with its own segment/pricing model.
- Hotelbeds returns rooms with rate plans, board types, cancellation policies in their own schema.
- Duffel returns offers with slices and segments in their OpenAPI schema.

The Search Service must normalize all of these into a single `UnifiedSearchResult` that the frontend consumes identically regardless of source.

### Unified Flight Result Model

```csharp
public class UnifiedFlightOffer
{
    public string OfferId { get; set; }          // Opaque ID, stored in Redis
    public string Source { get; set; }           // "amadeus" | "sabre" | "galileo" | "duffel"
    public string SourceBookingToken { get; set; } // Raw supplier token for booking

    public List<FlightSegment> OutboundSegments { get; set; }
    public List<FlightSegment>? InboundSegments { get; set; }   // null = one-way

    public PriceBreakdown Pricing { get; set; }
    public BaggageAllowance Baggage { get; set; }
    public string CabinClass { get; set; }       // Economy | Business | First
    public string FareBasis { get; set; }
    public FareRules Rules { get; set; }         // Refundable, changeable, penalties
    public bool IsNDC { get; set; }
}

public class FlightSegment
{
    public string FlightNumber { get; set; }
    public string OperatingCarrier { get; set; }
    public string MarketingCarrier { get; set; }
    public string DepartureAirport { get; set; }
    public string ArrivalAirport { get; set; }
    public DateTime DepartureTime { get; set; }  // Always UTC
    public DateTime ArrivalTime { get; set; }    // Always UTC
    public string AircraftType { get; set; }
    public int LayoverMinutes { get; set; }
}

public class PriceBreakdown
{
    public decimal Basefare { get; set; }
    public decimal Taxes { get; set; }
    public decimal Fees { get; set; }
    public decimal MarkupAmount { get; set; }    // Applied by Pricing Service
    public decimal TotalSellPrice { get; set; }
    public string Currency { get; set; }
    public decimal? AgentNetPrice { get; set; }  // B2B only, null for B2C
    public decimal? AgentCommission { get; set; }
}
```

### Normalization Pattern — Adapter per Connector

Each connector implements `IInventoryConnector`:

```csharp
public interface IInventoryConnector
{
    string ConnectorId { get; }           // "amadeus", "sabre", "hotelbeds"
    ProductType[] SupportedProducts { get; }
    Task<List<UnifiedFlightOffer>> SearchFlightsAsync(FlightSearchRequest request, CancellationToken ct);
    Task<UnifiedFlightOffer> GetOfferDetailsAsync(string sourceToken, CancellationToken ct);
    Task<HoldResponse> HoldAsync(string sourceToken, PassengerDetails[] passengers, CancellationToken ct);
    Task ConfirmAsync(string supplierRef, CancellationToken ct);
    Task CancelAsync(string supplierRef, CancellationToken ct);
}
```

Search Service resolves all registered `IInventoryConnector` implementations via DI, fans out requests in parallel (Task.WhenAll), merges results, and sends the merged list through the Pricing Service for markup decoration before caching in Redis.

### Fan-Out and Merge

```csharp
// SearchService.cs (simplified)
public async Task<SearchSessionResult> SearchFlightsAsync(FlightSearchRequest request)
{
    var sessionId = Guid.NewGuid().ToString();

    // Fan out to all enabled connectors in parallel
    var tasks = _connectors
        .Where(c => c.SupportedProducts.Contains(ProductType.Flight))
        .Select(c => c.SearchFlightsAsync(request, CancellationToken.None));

    var results = await Task.WhenAll(tasks);

    // Merge, deduplicate (same flight, different source — prefer cheaper)
    var merged = MergeAndRank(results.SelectMany(r => r).ToList());

    // Decorate with markup via Pricing Service
    var priced = await _pricingClient.ApplyMarkupAsync(merged, request.ChannelType);

    // Cache in Redis with TTL
    await _cache.SetAsync(sessionId, priced, TimeSpan.FromMinutes(30));

    return new SearchSessionResult { SessionId = sessionId, Offers = priced };
}
```

### Redis Caching Strategy

- Cache key: `search:{sessionId}` with 30-minute TTL.
- Store the full `UnifiedFlightOffer` list serialized as JSON (MessagePack preferred for size).
- At booking time, Booking Service retrieves the offer by `offerId` from the session cache to get the `SourceBookingToken` needed to call the GDS hold. If the session has expired, return a "session expired — search again" error (do not re-search silently — prices may differ).
- Never cache at the route level across users — GDS pricing is per-session and seat-class availability changes constantly.

---

## API Gateway Strategy

### Gateway Architecture

Use **YARP** (Microsoft.ReverseProxy) as the single API gateway. All Next.js frontend traffic goes through the gateway. Internal service-to-service communication goes directly (no gateway hop — unnecessary latency).

```
Internet
  │
  ▼
[YARP API Gateway :443]
  │
  ├── /api/b2c/**        → B2C routes (Search, Booking, Payment, CRM/Customer)
  ├── /api/b2b/**        → B2B routes (Search, Booking, Wallet, CRM/Agent)
  ├── /api/backoffice/** → Backoffice routes (Backoffice Service, CRM)
  ├── /api/auth/**       → Identity Service (token endpoints)
  └── /api/webhooks/**   → Stripe webhook (Payment Service) — no auth, Stripe sig only
```

### Route-Level Auth Policies

Configure YARP transforms to enforce JWT validation at the gateway layer before the request reaches any downstream service:

```json
// yarp.json (simplified)
{
  "ReverseProxy": {
    "Routes": {
      "b2c-search": {
        "ClusterId": "search-service",
        "Match": { "Path": "/api/b2c/search/{**remainder}" },
        "Metadata": { "RequiredRole": "Customer" }
      },
      "b2b-search": {
        "ClusterId": "search-service",
        "Match": { "Path": "/api/b2b/search/{**remainder}" },
        "Metadata": { "RequiredRole": "Agent" }
      },
      "backoffice-bookings": {
        "ClusterId": "backoffice-service",
        "Match": { "Path": "/api/backoffice/{**remainder}" },
        "Metadata": { "RequiredRole": "Staff" }
      }
    }
  }
}
```

Each downstream service still validates the JWT (defense in depth) but the gateway is the primary enforcement layer.

### Gateway Responsibilities

| Concern | Implementation |
|---|---|
| TLS termination | Gateway handles SSL, downstream plain HTTP inside Docker network |
| JWT validation | Validate signature against Keycloak JWKS endpoint, check `aud` and `iss` claims |
| Role enforcement | Check JWT `role` claim matches route policy before proxying |
| Rate limiting | ASP.NET Core Rate Limiting middleware on gateway — 60 req/min for B2C search, 300 req/min for B2B |
| Request ID propagation | Add `X-Request-Id` header to every proxied request for distributed tracing |
| CORS | Gateway handles CORS — next.js origin allowed, others denied |
| Stripe webhook routing | Route `/api/webhooks/stripe` directly to Payment Service, bypass JWT auth, only Stripe signature check |

### B2C vs B2B Traffic Differentiation

The channel type (B2C / B2B) is encoded in the JWT claim (`channel_type: "b2c"` or `channel_type: "b2b"`). Downstream services use this claim to:
- Apply the correct pricing tier (public vs agent net)
- Show/hide commission data
- Route payment to Stripe vs wallet deduction

Do not rely solely on the URL prefix for channel determination — use the JWT claim as the authoritative source of truth, verified cryptographically.

---

## Auth Architecture

### Recommended: Keycloak + JWT

Use **Keycloak** (open source identity provider) to handle OAuth2/OIDC. It replaces IdentityServer/Duende (which requires paid license for commercial use at scale) and gives you SSO across all three portals out of the box.

**Keycloak Realm Structure:**

```
Realm: tbe
├── Clients
│   ├── tbe-b2c-portal      (public client, PKCE, Next.js SPA)
│   ├── tbe-b2b-portal      (public client, PKCE, Next.js SPA)
│   ├── tbe-backoffice      (confidential client, Next.js server-side)
│   └── tbe-api-gateway     (service account, introspection)
│
├── Roles
│   ├── Customer            (assigned to B2C registered users)
│   ├── Agent               (assigned to approved travel agents)
│   ├── AgencyAdmin         (manages sub-agents within one agency)
│   ├── Staff               (backoffice operators)
│   └── SuperAdmin          (full backoffice access)
│
└── Identity Providers (optional future)
    └── Google (B2C social login)
```

**SSO Flow:**

All three portals share the same Keycloak realm. A user who is both an agent and a backoffice staff member gets multiple roles on the same identity. The OIDC session is shared — logging into one portal does not require re-authentication for another (SSO via session cookie on the Keycloak domain).

**JWT Claims Structure:**

```json
{
  "sub": "user-uuid",
  "email": "agent@agency.com",
  "realm_access": {
    "roles": ["Agent", "AgencyAdmin"]
  },
  "agency_id": "agency-uuid",
  "channel_type": "b2b",
  "wallet_account_id": "wallet-uuid",
  "iss": "https://auth.tbe.com/realms/tbe",
  "aud": "tbe-api-gateway",
  "exp": 1712345678
}
```

**Downstream Service Validation:**

Each microservice validates the JWT independently (no roundtrip to Keycloak on every request — use JWKS caching):

```csharp
// In each service's Program.cs
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://auth.tbe.com/realms/tbe";
        options.Audience = "tbe-api-gateway";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        // JWKS auto-refreshed by the library — no roundtrip per request
    });
```

**B2B Agent Onboarding Flow:**

1. Agent self-registers → gets `PendingApproval` role (can log in but not search/book).
2. Backoffice staff approves → Keycloak Admin API call upgrades role to `Agent`.
3. Wallet account created → Identity Service publishes `AgentApproved` event → Payment Service creates wallet account → Backoffice staff loads initial credit.

**Access Token Lifetime:** 15 minutes (short, for security). Refresh token: 8 hours (session length). For backoffice SSR, use server-side token refresh via Next.js API routes.

---

## Event Sourcing for Bookings

### Why Event Sourcing for Travel Bookings

Travel bookings have strict compliance requirements (IATA, airline dispute resolution, financial audit). You need an **immutable, append-only log** of every state change — not just the current state. Traditional UPDATE-in-place databases destroy the history needed for:
- Dispute resolution ("was the price shown before payment the same as what we charged?")
- Reissue/refund eligibility (which fare rules applied at time of booking?)
- Financial reconciliation (audit trail of every payment and refund)
- Regulatory requirements (some jurisdictions require 7-year booking history)

### Recommended Approach: Event Log + Read-Side Projection (not full ES framework)

Full event sourcing (Marten, EventStoreDB) is powerful but adds significant operational complexity. For TBE, use a **pragmatic hybrid**: maintain both the current-state record (for fast reads) AND an append-only event log in the same `booking_db`.

```sql
-- Current state (fast reads, updates in place)
CREATE TABLE bookings (
    booking_id      UNIQUEIDENTIFIER PRIMARY KEY,
    pnr             NVARCHAR(20),
    status          NVARCHAR(50),       -- Held | Confirmed | Cancelled | Ticketed
    product_type    NVARCHAR(20),
    passenger_count INT,
    total_amount    DECIMAL(12,4),
    currency        NVARCHAR(3),
    channel         NVARCHAR(10),       -- b2c | b2b
    customer_id     UNIQUEIDENTIFIER,
    agent_id        UNIQUEIDENTIFIER NULL,
    created_at      DATETIMEOFFSET,
    updated_at      DATETIMEOFFSET
);

-- Immutable event log (append-only, never UPDATE or DELETE)
CREATE TABLE booking_events (
    event_id        UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    booking_id      UNIQUEIDENTIFIER NOT NULL,
    event_type      NVARCHAR(100) NOT NULL,  -- BookingInitiated | HoldSucceeded | PaymentCaptured | etc.
    event_data      NVARCHAR(MAX) NOT NULL,  -- JSON snapshot of full state at event time
    occurred_at     DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    actor_id        UNIQUEIDENTIFIER,        -- Which user/system triggered this
    actor_type      NVARCHAR(50),            -- Customer | Agent | Staff | System
    saga_id         UNIQUEIDENTIFIER,        -- Correlation to saga instance
    source_ip       NVARCHAR(45),            -- For fraud/compliance
    CONSTRAINT FK_BookingEvents_Booking FOREIGN KEY (booking_id) REFERENCES bookings(booking_id)
);

-- Never allow UPDATE or DELETE on booking_events — enforce via:
-- 1. DENY UPDATE, DELETE ON booking_events TO application_role
-- 2. Application-level: BookingEventRepository has no Update/Delete methods
```

**What to store in `event_data`:**

Each event captures a complete snapshot of the relevant state at that moment, not just the delta. This means you can reconstruct what was known at any point in time:

```json
{
  "event": "PaymentCaptured",
  "payment_intent_id": "pi_xxx",
  "amount": 487.50,
  "currency": "GBP",
  "pricing_snapshot": {
    "base_fare": 380.00,
    "taxes": 82.50,
    "markup": 25.00,
    "sell_price": 487.50
  },
  "fare_rules_snapshot": "...",   // Full fare rules text at time of booking
  "stripe_response": { ... }      // Sanitized Stripe response (no card data)
}
```

**Key events to log:**

| Event | When | What to Capture |
|---|---|---|
| `BookingInitiated` | Saga starts | Search session ID, offer snapshot, passenger details |
| `HoldSucceeded` | GDS returns PNR | PNR, hold expiry, seat assignments |
| `PriceValidated` | Price re-checked | Final price, currency, markup applied |
| `PaymentCaptured` | Stripe charges card | PaymentIntent ID, amount, pricing snapshot |
| `WalletDebited` | B2B wallet deducted | Transaction ID, amount, remaining balance |
| `SupplierConfirmed` | GDS confirms booking | Supplier booking reference |
| `TicketIssued` | E-ticket generated | Ticket numbers per passenger |
| `BookingCancelled` | Any cancellation | Cancellation reason, refund amount, who cancelled |
| `RefundIssued` | Refund processed | Refund amount, method, timeline |
| `BookingModified` | Change made | Old vs new itinerary, repricing |
| `ManualOverride` | Staff action | What was changed, reason, staff ID |

### Read-Side Projections

The CRM Service subscribes to booking events via RabbitMQ and builds its own denormalized `booking_projections` table. This is the CQRS read side — CRM never queries `booking_db` directly.

---

## Service Communication Map

### Synchronous (REST) — Used When the Caller Needs the Response Immediately

| Caller | Callee | Call | Why Sync |
|---|---|---|---|
| API Gateway | All services | All external requests | Gateway proxy |
| Search Service | Flight Connectors | `SearchFlights()` | Need results before returning to client |
| Search Service | Hotel/Car Connectors | `SearchHotels()` | Need results before returning to client |
| Search Service | Pricing Service | `ApplyMarkup()` | Need priced results before caching |
| Booking Service | Pricing Service | `ValidatePrice()` | Need confirmed price before charging |
| Booking Service | Payment Service | `CapturePayment()` / `DebitWallet()` | Need payment result to continue saga |
| Booking Service | Flight Connectors | `HoldPNR()`, `Confirm()`, `Issue()` | Need supplier response to advance saga |
| Backoffice Service | Booking Service | `GetBooking()` | Read queries |
| Backoffice Service | CRM Service | `GetCustomer()`, `GetAgent()` | Read queries |
| All services | Identity Service | Token validation (via JWKS cache) | Auth |

**Note:** Booking Service calls Connectors and Payment Service **synchronously within saga steps** — but these calls are triggered by async RabbitMQ messages. The saga step itself is async (message-driven), but the external calls within a step are synchronous HTTP. This is the correct pattern: the saga coordinates asynchronously, each step executes synchronously.

### Asynchronous (RabbitMQ / MassTransit) — Used for Events and Saga Messages

| Publisher | Event | Subscribers | Why Async |
|---|---|---|---|
| API Gateway (via Booking Svc) | `BookingInitiated` | Booking Saga | Decouples HTTP response from booking processing |
| Booking Service | `BookingConfirmed` | Notification Service, CRM Service, Backoffice Service | Non-critical-path — email/CRM must not block booking |
| Booking Service | `BookingCancelled` | Notification Service, CRM Service, Payment Service | Refund trigger must be reliable but async |
| Booking Service | `BookingFailed` | Notification Service, CRM Service | Failure notification |
| Booking Service | `TicketIssued` | Notification Service | Trigger ticket email |
| Payment Service | `RefundIssued` | Notification Service, CRM Service | Notify customer and log |
| Identity Service | `UserRegistered` | CRM Service | Create customer profile |
| Identity Service | `AgentApproved` | Payment Service, Notification Service | Create wallet, send welcome email |
| Backoffice Service | `ManualBookingCreated` | Booking Service, Notification Service | Sync manual booking into main booking store |

### RabbitMQ Exchange/Queue Design

Use **topic exchanges** with one exchange per domain:

```
Exchange: tbe.booking.events    (topic)
  → Queue: booking.notification (binding: booking.#)
  → Queue: booking.crm          (binding: booking.confirmed, booking.cancelled)
  → Queue: booking.payment      (binding: booking.cancelled)  // refund trigger

Exchange: tbe.identity.events   (topic)
  → Queue: identity.crm         (binding: identity.#)
  → Queue: identity.payment     (binding: identity.agent.approved)
```

MassTransit auto-creates these exchanges and queues from consumer configuration — you don't configure them manually.

### What Must NEVER Be Async

| Operation | Why Must Be Sync |
|---|---|
| GDS seat hold | GDS hold has a time window — you need the PNR reference immediately to continue |
| Payment capture | You need Stripe's authorization response before confirming the booking |
| Price validation | Must know final price before charging — async would create race condition |
| JWT validation | Every request — cannot tolerate queue delay |

---

## Suggested Build Order (Dependencies)

Build order follows the dependency graph: nothing should depend on a service that doesn't exist yet. Infrastructure comes first, then data services, then business logic, then UI-facing services.

### Phase 1: Infrastructure Foundation
**Goal:** Running infrastructure, all services can be started with config.

1. **Docker Compose stack** — RabbitMQ, MSSQL, Redis, Keycloak containers. All with persistent volumes.
2. **Keycloak realm setup** — Realm, clients, roles configured. Identity Service wired to Keycloak Admin API.
3. **API Gateway (YARP)** — Bare routing config, JWT validation middleware. No backend services yet.
4. **Shared libraries** — NuGet packages or shared projects for: `UnifiedInventoryModel`, `DomainEvents`, `JwtClaimsExtensions`, `OutboxPattern`. All services will depend on these.

### Phase 2: Identity and Auth
**Goal:** Login works across all three portals.

5. **Identity Service** — User registration, login flow, role management, Keycloak integration.
6. **B2C Portal (auth only)** — Next.js with Keycloak PKCE login, token refresh, protected routes.
7. **B2B Portal (auth only)** — Same, agent login.
8. **Backoffice (auth only)** — Staff login.

### Phase 3: Inventory Connectivity
**Goal:** Can search real inventory, results normalize correctly.

9. **One Flight Connector (Amadeus first)** — Amadeus REST API, normalization to `UnifiedFlightOffer`. Unit test normalization exhaustively.
10. **Pricing Service** — Markup rules engine, B2C public pricing, B2B agent net pricing. No bookings yet — just pricing.
11. **Search Service** — Fan-out to Amadeus connector, merge, send to Pricing Service, cache in Redis. REST endpoint works.
12. **B2C search UI** — Next.js flight search form, results page. Real results from Amadeus.
13. **Add remaining connectors** — Sabre, Galileo, Hotel/Car connectors. Search now fans out to all.

### Phase 4: Booking and Payment
**Goal:** End-to-end booking works for flights.

14. **Booking Service + Saga** — MassTransit saga state machine, MSSQL saga persistence, hold/confirm/ticket steps.
15. **Payment Service** — Stripe integration (PaymentIntent create/capture), B2C checkout. Idempotency keys.
16. **Booking saga wired to Amadeus** — Full hold → pay → confirm → ticket flow for one flight.
17. **Notification Service** — Consume `BookingConfirmed` and `BookingFailed`, send emails. Booking confirmation email with itinerary.
18. **B2C booking UI** — Passenger details form, Stripe checkout, booking confirmation page.

### Phase 5: B2B Agent Portal
**Goal:** Agents can search, book, and manage credit.

19. **B2B wallet in Payment Service** — Wallet accounts, debit/credit, transaction history.
20. **B2B booking flow** — Same saga, different payment step (wallet debit instead of Stripe).
21. **B2B portal UI** — Agent search (with net pricing visible), booking flow, wallet balance display.
22. **Agent onboarding** — Registration, approval workflow, wallet top-up by backoffice.

### Phase 6: CRM and Backoffice
**Goal:** Ops can manage bookings, customers, and agents.

23. **CRM Service** — Event projections from booking/identity events. Customer and agent profiles.
24. **Backoffice Service** — Booking list/detail, manual booking entry, supplier contract config.
25. **Backoffice UI** — React admin views for booking management, agent approval, wallet management.
26. **MIS reporting** — Basic analytics dashboard (booking volumes, revenue, supplier mix).

### Phase 7: Resilience and Production Readiness
**Goal:** System survives failures gracefully.

27. **Saga compensation paths** — Test and implement all rollback scenarios end-to-end.
28. **Dead letter queues** — Handle poison messages in RabbitMQ, alert on DLQ growth.
29. **Distributed tracing** — OpenTelemetry + Jaeger/Seq across all services using `X-Request-Id`.
30. **GDS failover** — If Amadeus is down, fall back to Sabre for the same route.
31. **Load testing** — Simulate 100 concurrent searches, validate Redis cache hit rate, identify bottlenecks.

---

## Additional Critical Decisions

### PNR Hold Time Pressure

GDS holds expire (typically 15 minutes for flights). The saga must enforce a **hard timeout**: if the customer has not completed payment within 13 minutes of hold, the saga auto-cancels (releases PNR hold) and sends a "session expired" event. This is implemented with MassTransit's `Schedule` + timeout consumer.

### Pricing Snapshot at Booking Time

**Never recalculate price at payment capture.** At the start of the saga, take a **pricing snapshot** (base fare, taxes, markup, total sell price, fare rules, baggage rules) and store it in the saga state. All subsequent steps use the snapshotted price. This prevents:
- Price changing between search result selection and payment (GDS fares fluctuate constantly)
- Markup rule changes by staff mid-booking affecting in-flight transactions
- Audit disputes about what price was shown vs what was charged

### Idempotency Everywhere

Every external call in the saga must be idempotent:
- Stripe: use `IdempotencyKey = bookingId.ToString()` on PaymentIntent creation
- GDS: most hold APIs are naturally idempotent (same PNR returned on retry if hold exists)
- RabbitMQ consumers: check `event_id` against a processed-events table before acting

### MSSQL vs NoSQL Decision

Stick with **MSSQL for all services** given the stack constraint. Travel bookings are inherently relational (bookings → passengers → segments → fares). ACID transactions are critical for payment/wallet operations. NoSQL would introduce consistency problems without meaningful benefit at this scale.

---

*Research confidence: HIGH for saga pattern (MassTransit official docs), service boundaries (industry standard DDD for travel), GDS adapter pattern (established pattern used by Amadeus SDK), Keycloak SSO (official documentation). MEDIUM for specific Keycloak realm configuration details — validate against current Keycloak 24.x docs. All technology versions should be verified against current NuGet/npm releases at build time.*
