#!/usr/bin/env bash
# Phase 5 Plan 05-00 Task 3 — Keycloak audience smoke for tbe-b2b-admin SA.
#
# Mints an access token via client_credentials against the tbe-b2b realm and
# asserts the aud claim equals 'tbe-api'. Guards Pitfall 4 for B2B at Plan 05-01
# gateway cutover (ValidateAudience flip from false to true on the B2B JWT scheme).
#
# Exit codes:
#   0  aud=tbe-api verified (smoke passed)
#   1  aud mismatch — realm / client / mapper misconfigured (Pitfall 4 not resolved)
#   2  required env vars unset (KEYCLOAK_B2B_ISSUER / KEYCLOAK_B2B_ADMIN_CLIENT_ID / KEYCLOAK_B2B_ADMIN_CLIENT_SECRET)
#
# Usage:  ./verify-audience-smoke-b2b.sh
#
# The script is fail-closed: if any of the 3 required env vars is unset it
# exits 2 BEFORE attempting any network I/O (mitigation T-05-00-07 — silent
# pass would let a misconfigured CI reach the gateway cutover).

set -u -o pipefail

need_env() {
  local var="$1"
  if [ -z "${!var:-}" ]; then
    printf 'ERROR: %s is required but not set.\n' "$var" >&2
    printf '       Source .env or export it before running this script.\n' >&2
    exit 2
  fi
}

need_env KEYCLOAK_B2B_ISSUER
need_env KEYCLOAK_B2B_ADMIN_CLIENT_ID
need_env KEYCLOAK_B2B_ADMIN_CLIENT_SECRET

if ! command -v jq >/dev/null 2>&1; then
  printf 'ERROR: jq is required. Install with `apt install jq` or `brew install jq`.\n' >&2
  exit 2
fi

token_endpoint="${KEYCLOAK_B2B_ISSUER%/}/protocol/openid-connect/token"

# ---- Step 1: client_credentials grant --------------------------------------
response=$(curl -sS -f -X POST "$token_endpoint" \
  -u "$KEYCLOAK_B2B_ADMIN_CLIENT_ID:$KEYCLOAK_B2B_ADMIN_CLIENT_SECRET" \
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

# ---- Summary ---------------------------------------------------------------
printf 'PASS: aud=tbe-api confirmed (Pitfall 4 resolved for tbe-b2b).\n'
printf 'Decoded payload:\n'
printf '%s\n' "$payload_json" | jq .
exit 0
