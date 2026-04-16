---
phase: 01-infrastructure-foundation
plan: 02
type: execute
wave: 1
depends_on: []
files_modified:
  - src/gateway/TBE.Gateway/TBE.Gateway.csproj
  - src/gateway/TBE.Gateway/Program.cs
  - src/gateway/TBE.Gateway/appsettings.json
  - src/gateway/TBE.Gateway/appsettings.Development.json
  - src/gateway/TBE.Gateway/Dockerfile
  - TBE.sln
autonomous: true
requirements:
  - INFRA-02
  - INFRA-03

must_haves:
  truths:
    - "A request to the gateway with a valid tbe-b2c JWT is routed to the correct downstream service and returns 200"
    - "A request to any protected gateway route without a token returns 401"
    - "A B2C JWT cannot authenticate a B2B route (scheme isolation works)"
    - "YARP routes cover B2C, B2B, and backoffice traffic patterns"
    - "Gateway project compiles: `dotnet build src/gateway/TBE.Gateway` succeeds"
  artifacts:
    - path: "src/gateway/TBE.Gateway/TBE.Gateway.csproj"
      provides: "Gateway project with Yarp.ReverseProxy 2.3.0"
      contains: "Yarp.ReverseProxy"
    - path: "src/gateway/TBE.Gateway/Program.cs"
      provides: "Three JWT bearer schemes (B2C, B2B, Backoffice) + three authorization policies"
      contains: "AddJwtBearer"
    - path: "src/gateway/TBE.Gateway/appsettings.json"
      provides: "YARP route + cluster config for all 9 downstream services"
      contains: "ReverseProxy"
  key_links:
    - from: "Program.cs AddAuthorization"
      to: "appsettings.json AuthorizationPolicy per route"
      via: "Policy name string must match exactly: B2CPolicy, B2BPolicy, BackofficePolicy"
      pattern: "AddAuthenticationSchemes"
    - from: "appsettings.json Clusters"
      to: "docker-compose.yml service names"
      via: "Address uses Docker Compose service DNS names (e.g., http://booking-service:8080/)"
      pattern: "booking-service:8080"
---

<objective>
Scaffold the YARP API Gateway project (`TBE.Gateway`) with three JWT bearer schemes backed by Keycloak JWKS, three authorization policies mapped to three Keycloak realms, and YARP routes + clusters covering all 9 downstream microservices. A Dockerfile is created for Docker Compose to build.

Purpose: Provides JWT-authenticated routing — the core of INFRA-02. Without this, no client can reach microservices in an authenticated manner.

Output: `TBE.Gateway` project with `Program.cs`, `appsettings.json`, `Dockerfile`, added to `TBE.sln`.
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
<!-- Key patterns the executor must implement exactly. From RESEARCH.md. -->

YARP multi-scheme JWT — CRITICAL GOTCHA:
Each AddAuthorization policy MUST call .AddAuthenticationSchemes("SchemeName") explicitly.
Without this, ASP.NET Core tries all schemes and returns 401 inconsistently.

```csharp
// Program.cs pattern — exact API
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer("B2C", options => { ... })
    .AddJwtBearer("B2B", options => { ... })
    .AddJwtBearer("Backoffice", options => { ... });

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
```

Keycloak authority URLs (internal Docker DNS):
- B2C:       http://keycloak:8080/realms/tbe-b2c
- B2B:       http://keycloak:8080/realms/tbe-b2b
- Backoffice: http://keycloak:8080/realms/tbe-backoffice

NuGet: Yarp.ReverseProxy 2.3.0
JWT Bearer is part of ASP.NET Core SDK — no extra NuGet needed.
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Create TBE.Gateway project — .csproj, Program.cs, Dockerfile, add to TBE.sln</name>
  <read_first>
    - .planning/phases/01-infrastructure-foundation/01-CONTEXT.md — D-04 (gateway is single project at src/gateway/TBE.Gateway/); D-09–D-12 (secrets via IConfiguration)
    - .planning/phases/01-infrastructure-foundation/01-RESEARCH.md — "YARP + Keycloak JWT Validation" section; NuGet versions table
  </read_first>
  <files>src/gateway/TBE.Gateway/TBE.Gateway.csproj, src/gateway/TBE.Gateway/Dockerfile, TBE.sln</files>
  <action>
**Step 1: Create the project directory and .csproj**

Run from repository root:
```bash
mkdir -p src/gateway/TBE.Gateway
dotnet new webapi -n TBE.Gateway -o src/gateway/TBE.Gateway --no-openapi
```

Then replace the generated `.csproj` content with:
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>TBE.Gateway</RootNamespace>
    <AssemblyName>TBE.Gateway</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Yarp.ReverseProxy" Version="2.3.0" />
    <PackageReference Include="Serilog.AspNetCore" Version="10.0.0" />
    <PackageReference Include="Serilog.Formatting.Compact" Version="3.0.0" />
  </ItemGroup>
</Project>
```

**Step 2: Add to TBE.sln**

If `TBE.sln` does not exist yet at the repository root, create it first:
```bash
dotnet new sln -n TBE --output .
```

Then add the gateway project:
```bash
dotnet sln TBE.sln add src/gateway/TBE.Gateway/TBE.Gateway.csproj
```

**Step 3: Create Dockerfile for TBE.Gateway**

Create `src/gateway/TBE.Gateway/Dockerfile`:
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/gateway/TBE.Gateway/TBE.Gateway.csproj", "src/gateway/TBE.Gateway/"]
RUN dotnet restore "src/gateway/TBE.Gateway/TBE.Gateway.csproj"
COPY . .
WORKDIR "/src/src/gateway/TBE.Gateway"
RUN dotnet build "TBE.Gateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TBE.Gateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TBE.Gateway.dll"]
```

The Dockerfile uses the build context root (`.`) so it can access shared projects added later. The `COPY . .` step copies the entire repository into the build image.
  </action>
  <verify>
    <automated>cd "C:/Users/zhdishaq/source/repos/TBE" && grep "Yarp.ReverseProxy" src/gateway/TBE.Gateway/TBE.Gateway.csproj && grep "Version=\"2.3.0\"" src/gateway/TBE.Gateway/TBE.Gateway.csproj && dotnet sln TBE.sln list | grep -i "Gateway"</automated>
  </verify>
  <acceptance_criteria>
    - `src/gateway/TBE.Gateway/TBE.Gateway.csproj` exists and contains `Yarp.ReverseProxy` with `Version="2.3.0"`
    - `src/gateway/TBE.Gateway/TBE.Gateway.csproj` contains `Serilog.AspNetCore` with `Version="10.0.0"`
    - `src/gateway/TBE.Gateway/Dockerfile` exists and contains `mcr.microsoft.com/dotnet/aspnet:8.0`
    - `dotnet sln TBE.sln list` output includes `TBE.Gateway.csproj`
    - `dotnet build src/gateway/TBE.Gateway` exits code 0 (project must compile clean)
  </acceptance_criteria>
  <done>TBE.Gateway project exists, compiles, has YARP 2.3.0 and Serilog packages, has a multi-stage Dockerfile, and is registered in TBE.sln</done>
</task>

<task type="auto">
  <name>Task 2: Implement Program.cs with three JWT schemes + authorization policies, and appsettings.json with YARP routes for all 9 services</name>
  <read_first>
    - src/gateway/TBE.Gateway/TBE.Gateway.csproj (just created — verify packages before coding)
    - .planning/phases/01-infrastructure-foundation/01-RESEARCH.md — "Program.cs Configuration" and "appsettings.json Route Configuration" sections; gotcha on AddAuthenticationSchemes
    - docker-compose.yml — verify Docker service names match cluster Address values
  </read_first>
  <files>src/gateway/TBE.Gateway/Program.cs, src/gateway/TBE.Gateway/appsettings.json, src/gateway/TBE.Gateway/appsettings.Development.json</files>
  <action>
**`src/gateway/TBE.Gateway/Program.cs`** — replace the generated file entirely:

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Formatting.Compact;

// Bootstrap logger — before builder is created
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
                     .Enrich.WithProperty("Service", "TBE.Gateway"));

    var keycloakBaseUrl = builder.Configuration["Keycloak:BaseUrl"]
        ?? "http://keycloak:8080";

    // Three JWT bearer schemes — one per Keycloak realm
    // CRITICAL: each scheme must be named and each policy must call AddAuthenticationSchemes()
    // Without AddAuthenticationSchemes(), ASP.NET Core tries all schemes and returns inconsistent 401s
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer("B2C", options =>
        {
            options.Authority = $"{keycloakBaseUrl}/realms/tbe-b2c";
            options.RequireHttpsMetadata = false; // dev only — set true in production
            options.Audience = "tbe-gateway";
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = $"{keycloakBaseUrl}/realms/tbe-b2c",
                ValidateAudience = false, // Keycloak puts audience in 'azp' not 'aud' by default
                ValidateLifetime = true
            };
        })
        .AddJwtBearer("B2B", options =>
        {
            options.Authority = $"{keycloakBaseUrl}/realms/tbe-b2b";
            options.RequireHttpsMetadata = false;
            options.Audience = "tbe-gateway";
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = $"{keycloakBaseUrl}/realms/tbe-b2b",
                ValidateAudience = false,
                ValidateLifetime = true
            };
        })
        .AddJwtBearer("Backoffice", options =>
        {
            options.Authority = $"{keycloakBaseUrl}/realms/tbe-backoffice";
            options.RequireHttpsMetadata = false;
            options.Audience = "tbe-gateway";
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = $"{keycloakBaseUrl}/realms/tbe-backoffice",
                ValidateAudience = false,
                ValidateLifetime = true
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

    builder.Services.AddHealthChecks();

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapReverseProxy();
    app.MapHealthChecks("/health");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "TBE.Gateway terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
```

---

**`src/gateway/TBE.Gateway/appsettings.json`** — replace the generated file entirely:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning"
    }
  },
  "Keycloak": {
    "BaseUrl": "http://keycloak:8080"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.Hosting.Lifetime": "Information",
        "Yarp": "Warning",
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
    "Enrich": ["FromLogContext"]
  },
  "ReverseProxy": {
    "Routes": {
      "b2c-search": {
        "ClusterId": "search-cluster",
        "AuthorizationPolicy": "B2CPolicy",
        "Match": { "Path": "/api/b2c/search/{**catch-all}" },
        "Transforms": [{ "PathPattern": "/api/search/{**catch-all}" }]
      },
      "b2c-bookings": {
        "ClusterId": "booking-cluster",
        "AuthorizationPolicy": "B2CPolicy",
        "Match": { "Path": "/api/b2c/bookings/{**catch-all}" },
        "Transforms": [{ "PathPattern": "/api/bookings/{**catch-all}" }]
      },
      "b2c-pricing": {
        "ClusterId": "pricing-cluster",
        "AuthorizationPolicy": "B2CPolicy",
        "Match": { "Path": "/api/b2c/pricing/{**catch-all}" },
        "Transforms": [{ "PathPattern": "/api/pricing/{**catch-all}" }]
      },
      "b2b-search": {
        "ClusterId": "search-cluster",
        "AuthorizationPolicy": "B2BPolicy",
        "Match": { "Path": "/api/b2b/search/{**catch-all}" },
        "Transforms": [{ "PathPattern": "/api/search/{**catch-all}" }]
      },
      "b2b-bookings": {
        "ClusterId": "booking-cluster",
        "AuthorizationPolicy": "B2BPolicy",
        "Match": { "Path": "/api/b2b/bookings/{**catch-all}" },
        "Transforms": [{ "PathPattern": "/api/bookings/{**catch-all}" }]
      },
      "b2b-pricing": {
        "ClusterId": "pricing-cluster",
        "AuthorizationPolicy": "B2BPolicy",
        "Match": { "Path": "/api/b2b/pricing/{**catch-all}" },
        "Transforms": [{ "PathPattern": "/api/pricing/{**catch-all}" }]
      },
      "backoffice-all": {
        "ClusterId": "backoffice-cluster",
        "AuthorizationPolicy": "BackofficePolicy",
        "Match": { "Path": "/api/backoffice/{**catch-all}" },
        "Transforms": [{ "PathPattern": "/api/{**catch-all}" }]
      },
      "backoffice-crm": {
        "ClusterId": "crm-cluster",
        "AuthorizationPolicy": "BackofficePolicy",
        "Match": { "Path": "/api/crm/{**catch-all}" },
        "Transforms": [{ "PathPattern": "/api/{**catch-all}" }]
      }
    },
    "Clusters": {
      "booking-cluster": {
        "Destinations": {
          "d1": { "Address": "http://booking-service:8080/" }
        }
      },
      "payment-cluster": {
        "Destinations": {
          "d1": { "Address": "http://payment-service:8080/" }
        }
      },
      "search-cluster": {
        "Destinations": {
          "d1": { "Address": "http://search-service:8080/" }
        }
      },
      "flight-connector-cluster": {
        "Destinations": {
          "d1": { "Address": "http://flight-connector-service:8080/" }
        }
      },
      "hotel-connector-cluster": {
        "Destinations": {
          "d1": { "Address": "http://hotel-connector-service:8080/" }
        }
      },
      "pricing-cluster": {
        "Destinations": {
          "d1": { "Address": "http://pricing-service:8080/" }
        }
      },
      "notification-cluster": {
        "Destinations": {
          "d1": { "Address": "http://notification-service:8080/" }
        }
      },
      "crm-cluster": {
        "Destinations": {
          "d1": { "Address": "http://crm-service:8080/" }
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

---

**`src/gateway/TBE.Gateway/appsettings.Development.json`** — override Keycloak URL for local development (when running gateway outside Docker pointing at localhost Keycloak):

```json
{
  "Keycloak": {
    "BaseUrl": "http://localhost:8080"
  }
}
```

Note: When running inside Docker Compose, `appsettings.json` is used and Keycloak BaseUrl is `http://keycloak:8080` (Docker DNS). When running `dotnet run` locally with containers on localhost, `appsettings.Development.json` overrides to `http://localhost:8080`.
  </action>
  <verify>
    <automated>cd "C:/Users/zhdishaq/source/repos/TBE" && grep "AddAuthenticationSchemes" src/gateway/TBE.Gateway/Program.cs | wc -l && grep "B2CPolicy\|B2BPolicy\|BackofficePolicy" src/gateway/TBE.Gateway/appsettings.json | wc -l && grep "booking-service:8080" src/gateway/TBE.Gateway/appsettings.json && dotnet build src/gateway/TBE.Gateway/TBE.Gateway.csproj -c Release --no-restore 2>&1 | tail -5</automated>
  </verify>
  <acceptance_criteria>
    - `Program.cs` contains exactly 3 calls to `.AddJwtBearer(` with names "B2C", "B2B", "Backoffice"
    - `Program.cs` contains exactly 3 calls to `.AddAuthenticationSchemes(` (one per policy — this is the critical gotcha fix)
    - `Program.cs` contains `app.UseAuthentication()` BEFORE `app.UseAuthorization()` BEFORE `app.MapReverseProxy()`
    - `appsettings.json` contains `"AuthorizationPolicy": "B2CPolicy"` and `"AuthorizationPolicy": "B2BPolicy"` and `"AuthorizationPolicy": "BackofficePolicy"`
    - `appsettings.json` contains all 9 cluster definitions with correct Docker service DNS names ending in `:8080`
    - `dotnet build src/gateway/TBE.Gateway/TBE.Gateway.csproj` exits code 0
  </acceptance_criteria>
  <done>Gateway Program.cs implements three isolated JWT bearer schemes with explicit authorization policy scheme binding; appsettings.json configures YARP routes covering B2C, B2B, and backoffice traffic to all 9 downstream services; project builds clean</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| client → gateway | Untrusted JWT tokens arrive from external clients at the gateway boundary |
| gateway → Keycloak JWKS | Gateway fetches public keys from Keycloak via internal Docker network; key cache poisoning possible |
| gateway → downstream services | Authenticated requests forwarded over unencrypted Docker bridge |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-02-01 | Spoofing | JWT validation — `ValidateAudience = false` | mitigate | Set `ValidateAudience = false` only because Keycloak puts audience in `azp` claim by default; document explicitly; revisit in Phase 7 when production Keycloak clients are configured with explicit audience mappers |
| T-02-02 | Spoofing | JWKS cold-start failure (Keycloak unavailable at gateway startup) | mitigate | `depends_on keycloak: condition: service_healthy` in docker-compose.yml ensures Keycloak is ready before gateway starts; JWKS is fetched and cached on first request |
| T-02-03 | Elevation of Privilege | B2C JWT accepted on B2B route | mitigate | Each policy calls `.AddAuthenticationSchemes("SchemeName")` explicitly; B2C JWT signed by `tbe-b2c` realm cannot have issuer matching `tbe-b2b` realm — cross-realm tokens are rejected |
| T-02-04 | Information Disclosure | `RequireHttpsMetadata = false` | accept | Dev-only setting; RESEARCH.md documents this must be removed for production; comment added in Program.cs |
| T-02-05 | Tampering | Authorization header forwarded unchanged to downstream | accept | Downstream services are inside the Docker bridge and trusted; bearer tokens are read-only — downstream validates signature independently in Phase 3+ |
| T-02-06 | Denial of Service | YARP misconfiguration causes 502 flood if downstream service is down | accept | Phase 1 stub services may not be running; YARP returns 502 naturally; circuit breaker deferred to Phase 7 |
</threat_model>

<verification>
After both tasks complete:

```bash
# Project builds
dotnet build src/gateway/TBE.Gateway/TBE.Gateway.csproj -c Release

# JWT scheme isolation is present
grep -c "AddAuthenticationSchemes" src/gateway/TBE.Gateway/Program.cs
# Expected: 3

# All 9 clusters exist in appsettings
grep -c "Address.*:8080" src/gateway/TBE.Gateway/appsettings.json
# Expected: 9

# Middleware order is correct
grep -n "UseAuthentication\|UseAuthorization\|MapReverseProxy" src/gateway/TBE.Gateway/Program.cs
# UseAuthentication must have lower line number than UseAuthorization
# UseAuthorization must have lower line number than MapReverseProxy
```
</verification>

<success_criteria>
- `TBE.Gateway` project compiles without errors or warnings
- `Program.cs` registers three JWT bearer schemes and three authorization policies, each with explicit scheme binding
- `appsettings.json` defines routes for B2C, B2B, and backoffice traffic covering all 9 downstream services
- Cluster addresses use Docker Compose service DNS names (e.g., `http://booking-service:8080/`)
- `appsettings.Development.json` overrides Keycloak URL to `http://localhost:8080` for local dotnet run
- Dockerfile builds the gateway as a multi-stage .NET 8 image
</success_criteria>

<output>
After completion, create `.planning/phases/01-infrastructure-foundation/01-yarp-keycloak-auth-SUMMARY.md` with:
- Files created/modified
- Key implementation decisions (ValidateAudience=false rationale, AddAuthenticationSchemes fix)
- Build verification result
- Any deviations from the plan and why
</output>
