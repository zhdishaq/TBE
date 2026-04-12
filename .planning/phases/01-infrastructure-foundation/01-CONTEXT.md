# Phase 1: Infrastructure Foundation - Context

**Gathered:** 2026-04-12
**Status:** Ready for planning

<domain>
## Phase Boundary

Bootstrap the entire development environment: all 9 microservice containers, MSSQL, RabbitMQ, Redis, and Keycloak run from a single `docker-compose up`. YARP gateway is configured and validates JWTs. All service database schemas exist. MassTransit messaging is wired with outbox. Health checks and structured logging are in place. No business logic — purely infrastructure and scaffolding.

</domain>

<decisions>
## Implementation Decisions

### Solution Structure
- **D-01:** Single `TBE.sln` at the repository root — one solution file covers all services and shared projects.
- **D-02:** Services are flat under `src/services/` — `src/services/BookingService/`, `src/services/PaymentService/`, `src/services/SearchService/`, etc. No domain-grouped subdirectories.
- **D-03:** Each service has exactly 3 C# projects following the naming pattern:
  - `{ServiceName}.API` — Controllers, DI registration, middleware, startup
  - `{ServiceName}.Application` — Use cases, commands, queries, saga state machines, MediatR handlers
  - `{ServiceName}.Infrastructure` — EF Core DbContext, migrations, repository implementations, external HTTP clients
- **D-04:** The API Gateway (YARP host) lives at `src/gateway/TBE.Gateway/` — a single .NET project, not split into layers.
- **D-05:** Service naming: `BookingService`, `PaymentService`, `SearchService`, `FlightConnectorService`, `HotelConnectorService`, `PricingService`, `NotificationService`, `CrmService`, `BackofficeService` — no `TBE.` prefix on folder names, but C# namespaces use `TBE.{ServiceName}.{Layer}`.

### Shared Library Strategy
- **D-06:** Two shared projects under `src/shared/`:
  - `TBE.Contracts` — Domain event classes, RabbitMQ message contracts, canonical search models (`UnifiedFlightOffer`, `UnifiedHotelOffer`, `UnifiedCarOffer`), and integration event interfaces. This is the contract boundary between services.
  - `TBE.Common` — Shared middleware (correlation ID, request logging), base exception types, EF Core base entity, health check extensions, Serilog configuration helpers, and MassTransit consumer base classes.
- **D-07:** Services reference shared projects via `<ProjectReference>` (not NuGet packages) — keeps the dev loop fast and refactoring straightforward within the monorepo.
- **D-08:** No service may reference another service's projects directly — only `TBE.Contracts` and `TBE.Common` are shared. Cross-service communication is exclusively via RabbitMQ messages or REST through the gateway.

### Secrets Management
- **D-09:** Dev environment uses a single `.env` file at the repository root, loaded automatically by Docker Compose. The `.env` file is git-ignored.
- **D-10:** A `.env.example` file is committed to the repository with placeholder values for all required secrets (MSSQL SA password, Keycloak admin credentials, RabbitMQ user/password, Redis password, future GDS API keys). New developers copy `.env.example` to `.env` and fill in values.
- **D-11:** `docker-compose.override.yml` is used for local developer-specific overrides (port mappings, volume paths). This file is also git-ignored; a `docker-compose.override.yml.example` is committed.
- **D-12:** Production secrets are injected as environment variables by the deployment system (CI/CD pipeline or hosting platform). The application reads all configuration via `IConfiguration` — no code changes needed between dev and prod environments.
- **D-13:** GDS credentials (Amadeus, Sabre, Galileo API keys) follow the same pattern — stored in `.env` for dev, injected as environment variables in production. Never committed to the repository.

### Claude's Discretion
- Specific Docker image versions for MSSQL, RabbitMQ, Redis, Keycloak (use latest stable at time of implementation)
- Keycloak realm import JSON format and exact client configuration details
- YARP route configuration specifics (path patterns, transforms)
- MassTransit exchange topology naming (final names for queues/exchanges)
- Serilog output template and logging level configuration per service
- EF Core migration strategy for multi-service startup ordering
- Health check response format details beyond `{"status":"Healthy"}`

</decisions>

<specifics>
## Specific Ideas

- The `docker-compose up` command from a clean checkout should require zero manual steps — copying `.env.example` to `.env` and filling credentials is the only prerequisite.
- Service containers should use `depends_on` with `condition: service_healthy` so RabbitMQ, MSSQL, Redis, and Keycloak are confirmed healthy before any service starts.
- MSSQL SA password and all other credentials in `.env.example` should use obvious placeholder format (e.g., `Your_Password123!`) that is clearly not a real credential.

</specifics>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project requirements and architecture
- `.planning/REQUIREMENTS.md` — INFRA-01 through INFRA-07 define acceptance criteria for this phase
- `.planning/research/STACK.md` — Confirmed tech choices: YARP over Ocelot, Keycloak over Duende, MassTransit 8.x, specific NuGet package recommendations
- `.planning/research/ARCHITECTURE.md` — 11-service topology, service DB ownership table, service communication map (sync vs async), Keycloak realm structure, suggested build order
- `.planning/research/SUMMARY.md` — Synthesized stack decisions with rationale; critical implementation rules (e.g., YARP not Ocelot, Keycloak not Duende)

### Security constraints
- `.planning/PROJECT.md` §Constraints — PCI-DSS compliance applies; GDS credentials must not be in source code
- `.planning/REQUIREMENTS.md` §Compliance — COMP-05: GDS credentials in environment secrets/vault only; COMP-04: all endpoints require valid JWT

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- None — greenfield project. No existing code to reuse.

### Established Patterns
- None yet — this phase establishes the patterns all subsequent phases will follow.

### Integration Points
- This phase creates the foundational scaffolding. Phase 2 (Inventory) connects to the Search Service stub created here. Phase 3 (Booking Saga) connects to the Booking Service stub and RabbitMQ topology created here.

</code_context>

<deferred>
## Deferred Ideas

- Kubernetes / Helm charts — explicitly out of scope; Docker Compose is the target for v1 (single-tenant scale)
- Elastic + Kibana logging stack — deferred; Console JSON or Seq covers dev needs without the overhead
- Service mesh (Dapr, Linkerd) — not in scope for this phase or v1
- CI/CD pipeline configuration — deferred to Phase 7 (Hardening & Go-Live)

</deferred>

---

*Phase: 01-infrastructure-foundation*
*Context gathered: 2026-04-12*
