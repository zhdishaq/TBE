---
phase: 01-infrastructure-foundation
plan: 03
type: execute
wave: 2
depends_on:
  - 01-PLAN-docker-compose-stack
  - 01-PLAN-mssql-migrations-shared
files_modified:
  - src/shared/TBE.Contracts/TBE.Contracts.csproj
  - src/shared/TBE.Contracts/Events/BookingEvents.cs
  - src/shared/TBE.Common/TBE.Common.csproj
  - src/shared/TBE.Common/Messaging/MassTransitServiceExtensions.cs
  - src/services/BookingService/BookingService.Infrastructure/BookingDbContext.cs
  - src/services/BookingService/BookingService.API/Program.cs
  - src/services/BookingService/BookingService.API/BookingService.API.csproj
  - src/services/BookingService/BookingService.Infrastructure/BookingService.Infrastructure.csproj
  - src/services/BookingService/BookingService.Application/Consumers/TestBookingConsumer.cs
  - TBE.sln
autonomous: true
requirements:
  - INFRA-04

must_haves:
  truths:
    - "BookingService connects to RabbitMQ on startup and does not crash"
    - "A test message published to RabbitMQ is consumed and acknowledged by TestBookingConsumer"
    - "The OutboxMessage table exists in BookingDb after db-migrator runs"
    - "A message published inside a transaction appears in OutboxMessage BEFORE being dispatched to RabbitMQ"
    - "TBE.Contracts defines BookingInitiated, BookingConfirmed, BookingFailed, PaymentProcessed record types"
  artifacts:
    - path: "src/shared/TBE.Contracts/Events/BookingEvents.cs"
      provides: "Canonical event contracts shared across all services"
      exports: ["BookingInitiated", "BookingConfirmed", "BookingFailed", "PaymentProcessed"]
    - path: "src/shared/TBE.Common/Messaging/MassTransitServiceExtensions.cs"
      provides: "Reusable AddTbeMassTransit() extension method"
      contains: "AddMassTransit"
    - path: "src/services/BookingService/BookingService.Infrastructure/BookingDbContext.cs"
      provides: "DbContext with outbox tables registered"
      contains: "AddOutboxMessageEntity"
  key_links:
    - from: "BookingService.Infrastructure BookingDbContext"
      to: "MassTransit outbox tables (InboxState, OutboxMessage, OutboxState)"
      via: "modelBuilder.AddInboxStateEntity() / AddOutboxMessageEntity() / AddOutboxStateEntity()"
      pattern: "AddOutboxMessageEntity"
    - from: "BookingService.API Program.cs"
      to: "RabbitMQ via MassTransit"
      via: "AddMassTransit → UsingRabbitMq with host from IConfiguration"
      pattern: "UsingRabbitMq"
---

<objective>
Wire MassTransit 9.1.0 over RabbitMQ into the BookingService as the reference implementation. Create `TBE.Contracts` with core event records and `TBE.Common` with a reusable `AddTbeMassTransit()` extension. Configure the EF Core outbox on `BookingDbContext`. Verify end-to-end message delivery with a `TestBookingConsumer`.

Purpose: Establishes the messaging backbone (INFRA-04). Subsequent phases (saga in Phase 3, notifications in Phase 3) build directly on the patterns established here.

Output: `TBE.Contracts` project, `TBE.Common` messaging extension, `BookingService` wired with MassTransit + outbox, `TestBookingConsumer` that logs received messages.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/ROADMAP.md
@.planning/phases/01-infrastructure-foundation/01-CONTEXT.md
@.planning/phases/01-infrastructure-foundation/01-RESEARCH.md

<interfaces>
<!-- Exact API patterns from RESEARCH.md — use verbatim -->

MassTransit 9.1.0 NuGet packages:
- MassTransit 9.1.0
- MassTransit.RabbitMQ 9.1.0
- MassTransit.EntityFrameworkCore 9.1.0

Outbox registration pattern (inside AddMassTransit, BEFORE UsingRabbitMq):
```csharp
x.AddEntityFrameworkOutbox<BookingDbContext>(o =>
{
    o.UseSqlServer();
    o.UseBusOutbox();
    o.QueryDelay = TimeSpan.FromSeconds(5);
    o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
});
```

Outbox DbContext extension methods (in OnModelCreating):
```csharp
modelBuilder.AddInboxStateEntity();
modelBuilder.AddOutboxMessageEntity();
modelBuilder.AddOutboxStateEntity();
```

Tables created: InboxState, OutboxMessage, OutboxState

Event contract naming: MassTransit 9 creates RabbitMQ exchanges from the full C# type name.
Use namespace TBE.Contracts.Events to get predictable exchange names.
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Create TBE.Contracts and TBE.Common shared projects with event contracts and MassTransit extension</name>
  <read_first>
    - .planning/phases/01-infrastructure-foundation/01-CONTEXT.md — D-06 (TBE.Contracts vs TBE.Common split); D-07 (ProjectReference not NuGet); D-08 (no cross-service project references)
    - .planning/phases/01-infrastructure-foundation/01-RESEARCH.md — "Exchange Topology for Phase 1" section; NuGet versions table
  </read_first>
  <files>
    src/shared/TBE.Contracts/TBE.Contracts.csproj,
    src/shared/TBE.Contracts/Events/BookingEvents.cs,
    src/shared/TBE.Common/TBE.Common.csproj,
    src/shared/TBE.Common/Messaging/MassTransitServiceExtensions.cs,
    TBE.sln
  </files>
  <action>
**Step 1: Create project directories and scaffolds**

```bash
mkdir -p src/shared/TBE.Contracts/Events
mkdir -p src/shared/TBE.Common/Messaging
dotnet new classlib -n TBE.Contracts -o src/shared/TBE.Contracts
dotnet new classlib -n TBE.Common   -o src/shared/TBE.Common
```

**Step 2: Replace TBE.Contracts.csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>TBE.Contracts</RootNamespace>
  </PropertyGroup>
  <!-- No external dependencies — pure contracts/records only -->
</Project>
```

**Step 3: Create `src/shared/TBE.Contracts/Events/BookingEvents.cs`**

```csharp
namespace TBE.Contracts.Events;

/// <summary>
/// Published when a booking is initiated by any channel (B2C or B2B).
/// Starts the booking saga in Phase 3.
/// </summary>
public record BookingInitiated(
    Guid BookingId,
    string ProductType,   // "flight" | "hotel" | "car"
    string Channel,       // "b2c" | "b2b"
    string UserId,
    DateTimeOffset InitiatedAt);

/// <summary>
/// Published when a booking is fully confirmed (PNR created + payment captured + ticket issued).
/// Triggers confirmation email in Notification Service.
/// </summary>
public record BookingConfirmed(
    Guid BookingId,
    string SupplierRef,
    string Channel,
    DateTimeOffset ConfirmedAt);

/// <summary>
/// Published when a booking fails at any saga step after compensation is complete.
/// Triggers failure email and backoffice dead-letter queue entry.
/// </summary>
public record BookingFailed(
    Guid BookingId,
    string Reason,
    string FailedAt,
    DateTimeOffset OccurredAt);

/// <summary>
/// Published by Payment Service when Stripe payment is processed.
/// Advances booking saga to the ticket-issuing step.
/// </summary>
public record PaymentProcessed(
    Guid BookingId,
    string PaymentIntentId,
    decimal Amount,
    string Currency,
    DateTimeOffset ProcessedAt);
```

**Step 4: Replace TBE.Common.csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>TBE.Common</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\TBE.Contracts\TBE.Contracts.csproj" />
    <PackageReference Include="MassTransit" Version="9.1.0" />
    <PackageReference Include="MassTransit.RabbitMQ" Version="9.1.0" />
  </ItemGroup>
</Project>
```

**Step 5: Create `src/shared/TBE.Common/Messaging/MassTransitServiceExtensions.cs`**

This provides a reusable registration helper that all 9 services can call. Services pass their own DbContext type and consumer registrations via Action delegates.

```csharp
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TBE.Common.Messaging;

/// <summary>
/// Shared MassTransit registration helper.
/// Services call AddTbeMassTransitWithRabbitMq() and pass their own consumers and outbox config.
/// </summary>
public static class MassTransitServiceExtensions
{
    /// <summary>
    /// Registers MassTransit with RabbitMQ transport.
    /// Callers provide consumer registration and optional outbox configuration.
    /// </summary>
    public static IServiceCollection AddTbeMassTransitWithRabbitMq(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureConsumers = null,
        Action<IBusRegistrationConfigurator>? configureOutbox = null)
    {
        services.AddMassTransit(x =>
        {
            // Register service-specific consumers
            configureConsumers?.Invoke(x);

            // Register outbox (called before UsingRabbitMq per MassTransit docs)
            configureOutbox?.Invoke(x);

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(
                    configuration["RabbitMQ:Host"] ?? "rabbitmq",
                    "/",
                    h =>
                    {
                        h.Username(configuration["RabbitMQ:Username"] ?? "guest");
                        h.Password(configuration["RabbitMQ:Password"] ?? "guest");
                    });

                // Apply outbox to ALL receive endpoints automatically
                cfg.AddConfigureEndpointsCallback((context, name, endpointCfg) =>
                {
                    // Outbox is applied per-endpoint when configured
                    // Services that configure outbox pass their DbContext via configureOutbox
                });

                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
```

**Step 6: Add both projects to TBE.sln**

```bash
dotnet sln TBE.sln add src/shared/TBE.Contracts/TBE.Contracts.csproj
dotnet sln TBE.sln add src/shared/TBE.Common/TBE.Common.csproj
```
  </action>
  <verify>
    <automated>cd "C:/Users/zhdishaq/source/repos/TBE" && grep "BookingInitiated\|BookingConfirmed\|BookingFailed\|PaymentProcessed" src/shared/TBE.Contracts/Events/BookingEvents.cs | wc -l && grep "MassTransit" src/shared/TBE.Common/TBE.Common.csproj && dotnet build src/shared/TBE.Contracts/TBE.Contracts.csproj && dotnet build src/shared/TBE.Common/TBE.Common.csproj</automated>
  </verify>
  <acceptance_criteria>
    - `src/shared/TBE.Contracts/Events/BookingEvents.cs` contains exactly 4 record types: `BookingInitiated`, `BookingConfirmed`, `BookingFailed`, `PaymentProcessed`
    - All 4 records are in namespace `TBE.Contracts.Events`
    - `src/shared/TBE.Common/TBE.Common.csproj` references `MassTransit` Version="9.1.0" and `MassTransit.RabbitMQ` Version="9.1.0"
    - `src/shared/TBE.Common/Messaging/MassTransitServiceExtensions.cs` contains method `AddTbeMassTransitWithRabbitMq`
    - `dotnet build src/shared/TBE.Contracts` exits code 0
    - `dotnet build src/shared/TBE.Common` exits code 0
    - Both projects appear in `dotnet sln TBE.sln list` output
  </acceptance_criteria>
  <done>TBE.Contracts defines 4 canonical event records; TBE.Common provides AddTbeMassTransitWithRabbitMq() extension; both compile and are registered in TBE.sln</done>
</task>

<task type="auto">
  <name>Task 2: Scaffold BookingService (3 projects) with MassTransit outbox on BookingDbContext and TestBookingConsumer</name>
  <read_first>
    - src/shared/TBE.Contracts/Events/BookingEvents.cs (just created — use exact type names)
    - src/shared/TBE.Common/Messaging/MassTransitServiceExtensions.cs (just created — call AddTbeMassTransitWithRabbitMq)
    - .planning/phases/01-infrastructure-foundation/01-RESEARCH.md — "MassTransit 8.x Outbox Setup" section (applies to 9.1.0); "DbContext Configuration (OnModelCreating)" section; "Typical .csproj Content per Layer Type" section
    - .planning/phases/01-infrastructure-foundation/01-CONTEXT.md — D-03 (3 projects per service pattern); D-07 (ProjectReference not NuGet)
  </read_first>
  <files>
    src/services/BookingService/BookingService.API/BookingService.API.csproj,
    src/services/BookingService/BookingService.API/Program.cs,
    src/services/BookingService/BookingService.API/Dockerfile,
    src/services/BookingService/BookingService.Application/BookingService.Application.csproj,
    src/services/BookingService/BookingService.Application/Consumers/TestBookingConsumer.cs,
    src/services/BookingService/BookingService.Infrastructure/BookingService.Infrastructure.csproj,
    src/services/BookingService/BookingService.Infrastructure/BookingDbContext.cs,
    TBE.sln
  </files>
  <action>
**Step 1: Scaffold the 3 BookingService projects**

```bash
mkdir -p src/services/BookingService/BookingService.API
mkdir -p src/services/BookingService/BookingService.Application/Consumers
mkdir -p src/services/BookingService/BookingService.Infrastructure/Migrations

dotnet new webapi     -n BookingService.API            -o src/services/BookingService/BookingService.API --no-openapi
dotnet new classlib   -n BookingService.Application    -o src/services/BookingService/BookingService.Application
dotnet new classlib   -n BookingService.Infrastructure -o src/services/BookingService/BookingService.Infrastructure
```

**Step 2: BookingService.API.csproj** — replace generated:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>TBE.BookingService.API</RootNamespace>
    <AssemblyName>BookingService.API</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\..\..\shared\TBE.Contracts\TBE.Contracts.csproj" />
    <ProjectReference Include="..\..\..\..\shared\TBE.Common\TBE.Common.csproj" />
    <ProjectReference Include="..\BookingService.Application\BookingService.Application.csproj" />
    <ProjectReference Include="..\BookingService.Infrastructure\BookingService.Infrastructure.csproj" />
    <PackageReference Include="Serilog.AspNetCore" Version="10.0.0" />
    <PackageReference Include="Serilog.Formatting.Compact" Version="3.0.0" />
    <PackageReference Include="AspNetCore.HealthChecks.SqlServer" Version="9.0.0" />
    <PackageReference Include="AspNetCore.HealthChecks.Rabbitmq" Version="9.0.0" />
    <PackageReference Include="AspNetCore.HealthChecks.Redis" Version="9.0.0" />
    <PackageReference Include="MassTransit.RabbitMQ" Version="9.1.0" />
    <PackageReference Include="MassTransit.EntityFrameworkCore" Version="9.1.0" />
  </ItemGroup>
</Project>
```

**Step 3: BookingService.Application.csproj** — replace generated:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>TBE.BookingService.Application</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\..\..\shared\TBE.Contracts\TBE.Contracts.csproj" />
    <ProjectReference Include="..\..\..\..\shared\TBE.Common\TBE.Common.csproj" />
    <PackageReference Include="MassTransit" Version="9.1.0" />
  </ItemGroup>
</Project>
```

**Step 4: BookingService.Infrastructure.csproj** — replace generated:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>TBE.BookingService.Infrastructure</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\BookingService.Application\BookingService.Application.csproj" />
    <ProjectReference Include="..\..\..\..\shared\TBE.Contracts\TBE.Contracts.csproj" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.25" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="8.0.25">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="MassTransit.EntityFrameworkCore" Version="9.1.0" />
  </ItemGroup>
</Project>
```

**Step 5: Create `src/services/BookingService/BookingService.Infrastructure/BookingDbContext.cs`**

```csharp
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace TBE.BookingService.Infrastructure;

public class BookingDbContext : DbContext
{
    public BookingDbContext(DbContextOptions<BookingDbContext> options) : base(options)
    {
    }

    // Booking domain entities will be added in Phase 3
    // Phase 1: Only outbox tables are created here

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // MassTransit outbox tables
        // Creates: InboxState, OutboxMessage, OutboxState
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
```

**Step 6: Create `src/services/BookingService/BookingService.Application/Consumers/TestBookingConsumer.cs`**

```csharp
using MassTransit;
using Microsoft.Extensions.Logging;
using TBE.Contracts.Events;

namespace TBE.BookingService.Application.Consumers;

/// <summary>
/// Phase 1 test consumer — verifies end-to-end RabbitMQ message delivery.
/// Replaced by the booking saga state machine in Phase 3.
/// </summary>
public class TestBookingConsumer : IConsumer<BookingInitiated>
{
    private readonly ILogger<TestBookingConsumer> _logger;

    public TestBookingConsumer(ILogger<TestBookingConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<BookingInitiated> context)
    {
        _logger.LogInformation(
            "TestBookingConsumer received BookingInitiated: BookingId={BookingId}, Channel={Channel}, ProductType={ProductType}",
            context.Message.BookingId,
            context.Message.Channel,
            context.Message.ProductType);

        return Task.CompletedTask;
    }
}
```

**Step 7: Create `src/services/BookingService/BookingService.API/Program.cs`**

```csharp
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Formatting.Compact;
using TBE.BookingService.Application.Consumers;
using TBE.BookingService.Infrastructure;
using TBE.Common.Messaging;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new CompactJsonFormatter())
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) =>
        configuration.ReadFrom.Configuration(context.Configuration)
                     .ReadFrom.Services(services)
                     .Enrich.FromLogContext()
                     .Enrich.WithProperty("Service", "BookingService"));

    // Database
    builder.Services.AddDbContext<BookingDbContext>(options =>
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("BookingDb"),
            sql => sql.EnableRetryOnFailure(maxRetryCount: 3)));

    // MassTransit with RabbitMQ and outbox
    builder.Services.AddTbeMassTransitWithRabbitMq(
        builder.Configuration,
        configureConsumers: x =>
        {
            x.AddConsumer<TestBookingConsumer>();
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

    // Health checks
    builder.Services.AddHealthChecks()
        .AddSqlServer(
            builder.Configuration.GetConnectionString("BookingDb")!,
            name: "booking-db",
            tags: new[] { "db", "sql" })
        .AddRabbitMQ(
            rabbitConnectionFactory: sp =>
            {
                var factory = new RabbitMQ.Client.ConnectionFactory
                {
                    HostName = builder.Configuration["RabbitMQ:Host"] ?? "rabbitmq",
                    UserName = builder.Configuration["RabbitMQ:Username"] ?? "guest",
                    Password = builder.Configuration["RabbitMQ:Password"] ?? "guest"
                };
                return factory.CreateConnectionAsync().GetAwaiter().GetResult();
            },
            name: "rabbitmq",
            tags: new[] { "messaging" });

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.MapHealthChecks("/health");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "BookingService terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
```

**Step 8: Create `src/services/BookingService/BookingService.API/Dockerfile`**

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/services/BookingService/BookingService.API/BookingService.API.csproj", "src/services/BookingService/BookingService.API/"]
COPY ["src/services/BookingService/BookingService.Application/BookingService.Application.csproj", "src/services/BookingService/BookingService.Application/"]
COPY ["src/services/BookingService/BookingService.Infrastructure/BookingService.Infrastructure.csproj", "src/services/BookingService/BookingService.Infrastructure/"]
COPY ["src/shared/TBE.Contracts/TBE.Contracts.csproj", "src/shared/TBE.Contracts/"]
COPY ["src/shared/TBE.Common/TBE.Common.csproj", "src/shared/TBE.Common/"]
RUN dotnet restore "src/services/BookingService/BookingService.API/BookingService.API.csproj"
COPY . .
WORKDIR "/src/src/services/BookingService/BookingService.API"
RUN dotnet build "BookingService.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "BookingService.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BookingService.API.dll"]
```

**Step 9: Add all 3 projects to TBE.sln**

```bash
dotnet sln TBE.sln add src/services/BookingService/BookingService.API/BookingService.API.csproj
dotnet sln TBE.sln add src/services/BookingService/BookingService.Application/BookingService.Application.csproj
dotnet sln TBE.sln add src/services/BookingService/BookingService.Infrastructure/BookingService.Infrastructure.csproj
```
  </action>
  <verify>
    <automated>cd "C:/Users/zhdishaq/source/repos/TBE" && grep "AddInboxStateEntity\|AddOutboxMessageEntity\|AddOutboxStateEntity" src/services/BookingService/BookingService.Infrastructure/BookingDbContext.cs | wc -l && grep "TestBookingConsumer" src/services/BookingService/BookingService.Application/Consumers/TestBookingConsumer.cs && dotnet build src/services/BookingService/BookingService.API/BookingService.API.csproj -c Release 2>&1 | tail -5</automated>
  </verify>
  <acceptance_criteria>
    - `BookingDbContext.cs` contains all 3 outbox registration calls: `modelBuilder.AddInboxStateEntity()`, `modelBuilder.AddOutboxMessageEntity()`, `modelBuilder.AddOutboxStateEntity()`
    - `TestBookingConsumer.cs` implements `IConsumer<BookingInitiated>` and contains `_logger.LogInformation`
    - `BookingService.API/Program.cs` calls `AddTbeMassTransitWithRabbitMq` from `TBE.Common.Messaging`
    - `BookingService.API/Program.cs` calls `AddEntityFrameworkOutbox<BookingDbContext>` with `o.UseBusOutbox()`
    - `BookingService.Infrastructure.csproj` references `MassTransit.EntityFrameworkCore` Version="9.1.0"
    - `BookingService.Infrastructure.csproj` references `Microsoft.EntityFrameworkCore.SqlServer` Version="8.0.25"
    - `dotnet build src/services/BookingService/BookingService.API/BookingService.API.csproj` exits code 0
    - All 3 BookingService projects appear in `dotnet sln TBE.sln list`
  </acceptance_criteria>
  <done>BookingService is fully scaffolded with MassTransit 9.1.0 outbox wired to BookingDbContext; TestBookingConsumer implements IConsumer&lt;BookingInitiated&gt;; all 3 projects added to TBE.sln and compile clean</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| RabbitMQ AMQP port | Services connect to RabbitMQ with username/password credentials from env vars |
| OutboxMessage table | Messages at rest in MSSQL between transaction commit and RabbitMQ dispatch |
| Consumer endpoints | Any service that can publish a message to an exchange can send to consumers |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-03-01 | Tampering | RabbitMQ messages without payload validation | accept | Phase 1 uses stub consumers; message validation added with FluentValidation in Phase 3 saga consumers |
| T-03-02 | Spoofing | Any service can publish to any exchange (no per-exchange authorization on RabbitMQ) | accept | Internal Docker bridge only; RabbitMQ user `tbe_admin` replaces guest/guest; per-vhost authorization is Phase 7 hardening |
| T-03-03 | Information Disclosure | OutboxMessage table contains message payload (may include PII in Phase 3) | mitigate | OutboxMessage is in BookingDb (not shared); `MassTransit.EntityFrameworkCore` outbox handles cleanup via `OutboxState`; Phase 3 will enforce payload encryption for passport data |
| T-03-04 | Denial of Service | RabbitMQ consumer poison message causes endless requeue loop | mitigate | MassTransit 9 has built-in dead-letter exchange support; configure `UseMessageRetry` with limit in Phase 3 consumers; Phase 1 TestBookingConsumer always returns Task.CompletedTask (never throws) |
| T-03-05 | Elevation of Privilege | RabbitMQ credentials from env vars visible in Docker inspect | accept | Acceptable for dev; production uses secrets manager (Phase 7); credentials are not hardcoded |
</threat_model>

<verification>
After both tasks complete:

```bash
# All 5 new projects compile
dotnet build src/shared/TBE.Contracts
dotnet build src/shared/TBE.Common
dotnet build src/services/BookingService/BookingService.Infrastructure
dotnet build src/services/BookingService/BookingService.Application
dotnet build src/services/BookingService/BookingService.API

# Outbox tables registered in DbContext
grep -c "AddOutboxMessageEntity\|AddInboxStateEntity\|AddOutboxStateEntity" \
  src/services/BookingService/BookingService.Infrastructure/BookingDbContext.cs
# Expected: 3

# MassTransit version is 9.1.0 (not 8.x)
grep "9.1.0" src/services/BookingService/BookingService.API/BookingService.API.csproj

# TBE.Contracts has no external package dependencies (pure contracts)
grep "PackageReference" src/shared/TBE.Contracts/TBE.Contracts.csproj
# Expected: empty (no output)
```
</verification>

<success_criteria>
- `TBE.Contracts` contains 4 canonical event records in namespace `TBE.Contracts.Events`
- `TBE.Common` provides `AddTbeMassTransitWithRabbitMq()` extension referencing MassTransit 9.1.0
- `BookingDbContext` registers all 3 outbox tables via `AddInboxStateEntity/AddOutboxMessageEntity/AddOutboxStateEntity`
- `TestBookingConsumer` implements `IConsumer<BookingInitiated>` and logs received message fields
- `BookingService.API/Program.cs` wires `AddTbeMassTransitWithRabbitMq` with outbox configured for BookingDbContext
- All 5 projects compile clean
- MassTransit version used everywhere is 9.1.0 (not 8.x)
</success_criteria>

<output>
After completion, create `.planning/phases/01-infrastructure-foundation/01-masstransit-rabbitmq-SUMMARY.md` with:
- Files created
- MassTransit version confirmation (9.1.0)
- Outbox table names created
- Consumer topology (exchange names derived from TBE.Contracts.Events.*)
- Build verification results
- Any deviations and why
</output>
