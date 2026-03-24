#!/usr/bin/env bash
# setup-paymongo.sh — Sets up PayMongo payment config on nexoflow demo tenants.
#
# Usage:
#   ./scripts/setup-paymongo.sh [BASE_URL]
#
# Defaults to https://chronith-api.azurewebsites.net if no URL provided.
# Requires: curl, jq
#
# Replace the REPLACE_ME placeholders with your actual PayMongo keys before running,
# or export them as environment variables:
#
#   export PAYMONGO_SECRET_KEY="sk_test_..."
#   export PAYMONGO_PUBLIC_KEY="pk_test_..."
#   export PAYMONGO_WEBHOOK_SECRET="whsec_..."
#   ./scripts/setup-paymongo.sh

set -euo pipefail

BASE_URL="${1:-https://chronith-api.azurewebsites.net}"
V1="$BASE_URL/v1"

# ─── PayMongo credentials (override via env vars) ────────────────────────────
SECRET_KEY="${PAYMONGO_SECRET_KEY:-sk_test_REPLACE_ME}"
PUBLIC_KEY="${PAYMONGO_PUBLIC_KEY:-pk_test_REPLACE_ME}"
WEBHOOK_SECRET="${PAYMONGO_WEBHOOK_SECRET:-whsec_REPLACE_ME}"

# ─── colours — all to stderr so stdout is clean ──────────────────────────────
GREEN='\033[0;32m'; YELLOW='\033[1;33m'; RED='\033[0;31m'; NC='\033[0m'
ok()   { echo -e "${GREEN}  ✓ $*${NC}" >&2; }
warn() { echo -e "${YELLOW}  ~ $*${NC}" >&2; }
fail() { echo -e "${RED}  ✗ $*${NC}" >&2; exit 1; }
hdr()  { echo -e "\n══════════════════════════════════════════\n $*\n══════════════════════════════════════════" >&2; }

# ─── helpers ─────────────────────────────────────────────────────────────────
login() {
  local slug="$1" email="$2" password="$3"

  local body
  body=$(jq -n --arg s "$slug" --arg e "$email" --arg p "$password" \
    '{"tenantSlug":$s,"email":$e,"password":$p}')

  local resp status json_body token
  resp=$(curl -s -w '\n%{http_code}' -X POST "$V1/auth/login" \
    -H "Content-Type: application/json" -d "$body")
  status=$(echo "$resp" | tail -1)
  json_body=$(echo "$resp" | sed '$d')

  [[ "$status" == "200" ]] || fail "Login failed for $email (HTTP $status): $json_body"
  ok "Logged in as $email"
  echo "$json_body" | jq -r '.accessToken'
}

create_payment_config() {
  local token="$1" label="$2" success_url="$3" failure_url="$4"

  local settings
  settings=$(jq -n -c \
    --arg sk "$SECRET_KEY" --arg pk "$PUBLIC_KEY" --arg ws "$WEBHOOK_SECRET" \
    --arg su "$success_url" --arg fu "$failure_url" \
    '{SecretKey:$sk,PublicKey:$pk,WebhookSecret:$ws,SuccessUrl:$su,FailureUrl:$fu}')

  local payload
  payload=$(jq -n \
    --arg pn "PayMongo" --arg l "$label" --arg s "$settings" \
    '{providerName:$pn,label:$l,settings:$s}')

  local resp status body
  resp=$(curl -s -w '\n%{http_code}' -X POST "$V1/tenant/payment-config" \
    -H "Authorization: Bearer $token" \
    -H "Content-Type: application/json" \
    -d "$payload")
  status=$(echo "$resp" | tail -1)
  body=$(echo "$resp" | sed '$d')

  if [[ "$status" == "201" ]]; then
    ok "Created PayMongo config: $label"
    echo "$body" | jq -r '.id'
  elif [[ "$status" == "409" ]] || [[ "$status" == "422" ]]; then
    warn "PayMongo config '$label' may already exist (HTTP $status)"
    echo ""
  else
    fail "Create payment config failed (HTTP $status): $body"
  fi
}

activate_config() {
  local token="$1" config_id="$2"

  if [[ -z "$config_id" ]]; then
    warn "No config ID to activate — skipping"
    return
  fi

  local resp status body
  resp=$(curl -s -w '\n%{http_code}' -X PATCH "$V1/tenant/payment-config/$config_id/activate" \
    -H "Authorization: Bearer $token")
  status=$(echo "$resp" | tail -1)
  body=$(echo "$resp" | sed '$d')

  if [[ "$status" == "200" || "$status" == "204" ]]; then
    ok "Activated config $config_id"
  else
    warn "Activate returned HTTP $status: $body"
  fi
}

list_configs() {
  local token="$1" tenant_name="$2"

  local resp
  resp=$(curl -s "$V1/tenant/payment-config" \
    -H "Authorization: Bearer $token")
  echo -e "\n  Payment configs for $tenant_name:" >&2
  echo "$resp" | jq '.' >&2
}

# ═══════════════════════════════════════════════════════════════════════════════
hdr "Setup PayMongo — Nexoflow Tenants"
echo -e "  Base URL: $BASE_URL" >&2
echo -e "  Keys:     ${SECRET_KEY:0:12}..." >&2

# ─── Nexoflow Automations ────────────────────────────────────────────────────
hdr "1/2  Nexoflow Automations"

AUTOMATIONS_TOKEN=$(login "nexoflow-automations" "admin@nexoflow-automations.demo" "Demo2026!")

AUTOMATIONS_CONFIG_ID=$(create_payment_config \
  "$AUTOMATIONS_TOKEN" \
  "PayMongo Test" \
  "https://booking.nexoflow.com/success" \
  "https://booking.nexoflow.com/failure")

activate_config "$AUTOMATIONS_TOKEN" "$AUTOMATIONS_CONFIG_ID"
list_configs "$AUTOMATIONS_TOKEN" "Nexoflow Automations"

# ─── Nexoflow Resort ─────────────────────────────────────────────────────────
hdr "2/2  Nexoflow Resort"

RESORT_TOKEN=$(login "nexoflow-resort" "admin@nexoflow-resort.demo" "Demo2026!")

RESORT_CONFIG_ID=$(create_payment_config \
  "$RESORT_TOKEN" \
  "PayMongo Test" \
  "https://resort.nexoflow.com/success" \
  "https://resort.nexoflow.com/failure")

activate_config "$RESORT_TOKEN" "$RESORT_CONFIG_ID"
list_configs "$RESORT_TOKEN" "Nexoflow Resort"

# ─── Done ────────────────────────────────────────────────────────────────────
hdr "Done"
ok "PayMongo configured on both nexoflow tenants"
