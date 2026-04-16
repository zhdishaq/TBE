# Keycloak realm artifacts (Phase 4)

This directory holds the realm definitions consumed by local and staging
Keycloak instances. Existing per-realm exports live under `realms/`;
Phase 4 Wave 0 adds two Pitfall-fix artifacts at the root of this
directory: `realm-tbe-b2c.json` and `verify-audience-smoke.sh`.

## Files

| File | Purpose |
|------|---------|
| `realms/tbe-b2c-realm.json` | Baseline realm (from Phase 1). Keeps `tbe-web`, `tbe-gateway` clients. |
| `realm-tbe-b2c.json` | **Wave 0 patch** — adds `tbe-b2c` OIDC client with audience mapper emitting `aud=tbe-api` (Pitfall 4) plus `tbe-b2c-admin` service client with `manage-users` / `view-users` role (Pitfall 8). |
| `verify-audience-smoke.sh` | Real-token smoke script. Uses the `tbe-b2c-admin` client_credentials flow to obtain an access token and asserts that `aud` contains `tbe-api`. Also exercises `send-verify-email` to prove Pitfall 8 is resolved. |

## Importing the patch

```bash
# From inside the running Keycloak container, or via kcadm.sh:
kc.sh import --file /opt/keycloak/data/import/realm-tbe-b2c.json

# Or, if merging into an existing realm, diff the clients/protocolMappers
# arrays into the existing export and re-apply via partial import in the
# admin console (Realm → Manage → Import).
```

## Secret rotation

Both clients use Keycloak-issued secrets interpolated via environment
variables in the JSON (`${KEYCLOAK_B2C_CLIENT_SECRET}` and
`${KEYCLOAK_B2C_ADMIN_CLIENT_SECRET}`). To rotate:

1. Keycloak admin → Clients → `tbe-b2c` (or `tbe-b2c-admin`) → Credentials
   → Regenerate.
2. Update `.env` / deployment secret store for both the portal and any
   consumer services.
3. Re-run `./verify-audience-smoke.sh` to confirm the new secret works.

## Env vars produced / required

| Var | Source | Consumer |
|-----|--------|----------|
| `KEYCLOAK_B2C_CLIENT_ID` | static (`tbe-b2c`) | src/portals/b2c-web auth.config.ts |
| `KEYCLOAK_B2C_CLIENT_SECRET` | Keycloak credential | src/portals/b2c-web auth.config.ts |
| `KEYCLOAK_B2C_ISSUER` | Keycloak `.well-known` URL | src/portals/b2c-web auth.config.ts |
| `KEYCLOAK_B2C_ADMIN_CLIENT_ID` | static (`tbe-b2c-admin`) | future admin route handlers |
| `KEYCLOAK_B2C_ADMIN_CLIENT_SECRET` | Keycloak credential | future admin route handlers |

## Smoke test

After importing the patch and populating env vars, run:

```bash
chmod +x infra/keycloak/verify-audience-smoke.sh
bash infra/keycloak/verify-audience-smoke.sh
```

Exit code `0` means the Pitfall 4 audience mapper and Pitfall 8 admin
role binding are both functional. The script prints the decoded JWT
payload on failure so you can see exactly which claim is missing.

## Manual provisioning (if not using the JSON import)

1. **Clients → Create client** → client ID `tbe-b2c`. Standard flow only.
   Redirect URIs: `http://localhost:3000/*`. Save.
2. On `tbe-b2c` → **Client Scopes** → **Dedicated** → **Add mapper** →
   **By configuration** → **Audience**. Included client audience
   `tbe-api`. **Add to access token**. Save.
3. **Clients → Create client** → client ID `tbe-b2c-admin`. Disable
   Standard flow. Enable **Service accounts roles**. Save.
4. On `tbe-b2c-admin` → **Service Accounts Roles** → assign realm role
   `manage-users` from `realm-management`.
5. Copy both client secrets into `.env` / secret store.
6. Run `verify-audience-smoke.sh`.
