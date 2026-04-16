# Phase 3: Core Flight Booking Saga (B2C) - Pattern Map

**Mapped:** 2026-04-15
**Files analyzed:** ~40 new files across 4 plans
**Analogs found:** strong analogs exist for every category except the state machine itself (no prior saga) and Stripe webhook HTTP ingress (no prior webhook) — both flagged below with recommended placement.

---

## Repo conventions detected

| Convention | Evidence |
|-----------|----------|
| **Service layout** | Every service has `{Name}.API` (ASP.NET Core Web — `Microsoft.NET.Sdk.Web`), `{Name}.Application` (pure lib), `{Name}.Infrastructure` (EF Core + MassTransit outbox). See `src/services/BookingService/*`, `src/services/PricingService/*`. |
| **Root namespace** | `TBE.{ServiceName}.{Layer}` — set explicitly via `<RootNamespace>` in every csproj. Folder-shaped sub-namespaces under that (e.g., `TBE.FlightConnectorService.Application.Amadeus`). |
| **Project refs** | API → Application + Infrastructure + `TBE.Contracts` + `TBE.Common`. Infrastructure → Application. Application → `TBE.Contracts` (+ `TBE.Common` when messaging). `TBE.Contracts` has no dependencies (pure records). |
| **Cross-service talk** | Only via `TBE.Contracts` records (events) or HTTP calls through `IHttpClientFactory` named clients. No direct project refs across service boundaries. Confirmed by `SearchController` calling `flight-connector` via named `HttpClient`. |
| **DI style** | Primary-constructor DI in classes (`public class Foo(IBar bar)`); composition in `Program.cs` with `builder.Services.Add...`. No `IServiceCollection` extensions per service except the shared `AddTbeMassTransitWithRabbitMq`. |
| **Options binding** | `builder.Services.Configure<FooOptions>(builder.Configuration.GetSection("Foo"))` + `IOptionsMonitor<FooOptions>` consumers. See `AmadeusOptions` / `AmadeusAuthHandler`. Options classes live in `{Service}.Application/{Vendor}/` next to the code using them. |
| **HTTP clients** | `AddRefitClient<T>()` with `AddHttpMessageHandler<AuthHandler>()` + `AddStandardResilienceHandler()`. See `Program.cs` of `FlightConnectorService.API` lines 28-33. Named-client plain `HttpClient` for service-to-service — see `SearchController`. |
| **Logging** | Serilog with `CompactJsonFormatter`, bootstrap logger, `Enrich.WithProperty("Service", "{ServiceName}")` per service. Identical `try/Log.Fatal/finally/Log.CloseAndFlush` in every `Program.cs`. |
| **Health checks** | Every API registers `AddSqlServer + AddRabbitMQ + AddRedis` at `/health`. RabbitMQ factory inline-creates `ConnectionFactory` from `RabbitMQ:*` config. |
| **MassTransit + outbox** | Use `services.AddTbeMassTransitWithRabbitMq(config, configureConsumers, configureOutbox)` from `TBE.Common.Messaging`. Outbox is `AddEntityFrameworkOutbox<TDbContext>` with `UseSqlServer()`, `UseBusOutbox()`, `QueryDelay = 5s`, `DuplicateDetectionWindow = 30m`. DbContext exposes outbox tables via `modelBuilder.AddInboxStateEntity(); AddOutboxMessageEntity(); AddOutboxStateEntity();`. |
| **Events/Contracts** | C# `record` types in `src/shared/TBE.Contracts/Events/` grouped by domain (e.g., `BookingEvents.cs`). Positional params, XML doc summary per event. |
| **EF Core migrations** | `IDesignTimeDbContextFactory<T>` in `{Name}.Infrastructure/{Name}DbContextFactory.cs` pointing at `localhost;Trusted_Connection=True`. Migrations land in `{Name}.Infrastructure/Migrations/`. Migrator tool runs them on startup via `TBE.DbMigrator/Program.cs` — every new `DbContext` must be added there. |
| **appsettings** | `appsettings.json` (common) + `appsettings.Development.json` (overrides). Empty `ConnectionStrings` placeholder + `RabbitMQ` + `Redis` sections already scaffolded. Add new sections alongside (`Stripe`, `SendGrid`, `Encryption`). |
| **Tests** | Single xUnit project `tests/TBE.Tests.Unit/` with per-service folders. xUnit 2.9.3, FluentAssertions 8.2.0, NSubstitute 5.3.0, `Microsoft.EntityFrameworkCore.InMemory` for DbContext-based tests. Each class has `[Trait("Category", "Unit")]`, uses `[Fact(DisplayName = "...")]` with requirement ID prefix (e.g., `"INV02: ..."`). |
| **Controllers** | `[ApiController]`, `[Route("resource")]`, primary-constructor DI, inline regex validation, named `HttpClient` for downstream, `CancellationToken ct` on every action. See `FlightSearchController`, `SearchController`, `PricingController`. |

---

## File Classification & Pattern Assignments

### Plan 03-01: MassTransit Booking Saga State Machine

| New File (proposed path) | Role | Data Flow | Closest Analog | Match |
|---|---|---|---|---|
| `src/services/BookingService/BookingService.Application/Saga/BookingSagaState.cs` | saga-state entity | event-driven | **no analog** (first saga) | new |
| `src/services/BookingService/BookingService.Application/Saga/BookingSaga.cs` (state machine) | orchestrator | event-driven | `BookingService.Application/Consumers/TestBookingConsumer.cs` (only for DI/namespace pattern) | partial |
| `src/services/BookingService/BookingService.Application/Saga/Events/*.cs` (saga-internal events if needed beyond `TBE.Contracts`) | contract | event-driven | `src/shared/TBE.Contracts/Events/BookingEvents.cs` | exact |
| `src/shared/TBE.Contracts/Events/SagaEvents.cs` (add `PriceReconfirmed`, `PNRCreated`, `PaymentAuthorized`, `TicketIssued`, `PaymentCaptured`, `BookingCancelled`, `BookingExpired`) | contract | event-driven | `src/shared/TBE.Contracts/Events/BookingEvents.cs` | exact |
| `src/services/BookingService/BookingService.Infrastructure/Configurations/BookingSagaStateMap.cs` | EF mapping | CRUD | `src/services/PricingService/PricingService.Infrastructure/PricingDbContext.cs` lines 24-34 (entity builder) | role-match |
| `src/services/BookingService/BookingService.Infrastructure/BookingDbContext.cs` (modify to add `DbSet<BookingSagaState>` under `Saga` schema + `DbSet<SagaDeadLetter>`) | dbcontext | CRUD | `src/services/PricingService/PricingService.Infrastructure/PricingDbContext.cs` | exact |
| `src/services/BookingService/BookingService.Infrastructure/BookingDbContextFactory.cs` | design-time factory | — | `src/services/PricingService/PricingService.Infrastructure/PricingDbContextFactory.cs` | exact |
| `src/services/BookingService/BookingService.Infrastructure/Migrations/*_AddBookingSagaState.cs` | migration | schema | `src/services/PricingService/PricingService.Infrastructure/Migrations/20260415000000_AddMarkupRules.cs` | exact |
| `src/services/BookingService/BookingService.Application/Saga/Activities/*Activity.cs` (PNR-create, auth, capture etc. if using Activities) | activity | request-response | `src/services/BookingService/BookingService.Application/Consumers/TestBookingConsumer.cs` (for ctor/log pattern) | partial |
| `src/services/BookingService/BookingService.API/Controllers/BookingsController.cs` (POST `/bookings` → publishes `BookingInitiated`, GET `/bookings/{id}`, GET `/customers/{id}/bookings`) | controller | request-response + CRUD | `src/services/FlightConnectorService/FlightConnectorService.API/Controllers/FlightSearchController.cs` | role-match |
| `src/services/BookingService/BookingService.API/Program.cs` (modify — register saga, schedule, SqlTransport or in-memory scheduler) | composition | — | existing `BookingService.API/Program.cs` (extends) + `FlightConnectorService.API/Program.cs` (for controller registration) | exact |
| `src/tools/TBE.DbMigrator/Program.cs` (no change — already migrates `BookingDbContext`) | — | — | — | — |
| `tests/TBE.Tests.Unit/BookingService/BookingSagaTests.cs` | test | event-driven | `tests/TBE.Tests.Unit/PricingService/MarkupRulesEngineTests.cs` (in-memory DbContext fixture) | role-match |

**FLAG: no saga analog exists.** `TestBookingConsumer` is the only prior MassTransit-touch in BookingService. Researcher cites `MassTransitStateMachine<T>` from MassTransit 9.1 docs as the authoritative template. Placement confirmed: saga class goes in `BookingService.Application/Saga/` (not Infrastructure — matches the `Application` = domain-logic convention already established by `PricingService.Application/Rules/IPricingRulesEngine.cs`). Persistence/repository wiring goes in `Program.cs` inside `AddMassTransit` using `.AddSagaStateMachine<BookingSaga, BookingSagaState>().EntityFrameworkRepository(...)`.

#### Key excerpts to copy from

**DbContext + outbox pattern** (`src/services/PricingService/PricingService.Infrastructure/PricingDbContext.cs` lines 1-36):
```csharp
using MassTransit;
using Microsoft.EntityFrameworkCore;
using TBE.PricingService.Application.Rules.Models;

namespace TBE.PricingService.Infrastructure;

public class PricingDbContext : DbContext
{
    public PricingDbContext(DbContextOptions<PricingDbContext> options) : base(options) { }
    public DbSet<MarkupRule> MarkupRules => Set<MarkupRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        modelBuilder.Entity<MarkupRule>(b =>
        {
            b.HasKey(r => r.Id);
            b.Property(r => r.ProductType).IsRequired().HasMaxLength(10);
            b.Property(r => r.Value).HasColumnType("decimal(18,4)");
            b.HasIndex(r => new { r.ProductType, r.Channel, r.IsActive });
        });
    }
}
```

For `BookingSagaState`, add to `BookingDbContext.OnModelCreating`:
- `modelBuilder.Entity<BookingSagaState>(b => b.ToTable("BookingSagaState", "Saga")...)` (D-01 dedicated schema)
- `b.Property(x => x.RowVersion).IsRowVersion();` for optimistic concurrency (D-01)

**Design-time factory** (`PricingDbContextFactory.cs` — copy verbatim, rename types, change DB name):
```csharp
public class PricingDbContextFactory : IDesignTimeDbContextFactory<PricingDbContext>
{
    public PricingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PricingDbContext>()
            .UseSqlServer("Server=localhost;Database=PricingDb;Trusted_Connection=True;")
            .Options;
        return new PricingDbContext(options);
    }
}
```

**Event record style** (`src/shared/TBE.Contracts/Events/BookingEvents.cs`):
```csharp
namespace TBE.Contracts.Events;

/// <summary>
/// Published when a booking is initiated by any channel (B2C or B2B).
/// Starts the booking saga in Phase 3.
/// </summary>
public record BookingInitiated(
    Guid BookingId,
    string ProductType,
    string Channel,
    string UserId,
    DateTimeOffset InitiatedAt);
```

**MassTransit + outbox registration** (`src/services/BookingService/BookingService.API/Program.cs` lines 30-45 — extend with saga repository):
```csharp
builder.Services.AddTbeMassTransitWithRabbitMq(
    builder.Configuration,
    configureConsumers: x =>
    {
        // x.AddSagaStateMachine<BookingSaga, BookingSagaState>()
        //  .EntityFrameworkRepository(r => { r.ConcurrencyMode = ConcurrencyMode.Optimistic;
        //                                    r.ExistingDbContext<BookingDbContext>(); });
    },
    configureOutbox: x =>
    {
        x.AddEntityFrameworkOutbox<BookingDbContext>(o =>
        {
            o.UseSqlServer();
            o.UseBusOutbox();
            o.QueryDelay = TimeSpan.FromSeconds(5);
            o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
        });
    });
```

---

### Plan 03-02: Stripe Payment Service + B2B Wallet

| New File | Role | Data Flow | Closest Analog | Match |
|---|---|---|---|---|
| `src/services/PaymentService/PaymentService.Application/Stripe/IStripePaymentGateway.cs` | interface | request-response | `src/services/PricingService/PricingService.Application/Rules/IPricingRulesEngine.cs` | exact (shape) |
| `src/services/PaymentService/PaymentService.Application/Stripe/StripePaymentGateway.cs` | service | request-response | `src/services/FlightConnectorService/FlightConnectorService.Application/Amadeus/AmadeusFlightProvider.cs` (external-SDK adapter) | role-match |
| `src/services/PaymentService/PaymentService.Application/Stripe/StripeOptions.cs` | options | — | `src/services/FlightConnectorService/FlightConnectorService.Application/Amadeus/AmadeusOptions.cs` | exact |
| `src/services/PaymentService/PaymentService.Application/Consumers/AuthorizePaymentConsumer.cs`, `CapturePaymentConsumer.cs`, `CancelAuthorizationConsumer.cs`, `RefundPaymentConsumer.cs` | consumer | event-driven | `src/services/BookingService/BookingService.Application/Consumers/TestBookingConsumer.cs` | exact |
| `src/services/PaymentService/PaymentService.API/Controllers/StripeWebhookController.cs` | controller (webhook ingress) | request-response (one-way POST) | `src/services/FlightConnectorService/FlightConnectorService.API/Controllers/FlightSearchController.cs` | role-match (flag below) |
| `src/services/PaymentService/PaymentService.Application/Wallet/IWalletRepository.cs` | repository | CRUD | `src/services/PricingService/PricingService.Application/Rules/IPricingRulesEngine.cs` (interface shape) | partial |
| `src/services/PaymentService/PaymentService.Infrastructure/Wallet/WalletRepository.cs` (Dapper-based, UPDLOCK/ROWLOCK) | repository | CRUD | **no analog** (first Dapper usage) | new — see flag |
| `src/services/PaymentService/PaymentService.Infrastructure/Wallet/WalletTransaction.cs` | entity | — | `src/services/PricingService/PricingService.Application/Rules/Models/MarkupRule.cs` | role-match |
| `src/services/PaymentService/PaymentService.Infrastructure/PaymentDbContext.cs` (modify — add `DbSet<WalletTransaction>`, `DbSet<StripeWebhookEvent>` idempotency) | dbcontext | CRUD | `src/services/PricingService/PricingService.Infrastructure/PricingDbContext.cs` | exact |
| `src/services/PaymentService/PaymentService.Infrastructure/PaymentDbContextFactory.cs` | design-time factory | — | `PricingDbContextFactory.cs` | exact |
| `src/services/PaymentService/PaymentService.Infrastructure/Migrations/*_AddWalletAndPaymentIntents.cs` | migration | — | `PricingService.Infrastructure/Migrations/20260415000000_AddMarkupRules.cs` | exact |
| `src/services/PaymentService/PaymentService.API/Program.cs` (modify — add Stripe config, consumers, controllers) | composition | — | existing `PaymentService.API/Program.cs` | exact |
| `src/services/PaymentService/PaymentService.API/appsettings.json` (add `Stripe:ApiKey`, `Stripe:WebhookSecret`) | config | — | existing `appsettings.json` | exact |
| `tests/TBE.Tests.Unit/PaymentService/WalletRepositoryTests.cs`, `StripePaymentGatewayTests.cs` | tests | — | `tests/TBE.Tests.Unit/PricingService/MarkupRulesEngineTests.cs` | role-match |

**FLAG: Stripe webhook HTTP handler has no analog** — no service currently receives unauthenticated webhook ingress. Place at `PaymentService.API/Controllers/StripeWebhookController.cs` following the controller style of `FlightSearchController` (primary-ctor DI, `[ApiController]`, `[Route("webhooks/stripe")]`). Key additions vs analog:
- Read raw body via `new StreamReader(Request.Body).ReadToEndAsync()` BEFORE model binding (signature verification needs raw bytes).
- Validate with `Stripe.EventUtility.ConstructEvent(rawJson, sig, webhookSecret)`.
- Do NOT run business logic in the HTTP handler — per STACK.md, publish a `StripeWebhookReceived` event onto the bus and return `200` immediately.
- Endpoint must be excluded from `[Authorize]` (webhook from Stripe, not JWT user).

**FLAG: Dapper not yet in repo.** Researcher specifies Dapper 2.1.35 for wallet UPDLOCK/ROWLOCK. Add to `PaymentService.Infrastructure.csproj` `<PackageReference>` alongside `Microsoft.EntityFrameworkCore.SqlServer`. Keep Dapper scoped to `Infrastructure/Wallet/` only — EF stays primary for everything else.

#### Key excerpts to copy from

**External SDK adapter pattern** — `AmadeusFlightProvider.cs` lines 1-12 (interface + primary-ctor DI + `Name` property):
```csharp
using Microsoft.Extensions.Logging;
using TBE.Contracts.Inventory;
using TBE.Contracts.Inventory.Models;

namespace TBE.FlightConnectorService.Application.Amadeus;

public sealed class AmadeusFlightProvider(IAmadeusFlightApi api, ILogger<AmadeusFlightProvider> logger)
    : IFlightAvailabilityProvider
{
    public string Name => "amadeus";
    public async Task<IReadOnlyList<UnifiedFlightOffer>> SearchAsync(...)
```

Apply to `StripePaymentGateway` — take `ILogger<StripePaymentGateway>` + `IOptionsMonitor<StripeOptions>` in primary ctor; never log full Stripe objects (PCI); return DTOs from Application layer, not raw `Stripe.PaymentIntent`.

**Options class** — `AmadeusOptions.cs` lines 1-8:
```csharp
namespace TBE.FlightConnectorService.Application.Amadeus;
public sealed class AmadeusOptions
{
    public string ApiKey { get; set; } = default!;
    public string ApiSecret { get; set; } = default!;
    public string BaseUrl { get; set; } = "https://test.api.amadeus.com/v2";
}
```
→ `StripeOptions { ApiKey, PublishableKey, WebhookSecret }`. Bound in `Program.cs` with `builder.Services.Configure<StripeOptions>(builder.Configuration.GetSection("Stripe"))`. Never log `ApiKey` or `WebhookSecret`.

**Consumer shell** — `TestBookingConsumer.cs` lines 1-30:
```csharp
public class TestBookingConsumer : IConsumer<BookingInitiated>
{
    private readonly ILogger<TestBookingConsumer> _logger;
    public TestBookingConsumer(ILogger<TestBookingConsumer> logger) { _logger = logger; }
    public Task Consume(ConsumeContext<BookingInitiated> context) { ... }
}
```
Register in `Program.cs` via `configureConsumers: x => x.AddConsumer<AuthorizePaymentConsumer>();` etc.

---

### Plan 03-03: TTL Monitor + Compliance Hardening

| New File | Role | Data Flow | Closest Analog | Match |
|---|---|---|---|---|
| `src/services/BookingService/BookingService.Application/Ttl/IFareRuleParser.cs` | interface | transform | `src/services/PricingService/PricingService.Application/Rules/IPricingRulesEngine.cs` | exact (shape) |
| `src/services/BookingService/BookingService.Application/Ttl/FareRuleParser.cs` | service | transform | `src/services/FlightConnectorService/FlightConnectorService.Application/Amadeus/AmadeusFlightProvider.cs` `MapOffer` (static regex/parse logic) | role-match |
| `src/services/BookingService/BookingService.Application/Ttl/Adapters/AmadeusFareRuleAdapter.cs`, `SabreFareRuleAdapter.cs`, `GalileoFareRuleAdapter.cs` | adapter | transform | `AmadeusFlightProvider` vs `SabreFlightProvider` (per-GDS split pattern) | role-match |
| `src/services/BookingService/BookingService.Application/Ttl/TtlMonitorService.cs` | hosted service | batch | `src/services/NotificationService/NotificationService.API/Worker.cs` (BackgroundService skeleton) | role-match |
| `src/services/BookingService/BookingService.API/Program.cs` (modify — `builder.Services.AddHostedService<TtlMonitorService>()`) | composition | — | existing | exact |
| `src/shared/TBE.Common/Encryption/IEncryptionKeyProvider.cs` | interface | — | `src/shared/TBE.Contracts/Inventory/IFlightAvailabilityProvider.cs` (interface in shared proj) | exact |
| `src/shared/TBE.Common/Encryption/EnvEncryptionKeyProvider.cs` | impl | — | **no analog** — first `TBE.Common` service beyond messaging | new |
| `src/shared/TBE.Common/Encryption/AesGcmEncryptor.cs` | service | transform | **no analog** | new |
| `src/shared/TBE.Common/Telemetry/SensitiveAttributeProcessor.cs` | OTel processor | transform | **no analog** (no OTel code yet — infra only) | new |
| `src/shared/TBE.Common/Telemetry/TelemetryServiceExtensions.cs` (`AddTbeOpenTelemetry(...)`) | DI extension | — | `src/shared/TBE.Common/Messaging/MassTransitServiceExtensions.cs` | exact |
| All `Program.cs` files (modify — call `AddTbeOpenTelemetry` + ensure `[Authorize]` middleware wired) | composition | — | existing Program.cs files | exact |
| `src/services/BookingService/BookingService.API/Controllers/BookingsController.cs` — apply `[Authorize]` | middleware attr | — | — (no prior `[Authorize]` in repo per Phase 1 scope) | new — see flag |
| `tests/TBE.Tests.Unit/Common/EncryptionTests.cs`, `TtlParserTests.cs`, `SensitiveAttributeProcessorTests.cs` | tests | — | `tests/TBE.Tests.Unit/PricingService/MarkupRulesEngineTests.cs` | role-match |

**FLAG: `TBE.Common` currently contains only messaging (`Messaging/MassTransitServiceExtensions.cs`).** Add new top-level folders: `Encryption/`, `Telemetry/`. Keep the "one static `Add...` DI extension per subsystem" pattern established by `MassTransitServiceExtensions`. Update `TBE.Common.csproj` to add `OpenTelemetry`, `OpenTelemetry.Extensions.Hosting` (no System.Security.Cryptography — in-box in .NET 8).

**FLAG: `[Authorize]` not yet applied anywhere** (Phase 1 delivered Keycloak/YARP but services don't enforce). Phase 3 blanket-applies it. Pattern: add `builder.Services.AddAuthentication("Bearer").AddJwtBearer(...)` + `app.UseAuthentication(); app.UseAuthorization();` to every API `Program.cs`, then `[Authorize]` on new controllers and retrospectively on existing ones touched in Phase 3. Stripe webhook endpoint must be `[AllowAnonymous]`.

**FLAG: No prior hosted service in BookingService.** `Worker.cs` in `NotificationService.API` is the only `BackgroundService` example and it's a Phase-1 placeholder. Place TTL monitor in `BookingService.Application/Ttl/TtlMonitorService.cs` (shape it off the `Worker` skeleton, inject `BookingDbContext` and `IPublishEndpoint` via `IServiceScopeFactory` because it is a singleton). Register in `BookingService.API/Program.cs` with `AddHostedService<TtlMonitorService>()`.

#### Key excerpts to copy from

**Shared DI extension pattern** — `src/shared/TBE.Common/Messaging/MassTransitServiceExtensions.cs` lines 11-48:
```csharp
public static class MassTransitServiceExtensions
{
    public static IServiceCollection AddTbeMassTransitWithRabbitMq(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureConsumers = null,
        Action<IBusRegistrationConfigurator>? configureOutbox = null)
    { ... }
}
```
Mirror for `AddTbeOpenTelemetry(this IServiceCollection, IConfiguration, string serviceName)` that registers the pipeline + inserts `SensitiveAttributeProcessor`. Every service's `Program.cs` then calls it one-line.

**Per-GDS adapter split** — `FlightConnectorService.Application/{Amadeus,Sabre}/` folder structure maps 1:1 onto `BookingService.Application/Ttl/Adapters/{Amadeus,Sabre,Galileo}FareRuleAdapter.cs`. Keyed DI registration from `FlightConnectorService.API/Program.cs` lines 34-55 is the registration template (but for `IFareRuleAdapter`).

**Hosted service shell** — `NotificationService.API/Worker.cs` lines 1-16. For the TTL monitor, keep the `while (!ct.IsCancellationRequested) { await Task.Delay(TimeSpan.FromMinutes(5), ct); ... }` loop, and inside create a DI scope (`using var scope = _scopeFactory.CreateScope();`) to resolve `BookingDbContext` (scoped) from a singleton `BackgroundService`.

---

### Plan 03-04: Notification Service + Email Templates

| New File | Role | Data Flow | Closest Analog | Match |
|---|---|---|---|---|
| `src/services/NotificationService/NotificationService.Application/Consumers/BookingConfirmedEmailConsumer.cs`, `BookingCancelledEmailConsumer.cs`, `TicketIssuedEmailConsumer.cs`, `BookingExpiredEmailConsumer.cs`, `WalletLowBalanceEmailConsumer.cs` | consumer | event-driven | `src/services/BookingService/BookingService.Application/Consumers/TestBookingConsumer.cs` | exact |
| `src/services/NotificationService/NotificationService.Application/Email/IEmailDelivery.cs` | interface | request-response | `src/services/PricingService/PricingService.Application/Rules/IPricingRulesEngine.cs` | exact |
| `src/services/NotificationService/NotificationService.Infrastructure/Email/SendGridEmailDelivery.cs` | impl | request-response | `src/services/FlightConnectorService/FlightConnectorService.Application/Amadeus/AmadeusFlightProvider.cs` (external SDK adapter) | role-match |
| `src/services/NotificationService/NotificationService.Infrastructure/Email/SmtpEmailDelivery.cs` | impl (dev) | request-response | same | role-match |
| `src/services/NotificationService/NotificationService.Application/Email/SendGridOptions.cs`, `SmtpOptions.cs` | options | — | `AmadeusOptions.cs` | exact |
| `src/services/NotificationService/NotificationService.Application/Templates/IEmailTemplateRenderer.cs` | interface | transform | `IPricingRulesEngine` | exact (shape) |
| `src/services/NotificationService/NotificationService.Infrastructure/Templates/RazorLightTemplateRenderer.cs` | impl | transform | `SearchCacheService.cs` (third-party lib wrapper pattern) | role-match |
| `src/services/NotificationService/NotificationService.Infrastructure/Templates/Views/*.cshtml` (BookingConfirmation, Cancellation, TicketIssued, WalletLowBalance, + `_Header.cshtml`, `_Footer.cshtml`) | template | — | **no analog** | new |
| `src/services/NotificationService/NotificationService.Application/Pdf/IETicketPdfGenerator.cs` + `Infrastructure/Pdf/QuestPdfETicketGenerator.cs` | service | transform | — | new |
| `src/services/NotificationService/NotificationService.Infrastructure/Idempotency/EmailIdempotencyLog.cs` + DbContext entity | entity | CRUD | `PricingService.Infrastructure/PricingDbContext.cs` `MarkupRule` entity config | exact |
| `src/services/NotificationService/NotificationService.Infrastructure/NotificationDbContext.cs` (modify — add `DbSet<EmailIdempotencyLog>`) | dbcontext | — | `PricingDbContext.cs` | exact |
| `src/services/NotificationService/NotificationService.Infrastructure/NotificationDbContextFactory.cs` | factory | — | `PricingDbContextFactory.cs` | exact |
| `src/services/NotificationService/NotificationService.Infrastructure/Migrations/*_AddEmailIdempotencyLog.cs` | migration | — | `PricingService.Infrastructure/Migrations/20260415000000_AddMarkupRules.cs` | exact |
| `src/services/NotificationService/NotificationService.API/Program.cs` (modify — add consumers, SendGrid/SMTP, RazorLight, QuestPDF) | composition | — | existing | exact |
| `src/services/NotificationService/NotificationService.API/Worker.cs` | **remove or repurpose** — pure consumer service needs no Worker | — | — | — |
| `tests/TBE.Tests.Unit/NotificationService/*ConsumerTests.cs`, `TemplateRendererTests.cs`, `EmailIdempotencyTests.cs` | tests | — | `MarkupRulesEngineTests.cs` | role-match |

#### Key excerpts to copy from

**Consumer pattern** — already covered by `TestBookingConsumer.cs` above. Each notification consumer takes `IEmailDelivery`, `IEmailTemplateRenderer`, `NotificationDbContext` (for idempotency), `ILogger<T>` via primary ctor.

**Template renderer — third-party lib wrapper** — mirror the shape of `SearchCacheService.cs` lines 1-16 (private static options, constructor injection of the third-party client):
```csharp
public sealed class SearchCacheService(HybridCache hybridCache, IDistributedCache redis) : ISearchCacheService
{
    private static readonly HybridCacheEntryOptions BrowseTtl = new() { ... };
    ...
}
```

**Idempotency log entity** — copy `MarkupRule` entity config from `PricingDbContext.cs` lines 24-34 (MaxLength constraints, composite index). `EmailIdempotencyLog` primary key: `(EventId Guid, EmailType string)` composite; unique index on both.

---

## Shared Patterns (applies to multiple plans)

### 1. Program.cs skeleton
**Source:** `src/services/BookingService/BookingService.API/Program.cs` (lines 1-80), augmented by `src/services/FlightConnectorService/FlightConnectorService.API/Program.cs` for controller services and adapter/options registration.
**Apply to:** every new or modified `Program.cs` (BookingService, PaymentService, NotificationService). Keep the Serilog bootstrap, `try/finally/Log.CloseAndFlush` envelope, `Enrich.WithProperty("Service", "{Name}")`, and the identical `AddHealthChecks().AddSqlServer(...).AddRabbitMQ(...).AddRedis(...)` block. New additions slot between DbContext registration and MassTransit registration.

### 2. Outbox-based event publishing (NEVER raw `IBus.Publish` from a DB-touching operation)
**Source:** `MassTransitServiceExtensions` + `BookingService.API/Program.cs` lines 36-44.
**Apply to:** every consumer and saga step that writes to SQL. Use `ConsumeContext.Publish` / `Context.Publish` inside the same EF transaction; the outbox guarantees at-least-once delivery.

### 3. Options binding
**Source:** `FlightConnectorService.API/Program.cs` line 24 + `AmadeusOptions.cs`.
**Apply to:** `StripeOptions`, `SendGridOptions`, `SmtpOptions`, `EncryptionOptions`, `TtlMonitorOptions`. Each: `Configure<TOptions>(GetSection("Section"))` + consumers take `IOptionsMonitor<TOptions>`.

### 4. Secrets handling
**Source:** appsettings.json across services + `AmadeusAuthHandler.cs` line 44 ("never log token value" security comment).
**Apply to:** Stripe keys, SendGrid API key, encryption key, webhook secret. Keep empty placeholder in `appsettings.json`; populate via env vars (`Stripe__ApiKey` etc.). Add a developer note to `appsettings.Development.json` pointing at `.env`. Phase 7 swaps for Key Vault per COMP-05.

### 5. Test project
**Source:** `tests/TBE.Tests.Unit/TBE.Tests.Unit.csproj` + `MarkupRulesEngineTests.cs`.
**Apply to:** every new Phase 3 test. Add needed `<ProjectReference>`s to the csproj (BookingService.Application, PaymentService.Application, NotificationService.Application, TBE.Common). Use `[Trait("Category", "Unit")]`, `[Fact(DisplayName = "FLTB-XX: ...")]` naming, FluentAssertions `.Should()...`, in-memory DbContext via `UseInMemoryDatabase(Guid.NewGuid().ToString())`. NSubstitute for interface mocks.

### 6. DbMigrator registration
**Source:** `src/tools/TBE.DbMigrator/Program.cs` lines 42-48.
**Apply to:** **no new DbContexts** — Booking/Payment/Notification DbContexts already registered. New migrations auto-apply.

---

## No Analog Found (plan for these from research, not codebase)

| File | Reason | Primary reference |
|------|--------|---|
| `BookingSaga.cs` (MassTransitStateMachine) | First saga in repo | RESEARCH.md § Saga State Machine; MassTransit 9.1 docs |
| `StripeWebhookController.cs` | First webhook ingress | RESEARCH.md § Webhook Signature Verification; docs.stripe.com |
| `WalletRepository.cs` with Dapper `UPDLOCK, ROWLOCK` | First Dapper usage; first raw-SQL lock hints | RESEARCH.md § Wallet Concurrency; PITFALLS §18 |
| `AesGcmEncryptor.cs` | No prior crypto in repo | RESEARCH.md § Standard Stack (AesGcm note) |
| `SensitiveAttributeProcessor.cs` | No prior OTel code beyond Phase 1 infra setup | RESEARCH.md § Telemetry |
| `RazorLightTemplateRenderer.cs` + `.cshtml` templates | First Razor usage outside MVC | RESEARCH.md + RazorLight docs |
| `QuestPdfETicketGenerator.cs` | First PDF generation | RESEARCH.md + QuestPDF docs |

For these, the planner should embed the authoritative code pattern from RESEARCH.md directly into the plan's Action section, and use the codebase analog only for surrounding scaffolding (namespace, folder, DI, ctor style, logging).

---

## Metadata

**Analog search scope:**
- `src/services/**/{API,Application,Infrastructure}/**/*.cs`
- `src/shared/TBE.{Common,Contracts}/**/*.cs`
- `src/tools/TBE.DbMigrator/Program.cs`
- `tests/TBE.Tests.Unit/**/*.cs`
- All `*.csproj`, `appsettings*.json`, `Program.cs`, `Dockerfile`

**Files scanned:** ~60 production, ~8 test, ~30 project/config.
**Worktree `/.claude/worktrees/agent-a7a51901/` excluded** (working-copy clone, not canonical).

**Pattern extraction date:** 2026-04-15

---

## PATTERN MAPPING COMPLETE

**Phase:** 3 - Core Flight Booking Saga (B2C)
**Files classified:** ~40 (across 4 plans)
**Analogs found:** 33 / 40 (7 flagged as no-analog, research-driven)

### Coverage
- Files with exact analog: 22 (DbContexts, factories, migrations, events, consumers, options, DI extensions, controllers, tests, Program.cs changes)
- Files with role-match analog: 11 (saga state, adapters, TTL monitor, external SDK wrappers)
- Files with no analog (research-driven): 7 (saga state machine, Stripe webhook, Dapper repo, AES-GCM, OTel processor, Razor templates, QuestPDF)

### Key Patterns Identified
- **Three-project service layout is sacred.** API = composition + controllers + Program.cs; Application = interfaces + domain logic + consumers + options; Infrastructure = EF Core + external-SDK impls + migrations + Dapper. Phase 3 files must respect this.
- **Every DB-touching service uses MassTransit EF Core outbox** via the shared `AddTbeMassTransitWithRabbitMq` helper — saga/payment/notification all extend that exact Program.cs block.
- **Cross-service events flow only through `TBE.Contracts/Events/*.cs` records**; adding saga events means appending to that file (or creating a sibling `SagaEvents.cs`).
- **Shared concerns go into `TBE.Common`** as `AddTbe*` DI extension methods (`Messaging` pattern is the template for `Encryption` and `Telemetry`).
- **Per-GDS vendor splits use folder-scoped subfolders** (`Application/Amadeus/`, `Application/Sabre/`) with keyed DI registration — directly transferable to fare-rule adapters.
- **Tests are xUnit + FluentAssertions + NSubstitute + EF InMemory**; one test project, one folder per service.

### File Created
`C:\Users\zhdishaq\source\repos\TBE\.planning\phases\03-core-flight-booking-saga-b2c\03-PATTERNS.md`

### Ready for Planning
Pattern mapping complete. Planner can now reference concrete analog files and code excerpts directly in each plan's Action sections. Seven research-driven files are clearly flagged so the planner pulls from RESEARCH.md for those rather than hunting the codebase.
