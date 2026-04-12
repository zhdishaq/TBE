---
phase: 01-infrastructure-foundation
plan: 02
subsystem: gateway
tags: [yarp, keycloak, jwt, authentication, gateway, infra]
requires: [docker-compose-stack]
provides: [tbe-gateway, jwt-auth, yarp-routing]
affects: [all-services]
tech_stack_added: [Yarp.ReverseProxy-2.3.0, Microsoft.AspNetCore.Authentication.JwtBearer-8.0.0, Serilog.AspNetCore-10.0.0]
tech_stack_patterns: [multi-scheme-jwt, per-policy-scheme-binding, yarp-config-from-appsettings]
key_files_created:
  - TBE.slnx
  - src/gateway/TBE.Gateway/TBE.Gateway.csproj
  - src/gateway/TBE.Gateway/Program.cs
  - src/gateway/TBE.Gateway/appsettings.json
  - src/gateway/TBE.Gateway/appsettings.Development.json
  - src/gateway/TBE.Gateway/Dockerfile
key_files_modified: []
decisions:
  - "TBE.slnx used instead of TBE.sln — dotnet new sln on .NET 10 SDK generates .slnx format; .slnx is the new standard and is fully supported by dotnet CLI and VS 2022 17.9+"
  - "Microsoft.AspNetCore.Authentication.JwtBearer 8.0.0 added explicitly — not pulled in transitively by Yarp.ReverseProxy; required for AddJwtBearer extension method"
  - "ValidateAudience = false on all three schemes — Keycloak places audience in azp claim not aud by default; documented with comment; revisit in Phase 7 with explicit audience mappers"
  - "AddAuthenticationSchemes() called on every policy — critical YARP multi-scheme pattern; without this ASP.NET Core tries all schemes and returns inconsistent 401s"
  - "appsettings.Development.json overrides Keycloak BaseUrl to http://localhost:8080 — enables dotnet run locally against containerized Keycloak on localhost"
metrics:
  duration: "3 minutes"
  completed: "2026-04-12"
  tasks_completed: 2
  tasks_total: 2
  files_created: 6
  files_modified: 1
---

# Phase 01 Plan 02: YARP Gateway + Keycloak Auth Summary

**One-liner:** YARP 2.3.0 reverse proxy with three isolated Keycloak JWT bearer schemes (B2C/B2B/Backoffice), per-policy scheme binding, and routes for all 9 downstream microservices — gateway compiles clean and is wired into Docker Compose.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Create TBE.Gateway project, Dockerfile, TBE.slnx | 164e035 | TBE.slnx, TBE.Gateway.csproj, Dockerfile |
| 2 | Implement Program.cs with JWT schemes and appsettings.json with YARP routes | 7ef0d36 | Program.cs, appsettings.json, appsettings.Development.json, TBE.Gateway.csproj |

## Acceptance Criteria Status

| Criterion | Status |
|-----------|--------|
| TBE.Gateway.csproj has Yarp.ReverseProxy Version="2.3.0" | PASS |
| TBE.Gateway.csproj has Serilog.AspNetCore Version="10.0.0" | PASS |
| Dockerfile exists with mcr.microsoft.com/dotnet/aspnet:8.0 | PASS |
| TBE.Gateway in dotnet sln list | PASS |
| dotnet build exits code 0 | PASS |
| Program.cs has 3 AddJwtBearer calls (B2C, B2B, Backoffice) | PASS |
| Program.cs has 3 AddAuthenticationSchemes calls (one per policy) | PASS |
| Middleware order: UseAuthentication → UseAuthorization → MapReverseProxy | PASS |
| appsettings.json has B2CPolicy, B2BPolicy, BackofficePolicy on routes | PASS |
| appsettings.json has 9 cluster definitions with :8080 addresses | PASS |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Added Microsoft.AspNetCore.Authentication.JwtBearer 8.0.0**
- **Found during:** Task 2 build
- **Issue:** `AddJwtBearer` and `TokenValidationParameters` require `Microsoft.AspNetCore.Authentication.JwtBearer` package; it is not pulled in transitively by `Yarp.ReverseProxy` or the `Microsoft.NET.Sdk.Web` SDK on net8.0 (verified — build failed with CS0234 error)
- **Fix:** Added `<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.0" />` to TBE.Gateway.csproj
- **Files modified:** src/gateway/TBE.Gateway/TBE.Gateway.csproj
- **Commit:** 7ef0d36

**2. [Rule 3 - Blocking] TBE.slnx generated instead of TBE.sln**
- **Found during:** Task 1
- **Issue:** `dotnet new sln` on .NET SDK 10.0.200-preview generates `.slnx` format (new XML-based solution format) instead of `.sln`; `dotnet sln TBE.sln` commands in the plan failed
- **Fix:** Accepted `.slnx` as the solution file; updated all solution commands to use `TBE.slnx`; `.slnx` is fully supported by `dotnet sln` CLI and Visual Studio 2022 17.9+
- **Files modified:** TBE.slnx (created as .slnx instead of .sln)
- **Commit:** 164e035

## Known Stubs

None — this plan creates infrastructure configuration only; no data flow or application stubs.

## Threat Flags

All threat mitigations from the plan's threat model applied:

| Threat ID | Mitigation Applied |
|-----------|-------------------|
| T-02-01 | ValidateAudience=false documented with inline comment and noted in decisions; Phase 7 revisit recorded |
| T-02-02 | Gateway depends_on keycloak: condition: service_healthy in docker-compose.yml (Plan 01 work); JWKS cached on first request |
| T-02-03 | AddAuthenticationSchemes("SchemeName") on each policy ensures realm isolation; B2C token issued by tbe-b2c realm cannot pass tbe-b2b issuer validation |
| T-02-04 | RequireHttpsMetadata=false has inline comment: "dev only — set true in production" |

## Self-Check: PASSED
