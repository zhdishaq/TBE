---
phase: 01-infrastructure-foundation
plan: 01
subsystem: infrastructure
tags: [docker-compose, keycloak, mssql, rabbitmq, redis, infra]
requires: []
provides: [docker-compose-stack, keycloak-realms, dev-environment]
affects: [all-services]
tech_stack_added: [keycloak-26.6.0, mssql-2022-latest, rabbitmq-3.13-management, redis-7.2-alpine]
tech_stack_patterns: [health-check-dependency-chains, db-migrator-pattern, env-file-secrets]
key_files_created:
  - docker-compose.yml
  - .env.example
  - docker-compose.override.yml.example
  - .gitignore
  - infra/keycloak/realms/tbe-b2c-realm.json
  - infra/keycloak/realms/tbe-b2b-realm.json
  - infra/keycloak/realms/tbe-backoffice-realm.json
key_files_modified: []
decisions:
  - "Keycloak 26.6.0 uses KC_BOOTSTRAP_ADMIN_USERNAME (not legacy KEYCLOAK_USER env var)"
  - "MSSQL 2022 healthcheck uses /opt/mssql-tools18/bin/sqlcmd with -C flag for self-signed TLS"
  - "Dedicated db-migrator compose service (condition: service_completed_successfully) avoids 9-way migration race condition"
  - "docker-compose.override.yml.example binds management ports (15672, 8080) to 127.0.0.1 to mitigate T-01-03 and T-01-04"
  - "sslRequired: none in all Keycloak realm JSON files required for Keycloak 26 dev mode HTTP"
  - "${VAR} substitution in realm JSON files for gateway client secrets via KC env vars"
metrics:
  duration: "2 minutes"
  completed: "2026-04-12"
  tasks_completed: 2
  tasks_total: 2
  files_created: 7
  files_modified: 0
---

# Phase 01 Plan 01: Docker Compose Stack + Keycloak Realm Import Summary

**One-liner:** Full 15-container Docker Compose orchestration with MSSQL/RabbitMQ/Redis/Keycloak health-check chains, db-migrator sequencing, and three Keycloak realm JSON files for tbe-b2c/tbe-b2b/tbe-backoffice auto-import on first boot.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Create docker-compose.yml with all containers and health check chains | 2114268 | docker-compose.yml, .env.example, docker-compose.override.yml.example, .gitignore |
| 2 | Create Keycloak realm JSON files for tbe-b2c, tbe-b2b, tbe-backoffice | b9e8788 | infra/keycloak/realms/tbe-b2c-realm.json, infra/keycloak/realms/tbe-b2b-realm.json, infra/keycloak/realms/tbe-backoffice-realm.json |

## Acceptance Criteria Status

| Criterion | Status |
|-----------|--------|
| docker-compose.yml uses /opt/mssql-tools18/bin/sqlcmd | PASS |
| docker-compose.yml uses KC_BOOTSTRAP_ADMIN_USERNAME | PASS |
| 10 service_completed_successfully conditions (9 services + gateway) | PASS |
| All 10 Wave 2 containers depend on db-migrator | PASS |
| Keycloak healthcheck has start_period: 60s | PASS |
| .env.example contains MSSQL_SA_PASSWORD=Your_Password123! | PASS |
| .env.example contains all 9 *_DB_CONNECTION variables | PASS |
| .env in .gitignore | PASS |
| docker-compose.override.yml in .gitignore | PASS |
| infra/keycloak/realms/ directory exists | PASS |
| tbe-b2c-realm.json: sslRequired: none, tbe-web client, tbe-gateway service client | PASS |
| tbe-b2b-realm.json: sslRequired: none, agent-admin/agent/agent-readonly roles | PASS |
| tbe-backoffice-realm.json: sslRequired: none, backoffice-admin/backoffice-operator/finance roles | PASS |
| All realm JSON files valid JSON | PASS |
| All tbe-gateway clients have serviceAccountsEnabled: true | PASS |

## Deviations from Plan

### Auto-fixed Issues

None - plan executed exactly as written.

### Plan Inconsistency (Noted, Not Fixed)

The plan frontmatter and objective reference "13 containers" while the task action explicitly defines 15 containers: 4 infra (mssql, rabbitmq, redis, keycloak) + 1 db-migrator + 1 tbe-gateway + 9 app services = 15 total. The task action is authoritative (explicit YAML provided), so 15 containers were created. The acceptance criteria requiring "exactly 5 occurrences of service_healthy" was also inconsistent with the explicit YAML (which produces 32 such conditions because all Wave 2 services depend on mssql + rabbitmq + redis healthy). The actual service_completed_successfully count of 10 (the meaningful sequencing gate) matches the expected value exactly.

## Known Stubs

None - this plan creates infrastructure configuration files only, no application stubs with data flow.

## Threat Flags

All threat mitigations from the plan's threat model were applied:

| Threat ID | Mitigation Applied |
|-----------|-------------------|
| T-01-01 | .env in .gitignore, .env.example uses obvious placeholders with warning comment |
| T-01-02 | SA account documented in .env.example comment; per-service SQL logins deferred to Phase 7 |
| T-01-03 | docker-compose.override.yml.example binds RabbitMQ 15672 to 127.0.0.1 only |
| T-01-04 | docker-compose.override.yml.example binds Keycloak 8080/9000 to 127.0.0.1 only |
| T-01-06 | All TBE_*_GATEWAY_CLIENT_SECRET vars included in .env.example with placeholder values |

## Self-Check: PASSED

All 7 files created and found on disk. Both task commits (2114268, b9e8788) verified in git log.
