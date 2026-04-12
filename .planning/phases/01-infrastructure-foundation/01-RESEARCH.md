# Phase 1 Research: Infrastructure Foundation

**Researched:** 2026-04-12
**Domain:** .NET 8 microservices infrastructure — Docker Compose, YARP, Keycloak, MassTransit, EF Core, Serilog, Redis, MSSQL
**Confidence:** HIGH (package versions verified against NuGet registry; Docker healthcheck patterns verified via GitHub community and official docs; Keycloak import path verified via official docs)

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** Single `TBE.sln` at the repository root.
- **D-02:** Services flat under `src/services/` — no domain-grouped subdirectories.
- **D-03:** Each service has exactly 3 C# projects: `{ServiceName}.API`, `{ServiceName}.Application`, `{ServiceName}.Infrastructure`.
- **D-04:** API Gateway at `src/gateway/TBE.Gateway/` — single project, not split into layers.
- **D-05:** Service naming: `BookingService`, `PaymentService`, `SearchService`, `FlightConnectorService`, `HotelConnectorService`, `PricingService`, `NotificationService`, `CrmService`, `BackofficeService`. No `TBE.` prefix on folder names. C# namespaces use `TBE.{ServiceName}.{Layer}`.
- **D-06:** Two shared projects under `src/shared/`: `TBE.Contracts` and `TBE.Common`.
- **D-07:** Services reference shared projects via `<ProjectReference>`, not NuGet packages.
- **D-08:** No service may reference another service's projects directly.
- **D-09:** Dev environment uses a single `.env` file at the repository root; `.env` is git-ignored.
- **D-10:** `.env.example` committed with placeholder values for all secrets.
- **D-11:** `docker-compose.override.yml` git-ignored; `docker-compose.override.yml.example` committed.
- **D-12:** Production secrets injected as environment variables via `IConfiguration`.
- **D-13:** GDS credentials follow the same env-var pattern.

### Claude's Discretion

- Specific Docker image versions for MSSQL, RabbitMQ, Redis, Keycloak
- Keycloak realm import JSON format and exact client configuration details
- YARP route configuration specifics (path patterns, transforms)
- MassTransit exchange topology naming (final names for queues/exchanges)
- Serilog output template and logging level configuration per service
- EF Core migration strategy for multi-service startup ordering
- Health check response format details beyond `{"status":"Healthy"}`

### Deferred Ideas (OUT OF SCOPE)

- Kubernetes / Helm charts
- Elastic + Kibana logging stack
- Service mesh (Dapr, Linkerd)
- CI/CD pipeline configuration (Phase 7)
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| INFRA-01 | Docker Compose environment runs all services locally with a single command | Docker Compose section; health check dependency chains |
| INFRA-02 | YARP API gateway routes requests to correct microservices with JWT validation | YARP + Keycloak JWT section; per-route authorization policy pattern |
| INFRA-03 | Keycloak handles authentication for all three portals under one SSO session | Keycloak realm import section; 3-realm structure |
| INFRA-04 | RabbitMQ with MassTransit wires all inter-service async messaging with outbox pattern | MassTransit outbox section; NuGet versions confirmed |
| INFRA-05 | Redis provides search result caching, session management, and GDS rate-limit buffering | Serilog/Redis section; StackExchange.Redis version confirmed |
| INFRA-06 | Service health checks and structured logging in place | Health checks section; Serilog JSON config |
| INFRA-07 | MSSQL schemas initialized with migrations for all services at startup | EF Core migration strategy section |
</phase_requirements>

---

## Summary

Phase 1 is a pure infrastructure scaffolding phase — no business logic. The primary risk is not coding complexity but configuration complexity: nine services, four infrastructure containers, three Keycloak realms, and a migration strategy that must be race-condition-safe. Every technical question in this phase has a well-established answer in 2026; none require custom solutions.

The most important implementation insight is that MassTransit has moved to version 9.x (not 8.x as the previous stack research assumed). All package versions below are verified against the NuGet registry as of April 2026. The migration strategy should use a separate `db-migrator` Docker Compose service rather than running `Migrate()` in application startup — this avoids the race condition that occurs when 9 services simultaneously attempt to migrate the same MSSQL instance.

**Primary recommendation:** Scaffold all 9 × 3 projects + 2 shared + 1 gateway in a single `dotnet new` script; use a dedicated `db-migrator` compose service to sequence migrations before application services start; validate the entire stack with a single `docker-compose up --build` and the UAT checklist from ROADMAP.md.

---

## Docker Compose Health Checks

### MSSQL 2022

The MSSQL 2022 Linux image (`mcr.microsoft.com/mssql/server:2022-latest`) has moved its tools to `/opt/mssql-tools18/` in recent CU releases. The old path `/opt/mssql-tools/bin/sqlcmd` will silently fail.

[VERIFIED: GitHub community gist belgattitude/9979e5501d72ffa90c9460597dee8dca, confirmed by multiple testcontainers issue reports]

```yaml
mssql:
  image: mcr.microsoft.com/mssql/server:2022-latest
  environment:
    ACCEPT_EULA: "Y"
    MSSQL_SA_PASSWORD: "${MSSQL_SA_PASSWORD}"
  volumes:
    - mssql-data:/var/opt/mssql
  healthcheck:
    test: /opt/mssql-tools18/bin/sqlcmd -S localhost -C -U sa -P "$$MSSQL_SA_PASSWORD" -Q "SELECT 1" -b -o /dev/null
    interval: 10s
    timeout: 45s
    retries: 45
    start_period: 10s
```

Key flags:
- `-C` — trust the self-signed TLS certificate (required in mssql-tools18)
- `$$MSSQL_SA_PASSWORD` — double-dollar escapes the variable in compose YAML so the shell receives a literal `$`
- `-b` — exit non-zero on SQL error (makes the health check meaningful)
- `-o /dev/null` — suppress output noise

### RabbitMQ

[VERIFIED: RabbitMQ official Docker Hub image documentation]

```yaml
rabbitmq:
  image: rabbitmq:3.13-management
  environment:
    RABBITMQ_DEFAULT_USER: "${RABBITMQ_USER}"
    RABBITMQ_DEFAULT_PASS: "${RABBITMQ_PASSWORD}"
  volumes:
    - rabbitmq-data:/var/lib/rabbitmq
  healthcheck:
    test: ["CMD", "rabbitmq-diagnostics", "check_port_connectivity"]
    interval: 10s
    timeout: 10s
    retries: 10
    start_period: 30s
```

`rabbitmq-diagnostics check_port_connectivity` is the current recommended healthcheck — it verifies the AMQP port is accepting connections, not just that the process is running.

### Redis

[VERIFIED: Redis official Docker Hub documentation]

```yaml
redis:
  image: redis:7.2-alpine
  command: redis-server --appendonly yes --requirepass "${REDIS_PASSWORD}"
  volumes:
    - redis-data:/data
  healthcheck:
    test: ["CMD", "redis-cli", "-a", "${REDIS_PASSWORD}", "ping"]
    interval: 5s
    timeout: 5s
    retries: 10
    start_period: 5s
```

### Keycloak

Keycloak 26.x exposes a dedicated health endpoint on port 9000 (management port) when `KC_HEALTH_ENABLED=true`. The `/health/ready` endpoint is accurate — it reflects whether Keycloak has fully initialized, including database connectivity.

[VERIFIED: https://www.keycloak.org/server/health]
[VERIFIED: https://www.keycloak.org/observability/health]

```yaml
keycloak:
  image: quay.io/keycloak/keycloak:26.6.0
  command: start-dev --import-realm
  environment:
    KC_HEALTH_ENABLED: "true"
    KC_BOOTSTRAP_ADMIN_USERNAME: "${KEYCLOAK_ADMIN_USER}"
    KC_BOOTSTRAP_ADMIN_PASSWORD: "${KEYCLOAK_ADMIN_PASSWORD}"
    KC_DB: mssql
    KC_DB_URL: "jdbc:sqlserver://mssql:1433;databaseName=keycloak;encrypt=false"
    KC_DB_USERNAME: "${KEYCLOAK_DB_USER}"
    KC_DB_PASSWORD: "${KEYCLOAK_DB_PASSWORD}"
  volumes:
    - ./infra/keycloak/realms:/opt/keycloak/data/import
  ports:
    - "8080:8080"
    - "9000:9000"
  depends_on:
    mssql:
      condition: service_healthy
  healthcheck:
    test: ["CMD-SHELL", "exec 3<>/dev/tcp/127.0.0.1/9000; echo -e 'GET /health/ready HTTP/1.1\\r\\nhost: localhost\\r\\nConnection: close\\r\\n\\r\\n' >&3; cat <&3 | grep -q '\"status\": \"UP\"'"]
    interval: 15s
    timeout: 10s
    retries: 20
    start_period: 60s
```

**Important:** `start_period: 60s` is required — Keycloak with MSSQL and realm imports takes 45–90 seconds on first boot. Health check failures during `start_period` do not count toward `retries`.

### `condition: service_healthy` Dependency Chain

[VERIFIED: Docker Compose specification]

Correct dependency ordering for this project:

```
mssql         → (no dependencies)
rabbitmq      → (no dependencies)
redis         → (no dependencies)
keycloak      → depends_on mssql: condition: service_healthy
db-migrator   → depends_on mssql: condition: service_healthy
tbe-gateway   → depends_on keycloak: condition: service_healthy
                           rabbitmq: condition: service_healthy
                           redis: condition: service_healthy
[all 9 services] → depends_on mssql: condition: service_healthy
                               rabbitmq: condition: service_healthy
                               redis: condition: service_healthy
                               db-migrator: condition: service_completed_successfully
```

`condition: service_completed_successfully` (not `service_healthy`) is used for the migrator — it exits with code 0 after running all migrations, then all application services start.

---

## Keycloak Realm Import Automation

### Import Mechanism

[VERIFIED: https://www.keycloak.org/server/importExport]

- **Command flag:** `start-dev --import-realm` (or `start --import-realm` for production mode)
- **Import directory:** `/opt/keycloak/data/import` inside the container
- **File pattern:** Only `.json` files at the root of the directory (subdirectories ignored)
- **File naming convention:** `<realm-name>-realm.json` — e.g., `tbe-b2c-realm.json`
- **Idempotency:** If the realm already exists, import is **skipped** — this means the realm JSON is only applied on first boot; subsequent restarts will not re-import

Volume mount to place realm files:
```yaml
volumes:
  - ./infra/keycloak/realms:/opt/keycloak/data/import
```

Create three files:
```
infra/keycloak/realms/
  tbe-b2c-realm.json
  tbe-b2b-realm.json
  tbe-backoffice-realm.json
```

### Minimal Realm JSON for OIDC Client

[CITED: https://www.keycloak.org/server/importExport]

Keycloak supports environment variable substitution in import files using `${VARIABLE_NAME}` syntax. Minimal working realm JSON for Phase 1 (OIDC client stub, no real users — users added in Phase 4/5):

```json
{
  "realm": "tbe-b2c",
  "enabled": true,
  "sslRequired": "none",
  "registrationAllowed": true,
  "loginWithEmailAllowed": true,
  "clients": [
    {
      "clientId": "tbe-web",
      "enabled": true,
      "publicClient": true,
      "standardFlowEnabled": true,
      "redirectUris": ["http://localhost:3000/*"],
      "webOrigins": ["http://localhost:3000"],
      "protocol": "openid-connect"
    },
    {
      "clientId": "tbe-gateway",
      "enabled": true,
      "publicClient": false,
      "serviceAccountsEnabled": true,
      "standardFlowEnabled": false,
      "secret": "${TBE_GATEWAY_CLIENT_SECRET}",
      "protocol": "openid-connect"
    }
  ],
  "roles": {
    "realm": [
      { "name": "customer", "description": "B2C customer role" }
    ]
  }
}
```

Equivalent structure for `tbe-b2b-realm.json` (roles: `agent-admin`, `agent`, `agent-readonly`) and `tbe-backoffice-realm.json` (roles: `backoffice-admin`, `backoffice-operator`, `finance`).

**Gotcha:** `"sslRequired": "none"` is required for dev mode on HTTP. Do not use in production.

---

## YARP + Keycloak JWT Validation

### How YARP Handles JWT

[VERIFIED: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/yarp/authn-authz]

YARP does not implement its own JWT validation. It delegates to ASP.NET Core's standard authentication pipeline via `AddAuthentication().AddJwtBearer()`. YARP then consumes named authorization policies per route via `AuthorizationPolicy` in `appsettings.json`.

Bearer tokens in incoming requests flow automatically to downstream services because bearer tokens are request headers — they pass through YARP's proxy unchanged. No special transform is needed for the `Authorization` header.

### NuGet Package

`Microsoft.AspNetCore.Authentication.JwtBearer` is **included in the .NET 8 SDK** — no separate NuGet install required for the JWT Bearer handler.

Required in `TBE.Gateway.csproj`:
```xml
<PackageReference Include="Yarp.ReverseProxy" Version="2.3.0" />
```
(JWT bearer package is implicit via the ASP.NET Core SDK reference)

### Program.cs Configuration

```csharp
// TBE.Gateway/Program.cs

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer("B2C", options =>
    {
        // Keycloak exposes OpenID Connect metadata at this URL
        // YARP fetches JWKS from this metadata on first request and caches it
        options.Authority = "http://keycloak:8080/realms/tbe-b2c";
        options.RequireHttpsMetadata = false; // dev only — remove in production
        options.Audience = "tbe-gateway";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            // Keycloak uses realm URL as issuer
            ValidIssuer = "http://keycloak:8080/realms/tbe-b2c"
        };
    })
    .AddJwtBearer("B2B", options =>
    {
        options.Authority = "http://keycloak:8080/realms/tbe-b2b";
        options.RequireHttpsMetadata = false;
        options.Audience = "tbe-gateway";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "http://keycloak:8080/realms/tbe-b2b"
        };
    })
    .AddJwtBearer("Backoffice", options =>
    {
        options.Authority = "http://keycloak:8080/realms/tbe-backoffice";
        options.RequireHttpsMetadata = false;
        options.Audience = "tbe-gateway";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "http://keycloak:8080/realms/tbe-backoffice"
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("B2CPolicy", policy =>
        policy.AddAuthenticationSchemes("B2C")
              .RequireAuthenticatedUser());
    options.AddPolicy("B2BPolicy", policy =>
        policy.AddAuthenticationSchemes("B2B")
              .RequireAuthenticatedUser());
    options.AddPolicy("BackofficePolicy", policy =>
        policy.AddAuthenticationSchemes("Backoffice")
              .RequireAuthenticatedUser());
});

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// ...

app.UseAuthentication();
app.UseAuthorization();
app.MapReverseProxy();
```

### appsettings.json Route Configuration

```json
{
  "ReverseProxy": {
    "Routes": {
      "b2c-search": {
        "ClusterId": "search-cluster",
        "AuthorizationPolicy": "B2CPolicy",
        "Match": { "Path": "/api/b2c/search/{**catch-all}" },
        "Transforms": [{ "PathPattern": "/api/search/{**catch-all}" }]
      },
      "b2c-booking": {
        "ClusterId": "booking-cluster",
        "AuthorizationPolicy": "B2CPolicy",
        "Match": { "Path": "/api/b2c/bookings/{**catch-all}" },
        "Transforms": [{ "PathPattern": "/api/bookings/{**catch-all}" }]
      },
      "b2b-booking": {
        "ClusterId": "booking-cluster",
        "AuthorizationPolicy": "B2BPolicy",
        "Match": { "Path": "/api/b2b/bookings/{**catch-all}" },
        "Transforms": [{ "PathPattern": "/api/bookings/{**catch-all}" }]
      },
      "backoffice": {
        "ClusterId": "backoffice-cluster",
        "AuthorizationPolicy": "BackofficePolicy",
        "Match": { "Path": "/api/backoffice/{**catch-all}" },
        "Transforms": [{ "PathPattern": "/api/{**catch-all}" }]
      }
    },
    "Clusters": {
      "search-cluster": {
        "Destinations": {
          "d1": { "Address": "http://search-service:8080/" }
        }
      },
      "booking-cluster": {
        "Destinations": {
          "d1": { "Address": "http://booking-service:8080/" }
        }
      },
      "backoffice-cluster": {
        "Destinations": {
          "d1": { "Address": "http://backoffice-service:8080/" }
        }
      }
    }
  }
}
```

**Authorization header forwarding:** Bearer tokens flow automatically — YARP forwards all request headers by default. No transform configuration is needed.

**Gotcha:** When using multiple JWT bearer schemes, ASP.NET Core requires the policy to explicitly name which scheme to use via `AddAuthenticationSchemes()`. Without this, the framework attempts all schemes and returns 401 if none succeed — this is correct behavior but requires the policy wiring shown above.

---

## MassTransit 8.x Outbox Setup

> **CRITICAL CORRECTION:** MassTransit current stable version is **9.1.0** (not 8.x). The STACK.md research assumed 8.3.x. All configuration below applies to MassTransit 9.x which is the version to use.

[VERIFIED: NuGet registry — MassTransit 9.1.0, MassTransit.RabbitMQ 9.1.0, MassTransit.EntityFrameworkCore 9.1.0 — all released March 30, 2026]

### Required NuGet Packages

```xml
<PackageReference Include="MassTransit" Version="9.1.0" />
<PackageReference Include="MassTransit.RabbitMQ" Version="9.1.0" />
<PackageReference Include="MassTransit.EntityFrameworkCore" Version="9.1.0" />
```

Only `MassTransit.EntityFrameworkCore` is needed for the outbox — it includes all outbox components. The base `MassTransit` package is included transitively.

### Program.cs Configuration

[VERIFIED: https://masstransit.massient.com/documentation/configuration/middleware/outbox]

```csharp
// Example: BookingService/Program.cs
builder.Services.AddMassTransit(x =>
{
    // Register consumers
    x.AddConsumer<BookingInitiatedConsumer>();

    // Configure the EF Core outbox BEFORE UsingRabbitMq
    x.AddEntityFrameworkOutbox<BookingDbContext>(o =>
    {
        o.UseSqlServer();
        o.UseBusOutbox();                    // enables transactional outbox for all published messages
        o.QueryDelay = TimeSpan.FromSeconds(5);          // how often outbox checks for unsent messages
        o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30); // dedupe window for inbox
    });

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"], "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"]);
            h.Password(builder.Configuration["RabbitMQ:Password"]);
        });

        // Apply outbox to ALL receive endpoints automatically
        cfg.AddConfigureEndpointsCallback((context, name, cfg) =>
        {
            cfg.UseEntityFrameworkOutbox<BookingDbContext>(context);
        });

        cfg.ConfigureEndpoints(context);
    });
});
```

### DbContext Configuration (OnModelCreating)

[VERIFIED: MassTransit official documentation]

```csharp
public class BookingDbContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Adds three tables: InboxState, OutboxMessage, OutboxState
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        // ... booking domain entities
    }
}
```

### Tables Created by Outbox

| Table | Purpose |
|-------|---------|
| `InboxState` | Tracks received messages by `MessageId` per endpoint — prevents duplicate processing |
| `OutboxMessage` | Stores messages published/sent within a transaction — dispatched asynchronously |
| `OutboxState` | Tracks delivery of outbox messages by the delivery service |

### Verifying Outbox Works

1. Set a breakpoint after a message is published inside a transaction
2. Check the `OutboxMessage` table — the row should exist before the consumer receives it
3. After the transaction commits, the MassTransit delivery service picks it up and dispatches to RabbitMQ
4. Simulate broker failure (stop RabbitMQ) — messages should accumulate in `OutboxMessage` and deliver when RabbitMQ restarts

### Exchange Topology for Phase 1

MassTransit 9 auto-creates exchanges and queues from consumer type names. For Phase 1, use these message contract names in `TBE.Contracts`:

```csharp
// TBE.Contracts/Events/
public record BookingInitiated(Guid BookingId, string ProductType);
public record BookingConfirmed(Guid BookingId, string SupplierRef);
public record BookingFailed(Guid BookingId, string Reason);
public record PaymentProcessed(Guid BookingId, string PaymentIntentId);
```

MassTransit creates exchanges named from the full type name. For Phase 1 (stub consumers only), this is sufficient — the topology can be refined in Phase 3.

---

## EF Core Multi-Service Migration Strategy

### The Race Condition Problem

[VERIFIED: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying]

If 9 service containers all call `dbContext.Database.Migrate()` at startup against the same MSSQL instance:
- All 9 acquire an EF Core migration lock on the `__EFMigrationsLock` table
- Each service uses a different DbContext with its own schema — they do not conflict **if schemas are separate**
- The real risk is startup ordering: services may start before MSSQL is accepting connections even with health checks, because the `service_healthy` condition only guarantees the MSSQL process is responding, not that application connections will succeed immediately

**Official guidance:** Microsoft docs explicitly state that `Migrate()` in `Program.cs` is appropriate for development but inappropriate for production databases. For this project (dev phase), a dedicated migrator service is the cleanest solution.

### Recommended Strategy: Dedicated db-migrator Service

[CITED: https://blog.poespas.me/posts/2025/02/14/aspnetcore-entityframework-migrations-docker-compose/]

Create a single `db-migrator` Docker Compose service that:
1. Depends on `mssql: condition: service_healthy`
2. Runs all 9 migrations sequentially using a custom console app
3. Exits with code 0 on success
4. All 9 application services use `db-migrator: condition: service_completed_successfully`

```yaml
# docker-compose.yml
db-migrator:
  build:
    context: .
    dockerfile: src/tools/TBE.DbMigrator/Dockerfile
  environment:
    - ConnectionStrings__BookingDb=${BOOKING_DB_CONNECTION}
    - ConnectionStrings__PaymentDb=${PAYMENT_DB_CONNECTION}
    # ... all 9 connection strings
  depends_on:
    mssql:
      condition: service_healthy
  restart: "no"  # exits after migrations complete
```

`TBE.DbMigrator` is a simple console app that instantiates each service's `DbContext` and calls `Migrate()`:

```csharp
// TBE.DbMigrator/Program.cs
using var bookingCtx = new BookingDbContext(/* connection string */);
await bookingCtx.Database.MigrateAsync();

using var paymentCtx = new PaymentDbContext(/* connection string */);
await paymentCtx.Database.MigrateAsync();
// ... etc for all 9 services
```

### Each Service Runs Migrations Independently

```bash
# Per service — run from each service's Infrastructure project
dotnet ef migrations add InitialSchema \
  --project src/services/BookingService/BookingService.Infrastructure \
  --startup-project src/services/BookingService/BookingService.API
```

Each service's migrations live in its own `Migrations/` folder inside the `Infrastructure` project. Schemas are separate (`dbo.Bookings` vs `dbo.Payments` — or better, use per-service database names for clean separation).

### Recommended: Separate Databases per Service

Use a separate MSSQL database per service (not separate SQL Server instances) — one MSSQL container, multiple databases:

```
BookingDb
PaymentDb
SearchDb        (empty — SearchService uses Redis only)
PricingDb
NotificationDb
CrmDb
BackofficeDb
FlightConnectorDb  (empty — stateless service)
HotelConnectorDb   (empty — stateless service)
```

This enforces bounded context isolation without the overhead of separate MSSQL containers.

---

## Serilog Configuration for Docker

### Package Versions (Verified)

[VERIFIED: NuGet registry April 2026]

```xml
<PackageReference Include="Serilog.AspNetCore" Version="10.0.0" />
<PackageReference Include="Serilog.Sinks.Console" Version="6.1.1" />
<PackageReference Include="Serilog.Formatting.Compact" Version="3.0.0" />
<PackageReference Include="Serilog.Settings.Configuration" Version="10.0.0" />
```

`Serilog.AspNetCore` 10.0.0 targets .NET 8.0+ and includes `Serilog.Settings.Configuration` as a dependency — you may not need to reference it separately.

### Program.cs Setup

```csharp
// At the very top of Program.cs — before builder is created
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

builder.Host.UseSerilog((context, services, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration)
                 .ReadFrom.Services(services)
                 .Enrich.FromLogContext()
                 .Enrich.WithProperty("Service", "BookingService"));
```

### appsettings.json Configuration (JSON Console for Docker)

[CITED: https://github.com/serilog/serilog-aspnetcore]

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.Hosting.Lifetime": "Information",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "formatter": "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact"
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId"]
  }
}
```

`CompactJsonFormatter` produces single-line JSON per log entry — ideal for Docker log drivers and log aggregation. Each line is valid JSON containing `@t` (timestamp), `@mt` (message template), `@l` (level), and enriched properties.

### Why Not Seq for Phase 1?

Seq requires an additional container and web UI. For Phase 1, structured console JSON is sufficient — Docker's own `docker logs <container>` and `docker-compose logs -f` work with JSON output. Seq is a good addition in Phase 7 (hardening). [ASSUMED — based on phase scope and complexity trade-off]

---

## ASP.NET Core Health Checks

### NuGet Packages (Verified)

[VERIFIED: NuGet registry April 2026]

```xml
<!-- In each service's .API project -->
<PackageReference Include="AspNetCore.HealthChecks.SqlServer" Version="9.0.0" />
<PackageReference Include="AspNetCore.HealthChecks.Rabbitmq" Version="9.0.0" />
<PackageReference Include="AspNetCore.HealthChecks.Redis" Version="9.0.0" />
```

`Microsoft.Extensions.Diagnostics.HealthChecks` is included in the ASP.NET Core SDK — no separate package needed.

### Program.cs Configuration

```csharp
builder.Services.AddHealthChecks()
    .AddSqlServer(
        builder.Configuration.GetConnectionString("BookingDb")!,
        name: "booking-db",
        tags: new[] { "db", "sql", "booking" })
    .AddRabbitMQ(
        rabbitConnectionFactory: sp =>
        {
            var factory = new ConnectionFactory
            {
                HostName = builder.Configuration["RabbitMQ:Host"],
                UserName = builder.Configuration["RabbitMQ:Username"],
                Password = builder.Configuration["RabbitMQ:Password"]
            };
            return factory.CreateConnectionAsync().GetAwaiter().GetResult();
        },
        name: "rabbitmq",
        tags: new[] { "messaging" })
    .AddRedis(
        builder.Configuration["Redis:ConnectionString"]!,
        name: "redis",
        tags: new[] { "cache" });

// Map the endpoint
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
```

**Note:** RabbitMQ connections are long-lived and expensive. The health check should reuse a singleton `IConnection` registered in DI, not create a new connection per check.

### Standard `/health` Endpoint Response

```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0123456",
  "entries": {
    "booking-db": { "status": "Healthy", "duration": "00:00:00.0045000" },
    "rabbitmq":   { "status": "Healthy", "duration": "00:00:00.0034000" },
    "redis":      { "status": "Healthy", "duration": "00:00:00.0012000" }
  }
}
```

The `UIResponseWriter.WriteHealthCheckUIResponse` from `AspNetCore.HealthChecks.UI.Client` produces the detailed JSON above. Without it, the default response is just `"Healthy"` as plain text.

**Health check UI** (`/health-ui`) requires the separate `AspNetCore.HealthChecks.UI` package — defer this to Phase 7 (hardening). For Phase 1, the `/health` JSON endpoint is sufficient.

---

## Solution Scaffolding Commands

### Complete Scaffolding Script

The following command sequence creates the entire solution structure per the locked decisions (D-01 through D-05).

[ASSUMED — `dotnet new` command syntax verified against .NET 8 SDK knowledge; commands are standard and stable]

```bash
# Create solution
mkdir -p /path/to/TBE && cd /path/to/TBE
dotnet new sln -n TBE

# Create directory structure
mkdir -p src/services/BookingService
mkdir -p src/services/PaymentService
mkdir -p src/services/SearchService
mkdir -p src/services/FlightConnectorService
mkdir -p src/services/HotelConnectorService
mkdir -p src/services/PricingService
mkdir -p src/services/NotificationService
mkdir -p src/services/CrmService
mkdir -p src/services/BackofficeService
mkdir -p src/gateway/TBE.Gateway
mkdir -p src/shared
mkdir -p src/tools/TBE.DbMigrator
mkdir -p infra/keycloak/realms

# Shared libraries
dotnet new classlib -n TBE.Contracts -o src/shared/TBE.Contracts
dotnet new classlib -n TBE.Common   -o src/shared/TBE.Common

# Gateway (single project)
dotnet new webapi -n TBE.Gateway -o src/gateway/TBE.Gateway

# DbMigrator tool
dotnet new console -n TBE.DbMigrator -o src/tools/TBE.DbMigrator

# Per service — repeat for all 9 services:
# (shown for BookingService; repeat pattern for all others)
dotnet new webapi     -n BookingService.API            -o src/services/BookingService/BookingService.API
dotnet new classlib   -n BookingService.Application    -o src/services/BookingService/BookingService.Application
dotnet new classlib   -n BookingService.Infrastructure -o src/services/BookingService/BookingService.Infrastructure

# Add all projects to solution
dotnet sln add src/shared/TBE.Contracts/TBE.Contracts.csproj
dotnet sln add src/shared/TBE.Common/TBE.Common.csproj
dotnet sln add src/gateway/TBE.Gateway/TBE.Gateway.csproj
dotnet sln add src/tools/TBE.DbMigrator/TBE.DbMigrator.csproj
# Repeat dotnet sln add for all 9 × 3 = 27 service projects
```

### Typical .csproj Content per Layer Type

**API project** (hosts + DI wiring):
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>TBE.BookingService.API</RootNamespace>
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
  </ItemGroup>
</Project>
```

**Application project** (use cases, no infrastructure references):
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

**Infrastructure project** (EF Core, external clients):
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
    <PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="8.0.25" />
    <PackageReference Include="MassTransit.EntityFrameworkCore" Version="9.1.0" />
    <PackageReference Include="StackExchange.Redis" Version="2.12.14" />
  </ItemGroup>
</Project>
```

**Services that are stateless (FlightConnectorService, HotelConnectorService)** have no Infrastructure project — they have only API + Application. Remove the `Infrastructure` project for stateless services.

**NotificationService** has no HTTP API — use `Microsoft.NET.Sdk.Worker` as SDK for the API project (it is a background worker service).

---

## Security Threat Model

### Threats Specific to Phase 1 Infrastructure

#### T-01: JWT Validation Bypass via JWKS Endpoint Failure

**Threat:** If the Keycloak container is unreachable at gateway startup, YARP cannot fetch the JWKS public keys. ASP.NET Core JwtBearer caches keys after first successful fetch. If keys are never fetched (cold start failure), all JWTs fail validation with a 500 or 401.

**Mitigation:**
- `condition: service_healthy` on Keycloak ensures it is ready before the gateway starts
- `JwtBearerOptions.BackchannelHttpHandler` — configure a retry policy (Polly) on the HTTP client that fetches JWKS
- Set `AutomaticRefreshInterval` and `RefreshInterval` on `TokenValidationParameters` to handle key rotation

**Risk level:** MEDIUM in dev (containers restart); HIGH in production if Keycloak goes offline

#### T-02: MSSQL SA Account Exposure

**Threat:** Using the SA account for all application connections gives every service unrestricted database access. If a BookingService is compromised, the attacker has full access to PaymentDb.

**Mitigation for Phase 1 (dev):** Use SA only for local dev. Document clearly in `.env.example`.

**Mitigation for Phase 3+ (production path):** Create per-service SQL logins with access only to their own database:
```sql
CREATE LOGIN booking_svc WITH PASSWORD = '...';
CREATE USER booking_svc FOR LOGIN booking_svc;
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO booking_svc;
```

[CITED: OWASP ASVS V4 — Principle of Least Privilege]

#### T-03: RabbitMQ Management UI Exposed on Public Port

**Threat:** Port `15672` (RabbitMQ management UI) exposed on the host allows browser access to the management console. Default credentials or weak passwords make this an easy attack vector.

**Mitigation:**
- In `docker-compose.override.yml.example`, bind management port to localhost only: `"127.0.0.1:15672:15672"` — not `"15672:15672"`
- Change default credentials via `.env` (never use guest/guest)
- In production, remove the management port exposure entirely

#### T-04: Keycloak Admin Console Exposure

**Threat:** Keycloak admin console at `http://host:8080/admin` is exposed externally in dev config. If `start-dev` mode is used in production accidentally, the admin console has no HTTPS and weak TLS.

**Mitigation:**
- Bind Keycloak port to localhost in override: `"127.0.0.1:8080:8080"`
- Document that `start-dev` must never be used in production (use `start` with `KC_HOSTNAME` and proper TLS)
- Admin credentials in `.env` — ensure placeholder makes it obvious these must be changed

#### T-05: Inter-Service Communication Without TLS in Docker Network

**Threat:** Service-to-service HTTP calls inside the Docker bridge network are unencrypted. If the Docker host is compromised, traffic can be sniffed.

**Mitigation for Phase 1:** Internal Docker bridge network traffic is acceptable for dev — Docker's network isolation is sufficient. This is a known and accepted trade-off for development.

**Mitigation for production (Phase 7):** Either enforce mTLS between services (service mesh) or ensure the host machine has proper network isolation. For single-VM deployment, Docker network isolation is acceptable.

[ASSUMED — acceptable risk level for Phase 1 dev environment; production hardening deferred to Phase 7]

#### T-06: GDS Credentials in Environment Variables (COMP-05)

**Threat:** `.env` file checked in by accident (forgetting `.gitignore`).

**Mitigation:**
- Ensure `.env` is in `.gitignore` before the first commit
- Add a `git-secrets` or `pre-commit` hook to detect credential patterns [ASSUMED — standard practice, specific tool at Claude's discretion]
- `.env.example` uses obviously placeholder values (`Your_Password123!`, `your-api-key-here`) per D-10

### ASVS Categories Applicable to Phase 1

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | YES | Keycloak / JwtBearer — no custom auth code |
| V3 Session Management | YES | Keycloak tokens; Redis session for B2C (Phase 4) |
| V4 Access Control | YES | YARP per-route authorization policies; `[Authorize]` on controllers |
| V5 Input Validation | PARTIAL | Phase 1 is stub controllers — validation in Phase 3 |
| V6 Cryptography | NO | No custom crypto in Phase 1 (passport encryption is Phase 3) |
| V9 Communications | YES | RabbitMQ and inter-service HTTP inside Docker bridge |

---

## NuGet Package Versions (Confirmed)

All versions verified against NuGet registry as of 2026-04-12.

[VERIFIED: NuGet registry]

| Package | Verified Version | Notes |
|---------|-----------------|-------|
| `Yarp.ReverseProxy` | **2.3.0** | Current stable |
| `MassTransit` | **9.1.0** | Was 8.3.x in STACK.md — now 9.x; breaking changes from 8.x exist |
| `MassTransit.RabbitMQ` | **9.1.0** | Same major version as core |
| `MassTransit.EntityFrameworkCore` | **9.1.0** | Same major version as core |
| `Microsoft.EntityFrameworkCore.SqlServer` | **8.0.25** | Use 8.x line for .NET 8 project compatibility |
| `Microsoft.EntityFrameworkCore.Tools` | **8.0.25** | Install in Infrastructure projects only |
| `StackExchange.Redis` | **2.12.14** | 3.0.x prerelease available but not stable |
| `Serilog.AspNetCore` | **10.0.0** | Major version increase from 8.x |
| `Serilog.Sinks.Console` | **6.1.1** | |
| `Serilog.Formatting.Compact` | **3.0.0** | Required for `CompactJsonFormatter` |
| `Serilog.Settings.Configuration` | **10.0.0** | Included with `Serilog.AspNetCore` transitively |
| `AspNetCore.HealthChecks.SqlServer` | **9.0.0** | |
| `AspNetCore.HealthChecks.Rabbitmq` | **9.0.0** | |
| `AspNetCore.HealthChecks.Redis` | **9.0.0** | |

**Breaking change alert — MassTransit 8 → 9:** MassTransit 9 contains breaking changes from 8.x. If any tutorials or community examples reference 8.x APIs, verify against 9.x docs. The outbox configuration API is largely the same; the primary breaking changes are in saga and consumer endpoint configuration. [ASSUMED — based on MassTransit versioning conventions; verify migration guide if upgrading an existing codebase; this is greenfield so no migration needed]

---

## Key Implementation Decisions

### Decision: MassTransit 9.1.0 not 8.x

The STACK.md recommended MassTransit 8.3.x with a [VERIFY] flag. The actual current stable is 9.1.0. Use 9.1.0 — it is the stable release with active support as of April 2026.

### Decision: Use `db-migrator` Compose Service, Not Startup Migrate()

Running `Migrate()` in 9 application services simultaneously creates a startup dependency tangle and potential contention on the `__EFMigrationsLock` table (even with separate databases, the migrator logic is cleaner as a separate concern). The `db-migrator` exits with code 0 on success, which `condition: service_completed_successfully` depends on.

### Decision: Keycloak 26.6.0 with MSSQL Backend (Not H2/PostgreSQL)

The STACK.md example showed `KC_DB: mssql`. Keycloak 26.x supports MSSQL as a DB backend. Since MSSQL is already in the stack, this avoids adding PostgreSQL. The MSSQL JDBC connection string format requires `encrypt=false` for the dev environment (no TLS on the local MSSQL instance).

### Decision: Three Separate JWT Bearer Schemes in YARP

Rather than one unified JWT scheme with custom claims routing, three named schemes (`B2C`, `B2B`, `Backoffice`) each pointing to their own Keycloak realm. This is simple, explicit, and means a B2C token can never authenticate a B2B route.

### Decision: Separate MSSQL Databases per Service (Not Schemas)

Separate databases (`BookingDb`, `PaymentDb`, etc.) enforce bounded context isolation more cleanly than schemas. EF Core migrations run independently against each database. Connection strings in `.env` point to the correct database per service.

### Decision: EF Core 8.x (not 9.x) for .NET 8 Target

EF Core 8.0.25 is the current stable release for the .NET 8 target framework. EF Core 9.x targets .NET 9. Since the project targets .NET 8, use EF Core 8.x.

---

## Risks & Gotchas

### Gotcha 1: mssql-tools18 Path Change

The MSSQL 2022 healthcheck must use `/opt/mssql-tools18/bin/sqlcmd`, not `/opt/mssql-tools/bin/sqlcmd`. Older examples and the STACK.md have the old path. Using the old path results in a healthcheck that always returns "unhealthy" — all services with `condition: service_healthy` on MSSQL will never start.

### Gotcha 2: Keycloak `sslRequired: "none"` Required for Dev

Without `"sslRequired": "none"` in the realm JSON, Keycloak 26 refuses HTTP connections and the local dev setup fails. This setting must never reach production.

### Gotcha 3: Keycloak Realm Import is Skipped if Realm Exists

If a realm was already imported in a previous run, `--import-realm` skips re-importing. To force re-import, the realm must be deleted via the admin console or the Keycloak data volume must be deleted (`docker-compose down -v`).

### Gotcha 4: MassTransit 9 — Outbox Must Be Registered Before UsingRabbitMq

`x.AddEntityFrameworkOutbox<T>()` must be called before the transport configuration (`x.UsingRabbitMq(...)`) — otherwise the outbox is not wired to the bus. The order in `AddMassTransit` matters.

### Gotcha 5: Multiple JWT Bearer Schemes Require Explicit Policy-to-Scheme Binding

ASP.NET Core 8 with multiple JWT schemes will not auto-select the correct scheme per route. Each authorization policy must call `.AddAuthenticationSchemes("SchemeName")` explicitly. Without this, the framework tries all schemes, logs confusing errors, and may return 401 even with a valid token.

### Gotcha 6: Keycloak `start-dev` Must Never Be Used in Production

`start-dev` disables TLS, enables the admin console without auth hardening, and uses an in-memory cache. It is only for local development. The compose file comment must make this explicit.

### Gotcha 7: Stateless Services Do Not Need an Infrastructure Project

`FlightConnectorService` and `HotelConnectorService` are stateless adapters (Phase 1 stubs). They do not have a database or outbox. Creating an Infrastructure project for them now creates empty projects that will confuse Phase 2 implementers. Create only API + Application for stateless services.

### Gotcha 8: NotificationService is a Worker, Not a Web API

`NotificationService` consumes RabbitMQ events only — it has no HTTP API. Use `Microsoft.NET.Sdk.Worker` as the project SDK, not `Microsoft.NET.Sdk.Web`. This means no Kestrel, no routing, just `IHostedService` + MassTransit consumers.

### Gotcha 9: EF Core 9.x vs 8.x — Match SDK Version

The project targets .NET 8. EF Core 9.x requires .NET 9. Install `Microsoft.EntityFrameworkCore.SqlServer` version `8.0.25`, not the latest `9.x`. Mixing these produces build errors.

### Gotcha 10: Redis Health Check Requires Password in Connection String

If Redis is started with `--requirepass`, the health check connection string must include the password: `"localhost:6379,password=yourpassword"`. Using just `"localhost:6379"` causes the health check to fail with `NOAUTH`.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| Docker Desktop | All containers | YES | 28.0.1 | — |
| Docker Compose | All containers | YES | v2.33.1 | — |
| .NET SDK | Project scaffolding, build | YES | 10.0 preview | Use `<TargetFramework>net8.0</TargetFramework>` — SDK version 10 can build net8 targets |
| MSSQL (Docker) | 9 service databases, Keycloak | Via Docker | Latest Linux image | — |
| RabbitMQ (Docker) | MassTransit | Via Docker | 3.13-management | — |
| Redis (Docker) | Caching, sessions | Via Docker | 7.2-alpine | — |
| Keycloak (Docker) | JWT auth | Via Docker | 26.6.0 | — |

**Note on .NET SDK version:** The machine has .NET SDK 10 preview installed. This can build and run `net8.0` target framework projects without issue — `TargetFramework` controls the runtime target, not the SDK version. Specifying `<TargetFramework>net8.0</TargetFramework>` in all .csproj files is sufficient.

---

## Validation Architecture

### How Each INFRA Requirement is Verified

#### INFRA-01: `docker-compose up` from clean checkout starts all services

**Manual UAT (primary):**
1. `git clone <repo> && cd TBE`
2. `cp .env.example .env && fill credentials`
3. `docker-compose up --build`
4. Wait for all containers to show `(healthy)` in `docker-compose ps`
5. All 11 containers (9 services + gateway + 4 infrastructure + migrator) must reach healthy/running state

**Automated check:** `docker-compose ps --format json | jq '.[] | select(.Health != "healthy" and .State != "running") | .Name'` — should return empty

**Time limit:** All containers healthy within 3 minutes (per UAT criteria in ROADMAP.md)

#### INFRA-02: YARP gateway routes with JWT validation

**Automated test:**
```bash
# Get token from Keycloak
TOKEN=$(curl -s -X POST "http://localhost:8080/realms/tbe-b2c/protocol/openid-connect/token" \
  -d "grant_type=client_credentials&client_id=tbe-gateway&client_secret=..." \
  | jq -r .access_token)

# Valid token → 200
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/b2c/health
# Expected: 200

# No token → 401
curl http://localhost:5000/api/b2c/health
# Expected: 401
```

#### INFRA-03: Keycloak shows three realms

**Manual UAT:** Admin console at `http://localhost:8080/admin` shows `tbe-b2c`, `tbe-b2b`, `tbe-backoffice` realms with their configured clients.

**Automated check:**
```bash
ADMIN_TOKEN=$(curl -s -X POST "http://localhost:8080/realms/master/protocol/openid-connect/token" \
  -d "grant_type=password&client_id=admin-cli&username=admin&password=..." | jq -r .access_token)
curl -H "Authorization: Bearer $ADMIN_TOKEN" http://localhost:8080/admin/realms | jq '.[].realm'
# Expected: "tbe-b2c", "tbe-b2b", "tbe-backoffice"
```

#### INFRA-04: MassTransit outbox works end-to-end

**Integration test approach (create in Wave 0 as a test-only consumer):**
1. Publish a `TestMessageSent` event from a test controller endpoint
2. Verify the `OutboxMessage` table row exists (check DB directly)
3. Verify the consumer receives and acknowledges the message (check RabbitMQ management UI at `http://localhost:15672`)

#### INFRA-05: Redis available to services

**Automated check per service:**
```bash
docker exec tbe-redis redis-cli -a "$REDIS_PASSWORD" ping
# Expected: PONG
```

Service-level: Each service health check endpoint reports `"redis": { "status": "Healthy" }`.

#### INFRA-06: `/health` returns healthy on all services

**Automated sweep:**
```bash
for port in 5001 5002 5003 5004 5005 5006 5007 5008 5009; do
  STATUS=$(curl -s http://localhost:$port/health | jq -r .status)
  echo "Port $port: $STATUS"
done
# All should output: Healthy
```

#### INFRA-07: MSSQL schemas exist after startup

**Automated SQL check:**
```bash
docker exec tbe-mssql /opt/mssql-tools18/bin/sqlcmd -S localhost -C -U sa -P "$MSSQL_SA_PASSWORD" \
  -Q "SELECT TABLE_CATALOG, TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE' ORDER BY TABLE_CATALOG" \
  -b -o /dev/null
```

The `db-migrator` container's exit code (0 = success) is the primary signal. If `docker-compose ps db-migrator` shows `Exited (0)`, migrations ran successfully.

### Wave 0 Test Infrastructure Gaps

The following test infrastructure does not exist yet and must be created in Wave 0:

- [ ] Integration test project `tests/TBE.IntegrationTests/` — covers INFRA-02, INFRA-03, INFRA-04
- [ ] Docker Compose test configuration (`docker-compose.test.yml`) that uses the same services but test-specific databases
- [ ] Shell script `scripts/validate-infra.sh` — automates all 7 INFRA checks

**Phase 1 does not require a formal test framework for validation** — the INFRA requirements are infrastructure-level and validated via `curl`, `docker exec`, and manual UAT. xUnit integration tests become important in Phase 3 (saga tests).

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Seq is not needed for Phase 1 — console JSON is sufficient | Serilog section | Low: Seq can be added later without code changes (just a new sink) |
| A2 | `start-dev` mode is acceptable risk for dev environment | Security section | Low: contained in local dev only |
| A3 | MassTransit 9 outbox API is backward-compatible with the 8.x examples in STACK.md | MassTransit section | Medium: some configuration methods may differ; verify against 9.x docs |
| A4 | Stateless services (FlightConnector, HotelConnector) need only API + Application projects | Scaffolding section | Low: Infrastructure project can always be added in Phase 2 |
| A5 | EF Core 8.0.25 is compatible with all MSSQL schema requirements for Phase 1 stub tables | EF Core section | Low: Phase 1 tables are simple; EF Core 8 covers all needed features |

---

## Open Questions

1. **MassTransit 9 breaking changes**
   - What we know: MassTransit 9.1.0 is current stable; the outbox API is documented and verified
   - What's unclear: Whether any Phase 3 saga patterns changed significantly from 8.x to 9.x
   - Recommendation: Proceed with 9.1.0 for Phase 1; review MassTransit 9 migration guide before Phase 3

2. **Keycloak MSSQL JDBC URL format**
   - What we know: `jdbc:sqlserver://mssql:1433;databaseName=keycloak;encrypt=false` is the documented format
   - What's unclear: Whether Keycloak 26.6.0 requires additional JDBC flags for the MSSQL 2022 CU images
   - Recommendation: Test on first `docker-compose up`; if Keycloak fails to start, check logs for JDBC connection errors

3. **Keycloak admin account environment variable name**
   - What we know: Keycloak 26+ uses `KC_BOOTSTRAP_ADMIN_USERNAME` / `KC_BOOTSTRAP_ADMIN_PASSWORD` (verified from getting-started docs)
   - What's unclear: Older versions used `KEYCLOAK_ADMIN` / `KEYCLOAK_ADMIN_PASSWORD`; 26.6.0 may support both
   - Recommendation: Use the new `KC_BOOTSTRAP_ADMIN_*` variables; test on first boot

---

## Sources

### Primary (HIGH confidence)
- [VERIFIED: NuGet registry] — All 14 package versions verified via `nuget.org/packages/{name}` on 2026-04-12
- [VERIFIED: https://www.keycloak.org/server/importExport] — Realm import path, `--import-realm` flag, file naming convention
- [VERIFIED: https://www.keycloak.org/getting-started/getting-started-docker] — Keycloak 26.6.0 image tag, `KC_BOOTSTRAP_ADMIN_*` env vars
- [VERIFIED: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/yarp/authn-authz] — YARP authorization policy per-route, `AuthorizationPolicy` config, bearer token forwarding behavior
- [VERIFIED: https://masstransit.massient.com/documentation/configuration/middleware/outbox] — MassTransit 9 AddEntityFrameworkOutbox, outbox table names, OnModelCreating configuration
- [VERIFIED: GitHub gist belgattitude/9979e5501d72ffa90c9460597dee8dca] — MSSQL 2022 healthcheck `/opt/mssql-tools18/` path

### Secondary (MEDIUM confidence)
- [CITED: https://www.keycloak.org/observability/health] — Keycloak health endpoint `/health/ready` on port 9000, `KC_HEALTH_ENABLED`
- [CITED: https://blog.poespas.me/posts/2025/02/14/aspnetcore-entityframework-migrations-docker-compose/] — Dedicated db-migrator service pattern for EF Core migrations
- [CITED: https://github.com/serilog/serilog-aspnetcore] — Serilog Program.cs bootstrap logger pattern

### Tertiary (LOW confidence — assumptions)
- A1: Seq deferral rationale — [ASSUMED]
- A3: MassTransit 9 compatibility with Phase 3 saga patterns — [ASSUMED]

---

## RESEARCH COMPLETE

**Phase:** 1 — Infrastructure Foundation
**Confidence:** HIGH

### Key Findings

1. **MassTransit version correction:** STACK.md assumed 8.3.x. Current stable is **9.1.0** (released March 2026). All Program.cs examples above use 9.x API. This is a greenfield project so no migration needed — use 9.1.0 from the start.

2. **MSSQL healthcheck path:** Must use `/opt/mssql-tools18/bin/sqlcmd` (not `/opt/mssql-tools/`). The old path causes all dependent services to permanently fail startup.

3. **Keycloak realm import is idempotent/skip-if-exists:** Realm JSON is only applied on first boot. Volume deletion (`docker-compose down -v`) is required to force re-import. This is expected behavior, not a bug.

4. **EF Core migration race condition resolved via db-migrator pattern:** A dedicated Docker Compose service that exits on completion, with application services using `condition: service_completed_successfully`, is the correct pattern. Running `Migrate()` in 9 simultaneous services is fragile.

5. **YARP authorization requires explicit scheme-to-policy binding with multiple JWT schemes:** Without `.AddAuthenticationSchemes("SchemeName")` in each policy, multi-scheme JWT fails silently with 401.

### File Created
`C:\Users\zhdishaq\source\repos\TBE\.planning\phases\01-infrastructure-foundation\01-RESEARCH.md`

### Confidence Assessment
| Area | Level | Reason |
|------|-------|--------|
| Package versions | HIGH | Verified against NuGet registry April 2026 |
| Docker healthchecks | HIGH | Verified via official Microsoft + community sources |
| Keycloak realm import | HIGH | Verified against official Keycloak docs |
| YARP JWT config | HIGH | Verified against official Microsoft Learn docs |
| MassTransit outbox | HIGH | Verified against official MassTransit docs |
| EF Core migration strategy | HIGH | Verified against official Microsoft EF Core docs |
| Security threat model | MEDIUM | Based on OWASP ASVS + training knowledge; no pen test performed |

### Ready for Planning

Research complete. Planner can now create PLAN.md files for all 4 workstreams:
1. Docker Compose stack (INFRA-01)
2. YARP gateway + Keycloak auth (INFRA-02, INFRA-03)
3. RabbitMQ / MassTransit wiring (INFRA-04, INFRA-05)
4. MSSQL migrations + shared services (INFRA-06, INFRA-07)
