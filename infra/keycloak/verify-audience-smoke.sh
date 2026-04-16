#!/usr/bin/env bash
# verify-audience-smoke.sh (W10)
#
# Real-token smoke for Phase 4 Wave 0 (Plan 04-00 Task 3).
#
# Uses the tbe-b2c-admin client_credentials grant to obtain an access
# token, then asserts that:
#   (a) the JWT payload contains aud=tbe-api (Pitfall 4), and
#   (b) the admin REST API accepts the token for send-verify-email
#       (Pitfall 8).
#
# Exits 0 on success. Prints the decoded payload on failure and exits
# with a non-zero status.

set -u -o pipefail

need_env() {
  local var="$1"
  if [ -z "${!var:-}" ]; then
    printf 'ERROR: %s is required but not set.\n' "$var" >&2
    printf '       Source .env or export it before running this script.\n' >&2
    exit 2
  fi
}

need_env KEYCLOAK_B2C_ISSUER
need_env KEYCLOAK_B2C_ADMIN_CLIENT_ID
need_env KEYCLOAK_B2C_ADMIN_CLIENT_SECRET

if ! command -v jq >/dev/null 2>&1; then
  printf 'ERROR: jq is required. Install with `apt install jq` or `brew install jq`.\n' >&2
  exit 2
fi

token_endpoint="${KEYCLOAK_B2C_ISSUER%/}/protocol/openid-connect/token"

# ---- Step 1: client_credentials grant --------------------------------------
response=$(curl -sS -f -X POST "$token_endpoint" \
  -u "$KEYCLOAK_B2C_ADMIN_CLIENT_ID:$KEYCLOAK_B2C_ADMIN_CLIENT_SECRET" \
  -d "grant_type=client_credentials" || true)

if [ -z "$response" ]; then
  printf 'FAIL: token endpoint returned empty response at %s\n' "$token_endpoint" >&2
  exit 1
fi

access_token=$(printf '%s' "$response" | jq -r '.access_token // empty')
if [ -z "$access_token" ]; then
  printf 'FAIL: no access_token in response:\n%s\n' "$response" >&2
  exit 1
fi

# ---- Step 2: decode JWT payload and assert aud=tbe-api ---------------------
payload_b64=$(printf '%s' "$access_token" | cut -d. -f2)
# JWT uses URL-safe base64 without padding; pad to a multiple of 4.
pad=$(( (4 - ${#payload_b64} % 4) % 4 ))
for _ in $(seq 1 $pad); do payload_b64="${payload_b64}="; done
payload_json=$(printf '%s' "$payload_b64" | tr '_-' '/+' | base64 -d 2>/dev/null || true)
if [ -z "$payload_json" ]; then
  printf 'FAIL: could not base64-decode JWT payload.\n' >&2
  exit 1
fi

aud_has_tbe_api=$(printf '%s' "$payload_json" \
  | jq -r '(if (.aud | type) == "array" then (.aud | index("tbe-api") != null) else (.aud == "tbe-api") end) | tostring')

if [ "$aud_has_tbe_api" != "true" ]; then
  printf 'FAIL: JWT aud does NOT contain "tbe-api" (Pitfall 4 not resolved).\n' >&2
  printf 'Decoded payload:\n' >&2
  printf '%s\n' "$payload_json" | jq . >&2
  exit 1
fi

# ---- Step 3: exercise send-verify-email on a canary user (Pitfall 8) ------
# KEYCLOAK_B2C_CANARY_USER_ID is optional — if not set, skip the admin
# call but still pass the audience check. CI pipelines set this to a
# known test user once the admin client is provisioned.
admin_step_status="skipped"
if [ -n "${KEYCLOAK_B2C_CANARY_USER_ID:-}" ]; then
  # Path: /admin/realms/{realm}/users/{id}/send-verify-email
  # KEYCLOAK_B2C_ISSUER is the realm URL; strip /realms/ to find the admin base.
  admin_base=$(printf '%s' "$KEYCLOAK_B2C_ISSUER" | sed -E 's|/realms/.*$||')
  realm=$(printf '%s' "$KEYCLOAK_B2C_ISSUER" | sed -E 's|.*/realms/([^/]+).*|\1|')
  admin_url="${admin_base}/admin/realms/${realm}/users/${KEYCLOAK_B2C_CANARY_USER_ID}/send-verify-email"
  http_status=$(curl -sS -o /dev/null -w '%{http_code}' \
    -X PUT "$admin_url" \
    -H "Authorization: Bearer $access_token" \
    -H 'Content-Length: 0' || printf '000')
  case "$http_status" in
    204|404)
      admin_step_status="$http_status (ok)"
      ;;
    *)
      printf 'FAIL: send-verify-email admin call returned HTTP %s (expected 204 or 404).\n' "$http_status" >&2
      printf '     URL: %s\n' "$admin_url" >&2
      exit 1
      ;;
  esac
fi

# ---- Summary ---------------------------------------------------------------
printf 'PASS: aud=tbe-api confirmed (Pitfall 4).\n'
printf 'PASS: send-verify-email admin step: %s (Pitfall 8).\n' "$admin_step_status"
printf 'Decoded payload:\n'
printf '%s\n' "$payload_json" | jq .
exit 0
