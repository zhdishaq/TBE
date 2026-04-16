---
phase: 01-infrastructure-foundation
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - docker-compose.yml
  - .env.example
  - docker-compose.override.yml.example
  - .gitignore
  - infra/keycloak/realms/tbe-b2c-realm.json
  - infra/keycloak/realms/tbe-b2b-realm.json
  - infra/keycloak/realms/tbe-backoffice-realm.json
autonomous: true
requirements:
  - INFRA-01
  - INFRA-03

must_haves:
  truths:
    - "Running `docker-compose up --build` from a clean checkout starts all 13 containers (9 services + MSSQL + RabbitMQ + Redis + Keycloak) with no manual steps"
    - "All infrastructure containers (mssql, rabbitmq, redis, keycloak) reach healthy state before any service container starts"
    - "db-migrator exits code 0 before the 9 application service containers start"
    - "Keycloak admin console shows three realms: tbe-b2c, tbe-b2b, tbe-backoffice"
    - ".env is git-ignored; .env.example is committed with obvious placeholder values"
  artifacts:
    - path: "docker-compose.yml"
      provides: "Full 13-container orchestration with dependency chains and health checks"
      contains: "service_healthy"
    - path: ".env.example"
      provides: "All required env var placeholders for developer onboarding"
      contains: "MSSQL_SA_PASSWORD"
    - path: "infra/keycloak/realms/tbe-b2c-realm.json"
      provides: "B2C realm definition with tbe-web and tbe-gateway clients"
      contains: "tbe-b2c"
    - path: "infra/keycloak/realms/tbe-b2b-realm.json"
      provides: "B2B realm with agent roles"
      contains: "tbe-b2b"
    - path: "infra/keycloak/realms/tbe-backoffice-realm.json"
      provides: "Backoffice realm with staff roles"
      contains: "tbe-backoffice"
  key_links:
    - from: "docker-compose.yml keycloak service"
      to: "infra/keycloak/realms/"
      via: "volume mount ./infra/keycloak/realms:/opt/keycloak/data/import"
      pattern: "opt/keycloak/data/import"
    - from: "all 9 service containers"
      to: "db-migrator"
      via: "condition: service_completed_successfully"
      pattern: "service_completed_successfully"
---

<objective>
Create the complete Docker Compose orchestration for the TBE platform: 13 containers (9 service stubs + MSSQL + RabbitMQ + Redis + Keycloak) with health checks, correct dependency ordering, persisted volumes, and Keycloak realm JSON files that auto-import on first boot.

Purpose: Provides the foundational `docker-compose up` single-command startup that all subsequent phases depend on. Without this, no developer can run the platform locally.

Output: `docker-compose.yml`, `.env.example`, `docker-compose.override.yml.example`, Keycloak realm JSON files, updated `.gitignore`.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/PROJECT.md
@.planning/ROADMAP.md
@.planning/phases/01-infrastructure-foundation/01-CONTEXT.md
@.planning/phases/01-infrastructure-foundation/01-RESEARCH.md
</context>

<tasks>

<task type="auto">
  <name>Task 1: Create docker-compose.yml with all 13 containers and health check chains</name>
  <read_first>
    - .planning/phases/01-infrastructure-foundation/01-CONTEXT.md — decisions D-01 through D-13 (solution layout, secrets strategy)
    - .planning/phases/01-infrastructure-foundation/01-RESEARCH.md — Docker Compose health checks section; dependency chain diagram; MSSQL 2022 gotcha (mssql-tools18 path)
  </read_first>
  <files>docker-compose.yml, .env.example, docker-compose.override.yml.example, .gitignore</files>
  <action>
Create `docker-compose.yml` at the repository root with the following exact structure. Every value below is authoritative — use it verbatim unless a comment says otherwise.

**Infrastructure containers (Wave 0 — no dependencies):**

```yaml
services:
  mssql:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_SA_PASSWORD: "${MSSQL_SA_PASSWORD}"
    volumes:
      - mssql-data:/var/opt/mssql
    ports:
      - "1433:1433"
    healthcheck:
      test: /opt/mssql-tools18/bin/sqlcmd -S localhost -C -U sa -P "$$MSSQL_SA_PASSWORD" -Q "SELECT 1" -b -o /dev/null
      interval: 10s
      timeout: 45s
      retries: 45
      start_period: 10s

  rabbitmq:
    image: rabbitmq:3.13-management
    environment:
      RABBITMQ_DEFAULT_USER: "${RABBITMQ_USER}"
      RABBITMQ_DEFAULT_PASS: "${RABBITMQ_PASSWORD}"
    volumes:
      - rabbitmq-data:/var/lib/rabbitmq
    ports:
      - "5672:5672"
      - "15672:15672"
    healthcheck:
      test: ["CMD", "rabbitmq-diagnostics", "check_port_connectivity"]
      interval: 10s
      timeout: 10s
      retries: 10
      start_period: 30s

  redis:
    image: redis:7.2-alpine
    command: redis-server --appendonly yes --requirepass "${REDIS_PASSWORD}"
    volumes:
      - redis-data:/data
    ports:
      - "6379:6379"
    healthcheck:
      test: ["CMD", "redis-cli", "-a", "${REDIS_PASSWORD}", "ping"]
      interval: 5s
      timeout: 5s
      retries: 10
      start_period: 5s

  keycloak:
    image: quay.io/keycloak/keycloak:26.6.0
    command: start-dev --import-realm
    environment:
      KC_HEALTH_ENABLED: "true"
      KC_BOOTSTRAP_ADMIN_USERNAME: "${KEYCLOAK_ADMIN_USER}"
      KC_BOOTSTRAP_ADMIN_PASSWORD: "${KEYCLOAK_ADMIN_PASSWORD}"
      KC_DB: mssql
      KC_DB_URL: "jdbc:sqlserver://mssql:1433;databaseName=KeycloakDb;encrypt=false"
      KC_DB_USERNAME: "${MSSQL_SA_USER}"
      KC_DB_PASSWORD: "${MSSQL_SA_PASSWORD}"
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

**db-migrator (Wave 1 — depends on mssql healthy):**

```yaml
  db-migrator:
    build:
      context: .
      dockerfile: src/tools/TBE.DbMigrator/Dockerfile
    environment:
      ConnectionStrings__BookingDb: "${BOOKING_DB_CONNECTION}"
      ConnectionStrings__PaymentDb: "${PAYMENT_DB_CONNECTION}"
      ConnectionStrings__SearchDb: "${SEARCH_DB_CONNECTION}"
      ConnectionStrings__PricingDb: "${PRICING_DB_CONNECTION}"
      ConnectionStrings__NotificationDb: "${NOTIFICATION_DB_CONNECTION}"
      ConnectionStrings__CrmDb: "${CRM_DB_CONNECTION}"
      ConnectionStrings__BackofficeDb: "${BACKOFFICE_DB_CONNECTION}"
      ConnectionStrings__FlightConnectorDb: "${FLIGHT_CONNECTOR_DB_CONNECTION}"
      ConnectionStrings__HotelConnectorDb: "${HOTEL_CONNECTOR_DB_CONNECTION}"
    depends_on:
      mssql:
        condition: service_healthy
    restart: "no"
```

**9 Application services (Wave 2 — depends on mssql + rabbitmq + redis healthy AND db-migrator completed):**

For each of the 9 services (booking-service, payment-service, search-service, flight-connector-service, hotel-connector-service, pricing-service, notification-service, crm-service, backoffice-service), add a block with this pattern (shown for booking-service; replicate for all 9):

```yaml
  booking-service:
    build:
      context: .
      dockerfile: src/services/BookingService/BookingService.API/Dockerfile
    environment:
      ASPNETCORE_ENVIRONMENT: "Development"
      ConnectionStrings__BookingDb: "${BOOKING_DB_CONNECTION}"
      RabbitMQ__Host: "rabbitmq"
      RabbitMQ__Username: "${RABBITMQ_USER}"
      RabbitMQ__Password: "${RABBITMQ_PASSWORD}"
      Redis__ConnectionString: "redis:6379,password=${REDIS_PASSWORD}"
    depends_on:
      mssql:
        condition: service_healthy
      rabbitmq:
        condition: service_healthy
      redis:
        condition: service_healthy
      db-migrator:
        condition: service_completed_successfully
    ports:
      - "5001:8080"
```

Port assignments (host:container 8080):
- tbe-gateway: 5000
- booking-service: 5001
- payment-service: 5002
- search-service: 5003
- flight-connector-service: 5004
- hotel-connector-service: 5005
- pricing-service: 5006
- notification-service: 5007
- crm-service: 5008
- backoffice-service: 5009

The tbe-gateway service also depends on keycloak healthy:
```yaml
  tbe-gateway:
    build:
      context: .
      dockerfile: src/gateway/TBE.Gateway/Dockerfile
    environment:
      ASPNETCORE_ENVIRONMENT: "Development"
      RabbitMQ__Host: "rabbitmq"
      RabbitMQ__Username: "${RABBITMQ_USER}"
      RabbitMQ__Password: "${RABBITMQ_PASSWORD}"
      Redis__ConnectionString: "redis:6379,password=${REDIS_PASSWORD}"
      Keycloak__BaseUrl: "http://keycloak:8080"
    depends_on:
      keycloak:
        condition: service_healthy
      rabbitmq:
        condition: service_healthy
      redis:
        condition: service_healthy
      db-migrator:
        condition: service_completed_successfully
    ports:
      - "5000:8080"
```

**Volumes block (at end of file):**
```yaml
volumes:
  mssql-data:
  rabbitmq-data:
  redis-data:
```

---

Create `.env.example` at repository root with ALL env vars referenced in docker-compose.yml. Use obviously-fake placeholder values per D-10:

```
# TBE Environment — copy to .env and fill in real values
# NEVER commit .env to git

# MSSQL
MSSQL_SA_USER=sa
MSSQL_SA_PASSWORD=Your_Password123!

# Per-service connection strings (point to same MSSQL host, different databases)
BOOKING_DB_CONNECTION=Server=mssql,1433;Database=BookingDb;User Id=sa;Password=Your_Password123!;TrustServerCertificate=True;
PAYMENT_DB_CONNECTION=Server=mssql,1433;Database=PaymentDb;User Id=sa;Password=Your_Password123!;TrustServerCertificate=True;
SEARCH_DB_CONNECTION=Server=mssql,1433;Database=SearchDb;User Id=sa;Password=Your_Password123!;TrustServerCertificate=True;
PRICING_DB_CONNECTION=Server=mssql,1433;Database=PricingDb;User Id=sa;Password=Your_Password123!;TrustServerCertificate=True;
NOTIFICATION_DB_CONNECTION=Server=mssql,1433;Database=NotificationDb;User Id=sa;Password=Your_Password123!;TrustServerCertificate=True;
CRM_DB_CONNECTION=Server=mssql,1433;Database=CrmDb;User Id=sa;Password=Your_Password123!;TrustServerCertificate=True;
BACKOFFICE_DB_CONNECTION=Server=mssql,1433;Database=BackofficeDb;User Id=sa;Password=Your_Password123!;TrustServerCertificate=True;
FLIGHT_CONNECTOR_DB_CONNECTION=Server=mssql,1433;Database=FlightConnectorDb;User Id=sa;Password=Your_Password123!;TrustServerCertificate=True;
HOTEL_CONNECTOR_DB_CONNECTION=Server=mssql,1433;Database=HotelConnectorDb;User Id=sa;Password=Your_Password123!;TrustServerCertificate=True;

# RabbitMQ
RABBITMQ_USER=tbe_admin
RABBITMQ_PASSWORD=Your_RabbitMQ_Password123!

# Redis
REDIS_PASSWORD=Your_Redis_Password123!

# Keycloak
KEYCLOAK_ADMIN_USER=admin
KEYCLOAK_ADMIN_PASSWORD=Your_Keycloak_Admin_Password123!

# Keycloak realm client secrets (injected into realm JSON via KC env substitution)
TBE_B2C_GATEWAY_CLIENT_SECRET=your-b2c-gateway-secret-here
TBE_B2B_GATEWAY_CLIENT_SECRET=your-b2b-gateway-secret-here
TBE_BACKOFFICE_GATEWAY_CLIENT_SECRET=your-backoffice-gateway-secret-here

# GDS API keys (Phase 2 — leave blank for Phase 1)
AMADEUS_API_KEY=
AMADEUS_API_SECRET=
```

---

Create `docker-compose.override.yml.example` at repository root per D-11:

```yaml
# docker-compose.override.yml — git-ignored; copy from .override.yml.example
# Use this file for local developer-specific settings (port remapping, volume paths)
# Example: bind RabbitMQ management and Keycloak admin to localhost only
services:
  rabbitmq:
    ports:
      - "127.0.0.1:15672:15672"
      - "127.0.0.1:5672:5672"
  keycloak:
    ports:
      - "127.0.0.1:8080:8080"
      - "127.0.0.1:9000:9000"
```

---

Update `.gitignore` to include (append if file exists, create if not):
```
# TBE environment secrets
.env
docker-compose.override.yml

# Build outputs
**/bin/
**/obj/

# Rider / VS
.idea/
*.user
.vs/
```
  </action>
  <verify>
    <automated>cd "C:/Users/zhdishaq/source/repos/TBE" && grep -c "service_healthy" docker-compose.yml && grep "service_completed_successfully" docker-compose.yml && grep "mssql-tools18" docker-compose.yml && grep "KC_BOOTSTRAP_ADMIN_USERNAME" docker-compose.yml && grep "tbe-b2c-realm.json" infra/keycloak/realms/tbe-b2c-realm.json 2>/dev/null || echo "realm file missing" && grep "\.env" .gitignore</automated>
  </verify>
  <acceptance_criteria>
    - `docker-compose.yml` contains the exact string `/opt/mssql-tools18/bin/sqlcmd` (NOT `/opt/mssql-tools/bin/sqlcmd`)
    - `docker-compose.yml` contains `KC_BOOTSTRAP_ADMIN_USERNAME` (NOT `KEYCLOAK_ADMIN_USERNAME`)
    - `docker-compose.yml` contains exactly 5 occurrences of `service_healthy` (mssql×1, rabbitmq×1, redis×1, keycloak×1 — all feeding into gateway; db-migrator depends on mssql only)
    - `docker-compose.yml` contains `condition: service_completed_successfully` on every one of the 9 application service `depends_on` blocks
    - `docker-compose.yml` contains `start_period: 60s` under the keycloak healthcheck
    - `.env.example` contains the string `MSSQL_SA_PASSWORD=Your_Password123!`
    - `.env.example` contains all 9 `*_DB_CONNECTION` variables
    - `.env` appears in `.gitignore`
    - `docker-compose.override.yml` appears in `.gitignore`
    - `infra/keycloak/realms/` directory exists
  </acceptance_criteria>
  <done>docker-compose.yml defines all 13 containers with correct health check dependency chains; .env.example has all required placeholders; .gitignore protects secrets; infra/keycloak/realms/ directory exists for realm JSON files (created in Task 2)</done>
</task>

<task type="auto">
  <name>Task 2: Create Keycloak realm JSON files for tbe-b2c, tbe-b2b, and tbe-backoffice</name>
  <read_first>
    - .planning/phases/01-infrastructure-foundation/01-RESEARCH.md — "Keycloak Realm Import Automation" section; minimal realm JSON example; gotcha about sslRequired
    - docker-compose.yml (just created) — verify volume mount path is ./infra/keycloak/realms
  </read_first>
  <files>infra/keycloak/realms/tbe-b2c-realm.json, infra/keycloak/realms/tbe-b2b-realm.json, infra/keycloak/realms/tbe-backoffice-realm.json</files>
  <action>
Create three Keycloak realm JSON files in `infra/keycloak/realms/`. These are auto-imported on first boot via `--import-realm` and the volume mount. The import is skipped on subsequent restarts if the realm already exists (`docker-compose down -v` forces re-import).

**CRITICAL:** Include `"sslRequired": "none"` in every realm — Keycloak 26 rejects HTTP connections without this in dev mode.

**`infra/keycloak/realms/tbe-b2c-realm.json`:**
```json
{
  "realm": "tbe-b2c",
  "enabled": true,
  "sslRequired": "none",
  "registrationAllowed": true,
  "loginWithEmailAllowed": true,
  "accessTokenLifespan": 300,
  "clients": [
    {
      "clientId": "tbe-web",
      "enabled": true,
      "publicClient": true,
      "standardFlowEnabled": true,
      "implicitFlowEnabled": false,
      "directAccessGrantsEnabled": false,
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
      "directAccessGrantsEnabled": false,
      "secret": "${TBE_B2C_GATEWAY_CLIENT_SECRET}",
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

**`infra/keycloak/realms/tbe-b2b-realm.json`:**
```json
{
  "realm": "tbe-b2b",
  "enabled": true,
  "sslRequired": "none",
  "registrationAllowed": false,
  "loginWithEmailAllowed": true,
  "accessTokenLifespan": 300,
  "clients": [
    {
      "clientId": "tbe-agent-portal",
      "enabled": true,
      "publicClient": true,
      "standardFlowEnabled": true,
      "implicitFlowEnabled": false,
      "directAccessGrantsEnabled": false,
      "redirectUris": ["http://localhost:3001/*"],
      "webOrigins": ["http://localhost:3001"],
      "protocol": "openid-connect"
    },
    {
      "clientId": "tbe-gateway",
      "enabled": true,
      "publicClient": false,
      "serviceAccountsEnabled": true,
      "standardFlowEnabled": false,
      "directAccessGrantsEnabled": false,
      "secret": "${TBE_B2B_GATEWAY_CLIENT_SECRET}",
      "protocol": "openid-connect"
    }
  ],
  "roles": {
    "realm": [
      { "name": "agent-admin", "description": "Agency administrator" },
      { "name": "agent", "description": "Travel agent" },
      { "name": "agent-readonly", "description": "Read-only agent access" }
    ]
  }
}
```

**`infra/keycloak/realms/tbe-backoffice-realm.json`:**
```json
{
  "realm": "tbe-backoffice",
  "enabled": true,
  "sslRequired": "none",
  "registrationAllowed": false,
  "loginWithEmailAllowed": true,
  "accessTokenLifespan": 300,
  "clients": [
    {
      "clientId": "tbe-backoffice-ui",
      "enabled": true,
      "publicClient": true,
      "standardFlowEnabled": true,
      "implicitFlowEnabled": false,
      "directAccessGrantsEnabled": false,
      "redirectUris": ["http://localhost:3002/*"],
      "webOrigins": ["http://localhost:3002"],
      "protocol": "openid-connect"
    },
    {
      "clientId": "tbe-gateway",
      "enabled": true,
      "publicClient": false,
      "serviceAccountsEnabled": true,
      "standardFlowEnabled": false,
      "directAccessGrantsEnabled": false,
      "secret": "${TBE_BACKOFFICE_GATEWAY_CLIENT_SECRET}",
      "protocol": "openid-connect"
    }
  ],
  "roles": {
    "realm": [
      { "name": "backoffice-admin", "description": "Full backoffice access" },
      { "name": "backoffice-operator", "description": "Booking management" },
      { "name": "finance", "description": "Financial reports and reconciliation" }
    ]
  }
}
```

Note: The `${TBE_B2C_GATEWAY_CLIENT_SECRET}` syntax uses Keycloak's built-in environment variable substitution in realm import files. The env vars must be set in the Keycloak container's environment for substitution to work. These are already included in `.env.example` from Task 1.
  </action>
  <verify>
    <automated>cd "C:/Users/zhdishaq/source/repos/TBE" && grep '"sslRequired": "none"' infra/keycloak/realms/tbe-b2c-realm.json && grep '"sslRequired": "none"' infra/keycloak/realms/tbe-b2b-realm.json && grep '"sslRequired": "none"' infra/keycloak/realms/tbe-backoffice-realm.json && grep 'agent-admin' infra/keycloak/realms/tbe-b2b-realm.json && grep 'backoffice-admin' infra/keycloak/realms/tbe-backoffice-realm.json && grep 'customer' infra/keycloak/realms/tbe-b2c-realm.json</automated>
  </verify>
  <acceptance_criteria>
    - `infra/keycloak/realms/tbe-b2c-realm.json` exists and contains `"sslRequired": "none"` and `"realm": "tbe-b2c"` and `"clientId": "tbe-web"` and `"clientId": "tbe-gateway"`
    - `infra/keycloak/realms/tbe-b2b-realm.json` exists and contains `"sslRequired": "none"` and `"realm": "tbe-b2b"` and roles `agent-admin`, `agent`, `agent-readonly`
    - `infra/keycloak/realms/tbe-backoffice-realm.json` exists and contains `"sslRequired": "none"` and `"realm": "tbe-backoffice"` and roles `backoffice-admin`, `backoffice-operator`, `finance`
    - All three files are valid JSON (parseable without errors)
    - All three files have `"serviceAccountsEnabled": true` on their `tbe-gateway` client entry
  </acceptance_criteria>
  <done>Three Keycloak realm JSON files exist in infra/keycloak/realms/; each defines the correct realm name, sslRequired: none, portal client, gateway service client, and realm roles; Keycloak will auto-import all three on first container boot</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| host → Docker bridge | Port bindings expose MSSQL (1433), RabbitMQ management (15672), and Keycloak admin (8080) to the host network |
| Docker bridge (internal) | All container-to-container traffic is unencrypted on the internal bridge network |
| .env file | Secrets at rest on developer filesystem; git-ignored but not encrypted |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-01-01 | Information Disclosure | .env file | mitigate | `.env` in `.gitignore` before first commit; `.env.example` uses obvious placeholders (`Your_Password123!`); add comment in `.env.example` warning against committing the real file |
| T-01-02 | Elevation of Privilege | MSSQL SA account | accept | SA account is acceptable for dev-only use; document in `.env.example` comment that per-service SQL logins must be created before production (Phase 7 hardening) |
| T-01-03 | Information Disclosure | RabbitMQ management port 15672 exposed on all interfaces | mitigate | `docker-compose.override.yml.example` binds management port to `127.0.0.1:15672:15672` only; developer instructions in override example file |
| T-01-04 | Information Disclosure | Keycloak admin console port 8080 exposed on all interfaces | mitigate | `docker-compose.override.yml.example` binds Keycloak to `127.0.0.1:8080:8080`; `start-dev` mode documented as dev-only in comments |
| T-01-05 | Information Disclosure | Inter-service HTTP (no TLS) on Docker bridge | accept | Docker bridge network isolation is acceptable for local dev; rationale: single-machine, no external access to bridge network; production remediation deferred to Phase 7 |
| T-01-06 | Spoofing | Keycloak realm import uses `${VAR}` substitution; missing env var silently produces literal string | mitigate | `.env.example` includes all `TBE_*_GATEWAY_CLIENT_SECRET` vars with placeholder values; secret used for gateway service-account clients not end-user authentication |
</threat_model>

<verification>
After both tasks complete, verify the complete Docker Compose stack structure:

```bash
# Verify compose file has all 13 services
grep -c "image:\|build:" docker-compose.yml

# Verify health check dependency chain integrity
grep "service_completed_successfully" docker-compose.yml | wc -l
# Expected: 10 (9 services + gateway all depend on db-migrator)

# Verify MSSQL health check uses correct tools path
grep "mssql-tools18" docker-compose.yml

# Verify Keycloak uses new bootstrap env vars (not legacy KEYCLOAK_USER)
grep "KC_BOOTSTRAP_ADMIN_USERNAME" docker-compose.yml

# Verify all three realm files are valid JSON
python3 -c "import json; [json.load(open(f)) for f in ['infra/keycloak/realms/tbe-b2c-realm.json','infra/keycloak/realms/tbe-b2b-realm.json','infra/keycloak/realms/tbe-backoffice-realm.json']]; print('All realm JSON valid')"

# Verify secrets are gitignored
grep "^\.env$" .gitignore
```
</verification>

<success_criteria>
- `docker-compose.yml` defines all 13 container services with correct dependency chains
- MSSQL healthcheck uses `/opt/mssql-tools18/bin/sqlcmd` (not the legacy path)
- Keycloak uses `KC_BOOTSTRAP_ADMIN_USERNAME` / `KC_BOOTSTRAP_ADMIN_PASSWORD` (not legacy `KEYCLOAK_USER`)
- 9 application services + gateway all use `condition: service_completed_successfully` on db-migrator
- 3 Keycloak realm JSON files exist with correct structure, `sslRequired: none`, correct clients, and correct role definitions
- `.env` is in `.gitignore`; `.env.example` is committed with all placeholder values
- `docker-compose.override.yml.example` shows localhost-only port binding for management interfaces
</success_criteria>

<output>
After completion, create `.planning/phases/01-infrastructure-foundation/01-docker-compose-stack-SUMMARY.md` with:
- Files created/modified
- Key decisions made (e.g., Keycloak 26.6.0, MSSQL tools18 path, db-migrator pattern)
- Any deviations from the plan and why
- Confirmation that all acceptance criteria passed
</output>
