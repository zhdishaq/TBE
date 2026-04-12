---
phase: 01-infrastructure-foundation
plan: 04
type: execute
wave: 1
depends_on: []
files_modified:
  - src/tools/TBE.DbMigrator/TBE.DbMigrator.csproj
  - src/tools/TBE.DbMigrator/Program.cs
  - src/tools/TBE.DbMigrator/Dockerfile
  - src/services/BookingService/BookingService.Infrastructure/BookingDbContext.cs
  - src/services/PaymentService/PaymentService.API/PaymentService.API.csproj
  - src/services/PaymentService/PaymentService.API/Program.cs
  - src/services/PaymentService/PaymentService.Infrastructure/PaymentDbContext.cs
  - src/services/SearchService/SearchService.API/Program.cs
  - src/services/PricingService/PricingService.Infrastructure/PricingDbContext.cs
  - src/services/NotificationService/NotificationService.API/Program.cs
  - src/services/CrmService/CrmService.Infrastructure/CrmDbContext.cs
  - src/services/BackofficeService/BackofficeService.Infrastructure/BackofficeDbContext.cs
  - src/services/FlightConnectorService/FlightConnectorService.API/Program.cs
  - src/services/HotelConnectorService/HotelConnectorService.API/Program.cs
  - TBE.sln
autonomous: true
requirements:
  - INFRA-05
  - INFRA-06
  - INFRA-07

must_haves:
  truths:
    - "db-migrator container exits code 0 after creating all 7 service databases in MSSQL"
    - "Each service database contains the correct tables for that service (at minimum: MassTransit outbox tables for services that use messaging)"
    - "Every service exposes /health returning {status:Healthy} with db, rabbitmq, and redis entries"
    - "Every service emits structured JSON logs (CompactJsonFormatter) on each request"
    - "Redis connection is registered in each service's DI container"
    - "All 8 remaining service API projects (non-BookingService) compile clean"
  artifacts:
    - path: "src/tools/TBE.DbMigrator/Program.cs"
      provides: "Runs EF Core Migrate() for all 7 service DbContexts sequentially"
      contains: "MigrateAsync"
    - path: "src/tools/TBE.DbMigrator/Dockerfile"
      provides: "Containerised migrator that docker-compose uses with condition: service_completed_successfully"
      contains: "dotnet/sdk:8.0"
  key_links:
    - from: "docker-compose.yml db-migrator service"
      to: "src/tools/TBE.DbMigrator/Dockerfile"
      via: "build context . + dockerfile path"
      pattern: "TBE.DbMigrator"
    - from: "TBE.DbMigrator Program.cs"
      to: "each service's DbContext class"
      via: "ProjectReference in TBE.DbMigrator.csproj"
      pattern: "MigrateAsync"
---

<objective>
Scaffold the remaining 8 service API projects (all except BookingService, which is handled in Plan 03), create the `TBE.DbMigrator` tool that runs EF Core migrations for all 7 service databases on startup, and wire Redis + Serilog + health checks into every service's `Program.cs` using the shared patterns from `TBE.Common`.

Purpose: Completes INFRA-05 (Redis), INFRA-06 (health checks + structured logging), and INFRA-07 (MSSQL schemas). Without this plan, no service has a database schema, no service reports health, and no service logs in structured format.

Output: `TBE.DbMigrator` console app with Dockerfile; 8 remaining service stub API projects; all service databases created by the migrator on first boot.
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
<!-- Key patterns from RESEARCH.md — use verbatim -->

NuGet versions (verified):
- Microsoft.EntityFrameworkCore.SqlServer: 8.0.25
- Microsoft.EntityFrameworkCore.Tools: 8.0.25
- StackExchange.Redis: 2.12.14
- Serilog.AspNetCore: 10.0.0
- Serilog.Formatting.Compact: 3.0.0
- AspNetCore.HealthChecks.SqlServer: 9.0.0
- AspNetCore.HealthChecks.Rabbitmq: 9.0.0
- AspNetCore.HealthChecks.Redis: 9.0.0

db-migrator pattern: console app, calls .MigrateAsync() per DbContext, exits 0 on success.
All 9 application services depend on db-migrator with condition: service_completed_successfully.

Service database map:
- BookingDb → BookingDbContext (Plan 03)
- PaymentDb → PaymentDbContext
- PricingDb → PricingDbContext
- NotificationDb → NotificationDbContext
- CrmDb → CrmDbContext
- BackofficeDb → BackofficeDbContext
- FlightConnectorDb → (migrator creates empty DB; service is stateless)
- HotelConnectorDb → (migrator creates empty DB; service is stateless)
- SearchDb → (migrator creates empty DB; SearchService uses Redis only)

Stateless services (FlightConnectorService, HotelConnectorService):
- Only API + Application projects (no Infrastructure/DbContext per RESEARCH.md)
- Worker service (NotificationService uses Microsoft.NET.Sdk.Worker)

Health check JSON response (with UIResponseWriter):
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0123456",
  "entries": {
    "booking-db": { "status": "Healthy" },
    "rabbitmq": { "status": "Healthy" },
    "redis": { "status": "Healthy" }
  }
}
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Create TBE.DbMigrator console app — migrates all 7 service databases on startup</name>
  <read_first>
    - .planning/phases/01-infrastructure-foundation/01-RESEARCH.md — "EF Core Multi-Service Migration Strategy" section; "Recommended: Separate Databases per Service" section
    - docker-compose.yml (from Plan 01) — verify db-migrator environment variable names match what Program.cs reads
    - src/services/BookingService/BookingService.Infrastructure/BookingDbContext.cs (from Plan 03) — verify class name
  </read_first>
  <files>
    src/tools/TBE.DbMigrator/TBE.DbMigrator.csproj,
    src/tools/TBE.DbMigrator/Program.cs,
    src/tools/TBE.DbMigrator/Dockerfile,
    TBE.sln
  </files>
  <action>
**Step 1: Create the TBE.DbMigrator project**

```bash
mkdir -p src/tools/TBE.DbMigrator
dotnet new console -n TBE.DbMigrator -o src/tools/TBE.DbMigrator
```

**Step 2: TBE.DbMigrator.csproj** — replace generated:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>TBE.DbMigrator</RootNamespace>
    <AssemblyName>TBE.DbMigrator</AssemblyName>
    <OutputType>Exe</OutputType>
  </PropertyGroup>
  <ItemGroup>
    <!-- Reference each service's Infrastructure project to access its DbContext -->
    <ProjectReference Include="..\..\services\BookingService\BookingService.Infrastructure\BookingService.Infrastructure.csproj" />
    <ProjectReference Include="..\..\services\PaymentService\PaymentService.Infrastructure\PaymentService.Infrastructure.csproj" />
    <ProjectReference Include="..\..\services\PricingService\PricingService.Infrastructure\PricingService.Infrastructure.csproj" />
    <ProjectReference Include="..\..\services\NotificationService\NotificationService.Infrastructure\NotificationService.Infrastructure.csproj" />
    <ProjectReference Include="..\..\services\CrmService\CrmService.Infrastructure\CrmService.Infrastructure.csproj" />
    <ProjectReference Include="..\..\services\BackofficeService\BackofficeService.Infrastructure\BackofficeService.Infrastructure.csproj" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.25" />
    <PackageReference Include="Microsoft.Extensions.Configuration" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.EnvironmentVariables" Version="8.0.0" />
    <PackageReference Include="Serilog.AspNetCore" Version="10.0.0" />
    <PackageReference Include="Serilog.Formatting.Compact" Version="3.0.0" />
  </ItemGroup>
</Project>
```

Note: FlightConnectorService, HotelConnectorService, and SearchService have no Infrastructure project (stateless / Redis-only). The migrator creates empty databases for these by running `EnsureCreatedAsync` with a plain DbContext configured against each connection string.

**Step 3: TBE.DbMigrator/Program.cs**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Formatting.Compact;
using TBE.BookingService.Infrastructure;
using TBE.PaymentService.Infrastructure;
using TBE.PricingService.Infrastructure;
using TBE.NotificationService.Infrastructure;
using TBE.CrmService.Infrastructure;
using TBE.BackofficeService.Infrastructure;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new CompactJsonFormatter())
    .CreateBootstrapLogger();

try
{
    Log.Information("TBE.DbMigrator starting");

    var config = new ConfigurationBuilder()
        .AddEnvironmentVariables()
        .Build();

    async Task MigrateAsync<TContext>(string connectionStringKey) where TContext : DbContext
    {
        var connectionString = config[$"ConnectionStrings__{connectionStringKey}"]
            ?? config.GetConnectionString(connectionStringKey)
            ?? throw new InvalidOperationException($"Missing connection string: {connectionStringKey}");

        Log.Information("Migrating {DbContext} using key {Key}", typeof(TContext).Name, connectionStringKey);

        var options = new DbContextOptionsBuilder<TContext>()
            .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null))
            .Options;

        using var ctx = (TContext)Activator.CreateInstance(typeof(TContext), options)!;
        await ctx.Database.MigrateAsync();
        Log.Information("{DbContext} migration complete", typeof(TContext).Name);
    }

    // Migrate each service database sequentially
    // Order does not matter — each DbContext targets a separate database
    await MigrateAsync<BookingDbContext>("BookingDb");
    await MigrateAsync<PaymentDbContext>("PaymentDb");
    await MigrateAsync<PricingDbContext>("PricingDb");
    await MigrateAsync<NotificationDbContext>("NotificationDb");
    await MigrateAsync<CrmDbContext>("CrmDb");
    await MigrateAsync<BackofficeDbContext>("BackofficeDb");

    // Stateless services (no DbContext): ensure the database exists via a plain connection
    // Uses EnsureCreated with minimal DbContext for SearchDb, FlightConnectorDb, HotelConnectorDb
    async Task EnsureDbExistsAsync(string connectionStringKey)
    {
        var connectionString = config[$"ConnectionStrings__{connectionStringKey}"]
            ?? config.GetConnectionString(connectionStringKey)
            ?? throw new InvalidOperationException($"Missing connection string: {connectionStringKey}");

        Log.Information("Ensuring database exists for {Key}", connectionStringKey);
        var options = new DbContextOptionsBuilder<DbContext>()
            .UseSqlServer(connectionString)
            .Options;
        using var ctx = new EmptyDbContext(options);
        await ctx.Database.EnsureCreatedAsync();
        Log.Information("Database {Key} exists", connectionStringKey);
    }

    await EnsureDbExistsAsync("SearchDb");
    await EnsureDbExistsAsync("FlightConnectorDb");
    await EnsureDbExistsAsync("HotelConnectorDb");

    Log.Information("TBE.DbMigrator completed successfully — all databases ready");
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "TBE.DbMigrator failed — check MSSQL connection and credentials");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

// Minimal DbContext for stateless services that only need a database to exist
public class EmptyDbContext : DbContext
{
    public EmptyDbContext(DbContextOptions options) : base(options) { }
}
```

**Step 4: TBE.DbMigrator/Dockerfile**

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy all service Infrastructure projects that DbMigrator references
COPY ["src/tools/TBE.DbMigrator/TBE.DbMigrator.csproj", "src/tools/TBE.DbMigrator/"]
COPY ["src/services/BookingService/BookingService.Infrastructure/BookingService.Infrastructure.csproj", "src/services/BookingService/BookingService.Infrastructure/"]
COPY ["src/services/BookingService/BookingService.Application/BookingService.Application.csproj", "src/services/BookingService/BookingService.Application/"]
COPY ["src/services/PaymentService/PaymentService.Infrastructure/PaymentService.Infrastructure.csproj", "src/services/PaymentService/PaymentService.Infrastructure/"]
COPY ["src/services/PaymentService/PaymentService.Application/PaymentService.Application.csproj", "src/services/PaymentService/PaymentService.Application/"]
COPY ["src/services/PricingService/PricingService.Infrastructure/PricingService.Infrastructure.csproj", "src/services/PricingService/PricingService.Infrastructure/"]
COPY ["src/services/PricingService/PricingService.Application/PricingService.Application.csproj", "src/services/PricingService/PricingService.Application/"]
COPY ["src/services/NotificationService/NotificationService.Infrastructure/NotificationService.Infrastructure.csproj", "src/services/NotificationService/NotificationService.Infrastructure/"]
COPY ["src/services/NotificationService/NotificationService.Application/NotificationService.Application.csproj", "src/services/NotificationService/NotificationService.Application/"]
COPY ["src/services/CrmService/CrmService.Infrastructure/CrmService.Infrastructure.csproj", "src/services/CrmService/CrmService.Infrastructure/"]
COPY ["src/services/CrmService/CrmService.Application/CrmService.Application.csproj", "src/services/CrmService/CrmService.Application/"]
COPY ["src/services/BackofficeService/BackofficeService.Infrastructure/BackofficeService.Infrastructure.csproj", "src/services/BackofficeService/BackofficeService.Infrastructure/"]
COPY ["src/services/BackofficeService/BackofficeService.Application/BackofficeService.Application.csproj", "src/services/BackofficeService/BackofficeService.Application/"]
COPY ["src/shared/TBE.Contracts/TBE.Contracts.csproj", "src/shared/TBE.Contracts/"]
COPY ["src/shared/TBE.Common/TBE.Common.csproj", "src/shared/TBE.Common/"]
RUN dotnet restore "src/tools/TBE.DbMigrator/TBE.DbMigrator.csproj"

COPY . .
WORKDIR "/src/src/tools/TBE.DbMigrator"
RUN dotnet publish "TBE.DbMigrator.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TBE.DbMigrator.dll"]
```

**Step 5: Add to TBE.sln**

```bash
dotnet sln TBE.sln add src/tools/TBE.DbMigrator/TBE.DbMigrator.csproj
```
  </action>
  <verify>
    <automated>cd "C:/Users/zhdishaq/source/repos/TBE" && grep -c "MigrateAsync" src/tools/TBE.DbMigrator/Program.cs && grep "return 0\|return 1" src/tools/TBE.DbMigrator/Program.cs && grep "TBE.DbMigrator" TBE.sln</automated>
  </verify>
  <acceptance_criteria>
    - `src/tools/TBE.DbMigrator/Program.cs` contains exactly 6 calls to `MigrateAsync<` (one per service with a DbContext)
    - `src/tools/TBE.DbMigrator/Program.cs` contains `return 0` on success and `return 1` in the catch block (docker-compose reads exit code for `service_completed_successfully`)
    - `src/tools/TBE.DbMigrator/Program.cs` contains 3 calls to `EnsureDbExistsAsync` for SearchDb, FlightConnectorDb, HotelConnectorDb
    - `TBE.DbMigrator.csproj` references exactly 6 Infrastructure `ProjectReference` entries
    - `src/tools/TBE.DbMigrator/Dockerfile` exists and contains `dotnet/sdk:8.0`
    - `dotnet sln TBE.sln list` output includes `TBE.DbMigrator.csproj`
  </acceptance_criteria>
  <done>TBE.DbMigrator runs all 6 service EF Core migrations sequentially, ensures 3 additional empty databases exist for stateless services, exits 0 on success (1 on failure), and has a Dockerfile for Docker Compose integration</done>
</task>

<task type="auto">
  <name>Task 2: Scaffold remaining 8 service projects with Serilog, Redis, and health checks</name>
  <read_first>
    - .planning/phases/01-infrastructure-foundation/01-CONTEXT.md — D-03 (3 projects per service); D-05 (service names); note FlightConnectorService and HotelConnectorService are stateless (API + Application only, no Infrastructure)
    - .planning/phases/01-infrastructure-foundation/01-RESEARCH.md — "Serilog Configuration for Docker"; "ASP.NET Core Health Checks"; "NotificationService uses Microsoft.NET.Sdk.Worker"; csproj templates
    - src/services/BookingService/BookingService.API/Program.cs (from Plan 03) — use same Program.cs structure for all services
  </read_first>
  <files>
    src/services/PaymentService/PaymentService.API/PaymentService.API.csproj,
    src/services/PaymentService/PaymentService.API/Program.cs,
    src/services/PaymentService/PaymentService.API/Dockerfile,
    src/services/PaymentService/PaymentService.Application/PaymentService.Application.csproj,
    src/services/PaymentService/PaymentService.Infrastructure/PaymentService.Infrastructure.csproj,
    src/services/PaymentService/PaymentService.Infrastructure/PaymentDbContext.cs,
    src/services/SearchService/SearchService.API/SearchService.API.csproj,
    src/services/SearchService/SearchService.API/Program.cs,
    src/services/SearchService/SearchService.API/Dockerfile,
    src/services/SearchService/SearchService.Application/SearchService.Application.csproj,
    src/services/FlightConnectorService/FlightConnectorService.API/FlightConnectorService.API.csproj,
    src/services/FlightConnectorService/FlightConnectorService.API/Program.cs,
    src/services/FlightConnectorService/FlightConnectorService.API/Dockerfile,
    src/services/FlightConnectorService/FlightConnectorService.Application/FlightConnectorService.Application.csproj,
    src/services/HotelConnectorService/HotelConnectorService.API/HotelConnectorService.API.csproj,
    src/services/HotelConnectorService/HotelConnectorService.API/Program.cs,
    src/services/HotelConnectorService/HotelConnectorService.API/Dockerfile,
    src/services/HotelConnectorService/HotelConnectorService.Application/HotelConnectorService.Application.csproj,
    src/services/PricingService/PricingService.API/PricingService.API.csproj,
    src/services/PricingService/PricingService.API/Program.cs,
    src/services/PricingService/PricingService.API/Dockerfile,
    src/services/PricingService/PricingService.Application/PricingService.Application.csproj,
    src/services/PricingService/PricingService.Infrastructure/PricingService.Infrastructure.csproj,
    src/services/PricingService/PricingService.Infrastructure/PricingDbContext.cs,
    src/services/NotificationService/NotificationService.API/NotificationService.API.csproj,
    src/services/NotificationService/NotificationService.API/Program.cs,
    src/services/NotificationService/NotificationService.API/Dockerfile,
    src/services/NotificationService/NotificationService.Application/NotificationService.Application.csproj,
    src/services/NotificationService/NotificationService.Infrastructure/NotificationService.Infrastructure.csproj,
    src/services/NotificationService/NotificationService.Infrastructure/NotificationDbContext.cs,
    src/services/CrmService/CrmService.API/CrmService.API.csproj,
    src/services/CrmService/CrmService.API/Program.cs,
    src/services/CrmService/CrmService.API/Dockerfile,
    src/services/CrmService/CrmService.Application/CrmService.Application.csproj,
    src/services/CrmService/CrmService.Infrastructure/CrmService.Infrastructure.csproj,
    src/services/CrmService/CrmService.Infrastructure/CrmDbContext.cs,
    src/services/BackofficeService/BackofficeService.API/BackofficeService.API.csproj,
    src/services/BackofficeService/BackofficeService.API/Program.cs,
    src/services/BackofficeService/BackofficeService.API/Dockerfile,
    src/services/BackofficeService/BackofficeService.Application/BackofficeService.Application.csproj,
    src/services/BackofficeService/BackofficeService.Infrastructure/BackofficeService.Infrastructure.csproj,
    src/services/BackofficeService/BackofficeService.Infrastructure/BackofficeDbContext.cs,
    TBE.sln
  </files>
  <action>
This task scaffolds all remaining 8 services following the same pattern established for BookingService in Plan 03. Below is the canonical template. Apply it to all 8 services with the substitutions noted.

**Services to scaffold (with substitution values):**

| Service | SDK | Has Infrastructure | DbContext Class | DB Connection Key | Has RabbitMQ |
|---------|-----|-------------------|-----------------|--------------------|--------------|
| PaymentService | Web | Yes | PaymentDbContext | PaymentDb | Yes |
| SearchService | Web | No (Redis only) | — | SearchDb (empty) | Yes |
| FlightConnectorService | Web | No (stateless) | — | — | Yes |
| HotelConnectorService | Web | No (stateless) | — | — | Yes |
| PricingService | Web | Yes | PricingDbContext | PricingDb | Yes |
| NotificationService | Worker | Yes | NotificationDbContext | NotificationDb | Yes |
| CrmService | Web | Yes | CrmDbContext | CrmDb | Yes |
| BackofficeService | Web | Yes | BackofficeDbContext | BackofficeDb | Yes |

---

**CANONICAL API .csproj TEMPLATE** (for services with Infrastructure + DB health check):

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>TBE.{ServiceName}.API</RootNamespace>
    <AssemblyName>{ServiceName}.API</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\..\..\shared\TBE.Contracts\TBE.Contracts.csproj" />
    <ProjectReference Include="..\..\..\..\shared\TBE.Common\TBE.Common.csproj" />
    <ProjectReference Include="..\{ServiceName}.Application\{ServiceName}.Application.csproj" />
    <ProjectReference Include="..\{ServiceName}.Infrastructure\{ServiceName}.Infrastructure.csproj" />
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

For stateless services (FlightConnectorService, HotelConnectorService): omit Infrastructure ProjectReference, SqlServer health check, and MassTransit.EntityFrameworkCore.

For SearchService: omit Infrastructure ProjectReference and SqlServer health check. Keep RabbitMQ + Redis.

For NotificationService: use `Microsoft.NET.Sdk.Worker` as Sdk instead of `Microsoft.NET.Sdk.Web`.

---

**CANONICAL INFRASTRUCTURE .csproj TEMPLATE:**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>TBE.{ServiceName}.Infrastructure</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\{ServiceName}.Application\{ServiceName}.Application.csproj" />
    <ProjectReference Include="..\..\..\..\shared\TBE.Contracts\TBE.Contracts.csproj" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.25" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="8.0.25">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="MassTransit.EntityFrameworkCore" Version="9.1.0" />
    <PackageReference Include="StackExchange.Redis" Version="2.12.14" />
  </ItemGroup>
</Project>
```

---

**CANONICAL DbContext TEMPLATE** (for all 5 services with Infrastructure):

```csharp
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace TBE.{ServiceName}.Infrastructure;

public class {ServiceName}DbContext : DbContext
{
    public {ServiceName}DbContext(DbContextOptions<{ServiceName}DbContext> options) : base(options)
    {
    }

    // Domain entities added in later phases

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // MassTransit outbox tables
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
```

Apply to: PaymentDbContext, PricingDbContext, NotificationDbContext, CrmDbContext, BackofficeDbContext.

---

**CANONICAL Program.cs TEMPLATE** (for services with DB + RabbitMQ + Redis):

```csharp
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Formatting.Compact;
using TBE.{ServiceName}.Infrastructure;
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
                     .Enrich.WithProperty("Service", "{ServiceName}"));

    builder.Services.AddDbContext<{ServiceName}DbContext>(options =>
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("{DbName}"),
            sql => sql.EnableRetryOnFailure(maxRetryCount: 3)));

    builder.Services.AddTbeMassTransitWithRabbitMq(
        builder.Configuration,
        configureOutbox: x =>
        {
            x.AddEntityFrameworkOutbox<{ServiceName}DbContext>(o =>
            {
                o.UseSqlServer();
                o.UseBusOutbox();
                o.QueryDelay = TimeSpan.FromSeconds(5);
                o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
            });
        });

    builder.Services.AddHealthChecks()
        .AddSqlServer(
            builder.Configuration.GetConnectionString("{DbName}")!,
            name: "{service-name}-db",
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
            tags: new[] { "messaging" })
        .AddRedis(
            builder.Configuration["Redis:ConnectionString"]!,
            name: "redis",
            tags: new[] { "cache" });

    var app = builder.Build();
    app.UseSerilogRequestLogging();
    app.MapHealthChecks("/health");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "{ServiceName} terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
```

For SearchService (no DB): omit DbContext, remove SqlServer health check, keep RabbitMQ + Redis.
For FlightConnectorService, HotelConnectorService (stateless): remove DbContext and SqlServer health check entirely.
For NotificationService: use `var builder = Host.CreateApplicationBuilder(args)` + `builder.Services.AddHostedService<NotificationWorker>()` pattern instead of WebApplication; still add health checks and Serilog.

---

**CANONICAL Dockerfile TEMPLATE:**

Use the same pattern as BookingService.API/Dockerfile from Plan 03. Replace:
- `BookingService` → `{ServiceName}`
- Remove Infrastructure COPY line for stateless services

---

**SCAFFOLDING COMMANDS** — run all these from repository root:

```bash
# PaymentService
mkdir -p src/services/PaymentService/PaymentService.API src/services/PaymentService/PaymentService.Application src/services/PaymentService/PaymentService.Infrastructure
dotnet new webapi   -n PaymentService.API            -o src/services/PaymentService/PaymentService.API --no-openapi
dotnet new classlib -n PaymentService.Application    -o src/services/PaymentService/PaymentService.Application
dotnet new classlib -n PaymentService.Infrastructure -o src/services/PaymentService/PaymentService.Infrastructure

# SearchService (no Infrastructure)
mkdir -p src/services/SearchService/SearchService.API src/services/SearchService/SearchService.Application
dotnet new webapi   -n SearchService.API         -o src/services/SearchService/SearchService.API --no-openapi
dotnet new classlib -n SearchService.Application -o src/services/SearchService/SearchService.Application

# FlightConnectorService (stateless — no Infrastructure)
mkdir -p src/services/FlightConnectorService/FlightConnectorService.API src/services/FlightConnectorService/FlightConnectorService.Application
dotnet new webapi   -n FlightConnectorService.API         -o src/services/FlightConnectorService/FlightConnectorService.API --no-openapi
dotnet new classlib -n FlightConnectorService.Application -o src/services/FlightConnectorService/FlightConnectorService.Application

# HotelConnectorService (stateless — no Infrastructure)
mkdir -p src/services/HotelConnectorService/HotelConnectorService.API src/services/HotelConnectorService/HotelConnectorService.Application
dotnet new webapi   -n HotelConnectorService.API         -o src/services/HotelConnectorService/HotelConnectorService.API --no-openapi
dotnet new classlib -n HotelConnectorService.Application -o src/services/HotelConnectorService/HotelConnectorService.Application

# PricingService
mkdir -p src/services/PricingService/PricingService.API src/services/PricingService/PricingService.Application src/services/PricingService/PricingService.Infrastructure
dotnet new webapi   -n PricingService.API            -o src/services/PricingService/PricingService.API --no-openapi
dotnet new classlib -n PricingService.Application    -o src/services/PricingService/PricingService.Application
dotnet new classlib -n PricingService.Infrastructure -o src/services/PricingService/PricingService.Infrastructure

# NotificationService (Worker SDK)
mkdir -p src/services/NotificationService/NotificationService.API src/services/NotificationService/NotificationService.Application src/services/NotificationService/NotificationService.Infrastructure
dotnet new worker   -n NotificationService.API            -o src/services/NotificationService/NotificationService.API
dotnet new classlib -n NotificationService.Application    -o src/services/NotificationService/NotificationService.Application
dotnet new classlib -n NotificationService.Infrastructure -o src/services/NotificationService/NotificationService.Infrastructure

# CrmService
mkdir -p src/services/CrmService/CrmService.API src/services/CrmService/CrmService.Application src/services/CrmService/CrmService.Infrastructure
dotnet new webapi   -n CrmService.API            -o src/services/CrmService/CrmService.API --no-openapi
dotnet new classlib -n CrmService.Application    -o src/services/CrmService/CrmService.Application
dotnet new classlib -n CrmService.Infrastructure -o src/services/CrmService/CrmService.Infrastructure

# BackofficeService
mkdir -p src/services/BackofficeService/BackofficeService.API src/services/BackofficeService/BackofficeService.Application src/services/BackofficeService/BackofficeService.Infrastructure
dotnet new webapi   -n BackofficeService.API            -o src/services/BackofficeService/BackofficeService.API --no-openapi
dotnet new classlib -n BackofficeService.Application    -o src/services/BackofficeService/BackofficeService.Application
dotnet new classlib -n BackofficeService.Infrastructure -o src/services/BackofficeService/BackofficeService.Infrastructure
```

After scaffolding all 8 services, replace generated .csproj files with canonical templates (see above) and create DbContext files, Program.cs files, and Dockerfiles using the canonical patterns.

Then add all to TBE.sln:
```bash
# Add all 8 services' projects (all projects created above)
dotnet sln TBE.sln add \
  src/services/PaymentService/PaymentService.API/PaymentService.API.csproj \
  src/services/PaymentService/PaymentService.Application/PaymentService.Application.csproj \
  src/services/PaymentService/PaymentService.Infrastructure/PaymentService.Infrastructure.csproj \
  src/services/SearchService/SearchService.API/SearchService.API.csproj \
  src/services/SearchService/SearchService.Application/SearchService.Application.csproj \
  src/services/FlightConnectorService/FlightConnectorService.API/FlightConnectorService.API.csproj \
  src/services/FlightConnectorService/FlightConnectorService.Application/FlightConnectorService.Application.csproj \
  src/services/HotelConnectorService/HotelConnectorService.API/HotelConnectorService.API.csproj \
  src/services/HotelConnectorService/HotelConnectorService.Application/HotelConnectorService.Application.csproj \
  src/services/PricingService/PricingService.API/PricingService.API.csproj \
  src/services/PricingService/PricingService.Application/PricingService.Application.csproj \
  src/services/PricingService/PricingService.Infrastructure/PricingService.Infrastructure.csproj \
  src/services/NotificationService/NotificationService.API/NotificationService.API.csproj \
  src/services/NotificationService/NotificationService.Application/NotificationService.Application.csproj \
  src/services/NotificationService/NotificationService.Infrastructure/NotificationService.Infrastructure.csproj \
  src/services/CrmService/CrmService.API/CrmService.API.csproj \
  src/services/CrmService/CrmService.Application/CrmService.Application.csproj \
  src/services/CrmService/CrmService.Infrastructure/CrmService.Infrastructure.csproj \
  src/services/BackofficeService/BackofficeService.API/BackofficeService.API.csproj \
  src/services/BackofficeService/BackofficeService.Application/BackofficeService.Application.csproj \
  src/services/BackofficeService/BackofficeService.Infrastructure/BackofficeService.Infrastructure.csproj
```

Finally add an `appsettings.json` to each service API project with Serilog configuration (same structure as TBE.Gateway's appsettings.json — use service-specific override values only for MinimumLevel if needed).
  </action>
  <verify>
    <automated>cd "C:/Users/zhdishaq/source/repos/TBE" && dotnet build TBE.sln -c Release 2>&1 | grep -E "error|warning|succeeded|failed" | tail -20</automated>
  </verify>
  <acceptance_criteria>
    - `dotnet build TBE.sln` exits code 0 — ALL projects in the solution build clean
    - `dotnet sln TBE.sln list | wc -l` shows at least 31 projects (1 gateway + 2 shared + 1 migrator + 9 services × 3 projects − 5 missing Infrastructure for stateless services = ~30 projects)
    - Every service that has a DbContext (PaymentService, PricingService, NotificationService, CrmService, BackofficeService) has a `*DbContext.cs` containing `modelBuilder.AddOutboxMessageEntity()`
    - Every service Program.cs contains `WriteTo.Console(new CompactJsonFormatter())` or references it via Serilog configuration
    - Every service Program.cs contains `app.MapHealthChecks("/health")` (or for NotificationService: worker registers health check endpoint)
    - `src/services/NotificationService/NotificationService.API/NotificationService.API.csproj` contains `Sdk="Microsoft.NET.Sdk.Worker"`
    - `src/services/FlightConnectorService/FlightConnectorService.API/FlightConnectorService.API.csproj` does NOT contain an Infrastructure ProjectReference
  </acceptance_criteria>
  <done>All 8 remaining service API projects are scaffolded with canonical structure; every service has structured JSON logging, /health endpoint, and Redis health check; services with databases have DbContexts with outbox tables; NotificationService uses Worker SDK; stateless services have no Infrastructure project; entire solution builds clean</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| db-migrator → MSSQL SA | Migrator connects as SA with full access to create databases and tables |
| service → MSSQL | Each service connects to its own database using SA credentials (dev only) |
| service → Redis | Redis connection uses password from env var |
| /health endpoint | Exposes infrastructure connectivity status — unauthenticated in Phase 1 |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-04-01 | Information Disclosure | /health endpoint unauthenticated | accept | /health is intentionally public for Docker health check polling; does not expose PII or credentials; exposes connectivity status only — acceptable for dev; production should add IP allowlist or internal-only binding |
| T-04-02 | Elevation of Privilege | db-migrator runs as SA with unlimited MSSQL access | accept | Dev-only pattern; SA password from .env (git-ignored); production remediation documented: per-service SQL logins with GRANT on own schema only (Phase 7) |
| T-04-03 | Denial of Service | db-migrator failure blocks ALL 9 services (condition: service_completed_successfully) | mitigate | Program.cs returns exit code 1 on failure; db-migrator logs the failing DbContext before exiting; Docker Compose will not start application services if migrator exits 1 — clear failure signal |
| T-04-04 | Information Disclosure | Serilog CompactJsonFormatter logs may include sensitive request data | mitigate | `Microsoft` and `System` logging overridden to `Warning` in appsettings.json; request body logging NOT enabled (UseSerilogRequestLogging logs only path/method/status/duration — no body); passport data logging prevention enforced in Phase 3 |
| T-04-05 | Information Disclosure | Redis connection string with password in plain text env var | accept | Dev-only; password sourced from .env (git-ignored); production uses secrets manager injection via IConfiguration |
</threat_model>

<verification>
After both tasks complete:

```bash
# Full solution builds
dotnet build TBE.sln -c Release --no-incremental
# Expected: Build succeeded. 0 Error(s)

# All service DbContexts have outbox tables
for service in Payment Pricing Notification Crm Backoffice; do
  grep "AddOutboxMessageEntity" "src/services/${service}Service/${service}Service.Infrastructure/${service}DbContext.cs" && echo "${service}DbContext OK"
done

# db-migrator has correct exit codes
grep "return 0\|return 1" src/tools/TBE.DbMigrator/Program.cs

# NotificationService uses Worker SDK
grep "Sdk.Worker" src/services/NotificationService/NotificationService.API/NotificationService.API.csproj

# Stateless services have no Infrastructure project
ls src/services/FlightConnectorService/ | grep -v Infrastructure && echo "FlightConnector: no Infrastructure"
ls src/services/HotelConnectorService/ | grep -v Infrastructure && echo "HotelConnector: no Infrastructure"

# Solution project count
dotnet sln TBE.sln list | wc -l
```
</verification>

<success_criteria>
- `dotnet build TBE.sln` completes with 0 errors
- `TBE.DbMigrator/Program.cs` migrates 6 service databases and ensures 3 empty databases, exits 0/1 correctly
- All 5 service DbContexts (Payment, Pricing, Notification, Crm, Backoffice) have the 3 outbox table registrations
- Every service API Program.cs wires `UseSerilogRequestLogging()`, `MapHealthChecks("/health")`, and Serilog structured JSON via `CompactJsonFormatter`
- `NotificationService.API.csproj` uses `Microsoft.NET.Sdk.Worker` SDK
- `FlightConnectorService` and `HotelConnectorService` have 2 projects only (API + Application, no Infrastructure)
- All projects are registered in `TBE.sln`
- `StackExchange.Redis` 2.12.14 is referenced in Infrastructure projects that use Redis
</success_criteria>

<output>
After completion, create `.planning/phases/01-infrastructure-foundation/01-mssql-migrations-shared-SUMMARY.md` with:
- Total project count added to TBE.sln
- Build result (dotnet build TBE.sln exit code)
- Services with Infrastructure vs. stateless services list
- NuGet package versions confirmed
- Any deviations from the plan and why
</output>
