#!/usr/bin/env bash
# seed-demo.sh — Seeds two demo tenants on the live Chronith API.
#
# Usage:
#   ./scripts/seed-demo.sh [BASE_URL]
#
# Defaults to https://chronith-api.azurewebsites.net if no URL provided.
# Requires: curl, jq
#
# Idempotent: re-running skips already-registered tenants (409 → login instead).

set -euo pipefail

BASE_URL="${1:-https://chronith-api.azurewebsites.net}"
V1="$BASE_URL/v1"

# ─── colours — all to stderr so stdout is clean ──────────────────────────────
GREEN='\033[0;32m'; YELLOW='\033[1;33m'; RED='\033[0;31m'; NC='\033[0m'
ok()   { echo -e "${GREEN}  ✓ $*${NC}" >&2; }
warn() { echo -e "${YELLOW}  ~ $*${NC}" >&2; }
fail() { echo -e "${RED}  ✗ $*${NC}" >&2; exit 1; }
hdr()  { echo -e "\n══════════════════════════════════════════\n $*\n══════════════════════════════════════════" >&2; }
sec()  { echo -e "\n  ── $* ──" >&2; }

# ─── helpers ─────────────────────────────────────────────────────────────────
register_or_login() {
  local name="$1" slug="$2" tz="$3" email="$4" password="$5"

  local body
  body=$(jq -n \
    --arg n "$name" --arg s "$slug" --arg t "$tz" \
    --arg e "$email" --arg p "$password" \
    '{"tenantName":$n,"tenantSlug":$s,"timeZoneId":$t,"email":$e,"password":$p}')

  local resp status json_body token
  resp=$(curl -s -w '\n%{http_code}' -X POST "$V1/auth/register" \
    -H "Content-Type: application/json" -d "$body")
  status=$(echo "$resp" | tail -1)
  json_body=$(echo "$resp" | sed '$d')

  if [[ "$status" == "201" ]]; then
    ok "Registered tenant: $name"
    token=$(echo "$json_body" | jq -r '.accessToken')
  elif [[ "$status" == "409" || "$status" == "400" ]]; then
    warn "Tenant '$slug' already exists — logging in"
    local login_body
    login_body=$(jq -n --arg s "$slug" --arg e "$email" --arg p "$password" \
      '{"tenantSlug":$s,"email":$e,"password":$p}')
    resp=$(curl -s -w '\n%{http_code}' -X POST "$V1/auth/login" \
      -H "Content-Type: application/json" -d "$login_body")
    status=$(echo "$resp" | tail -1)
    json_body=$(echo "$resp" | sed '$d')
    [[ "$status" == "200" ]] || fail "Login failed for $email (HTTP $status): $json_body"
    ok "Logged in as $email"
    token=$(echo "$json_body" | jq -r '.accessToken')
  else
    fail "Register failed (HTTP $status): $json_body"
  fi

  # Only the token goes to stdout
  echo "$token"
}

create_booking_type() {
  local token="$1" payload="$2" label="$3"
  local resp status body
  resp=$(curl -s -w '\n%{http_code}' -X POST "$V1/booking-types" \
    -H "Authorization: Bearer $token" \
    -H "Content-Type: application/json" \
    -d "$payload")
  status=$(echo "$resp" | tail -1)
  body=$(echo "$resp" | sed '$d')
  if [[ "$status" == "201" ]]; then
    ok "Booking type: $label"
    echo "$body" | jq -r '.id'
  elif [[ "$status" == "409" ]] || { [[ "$status" == "422" ]] && echo "$body" | grep -qi "already exists"; }; then
    warn "Booking type '$label' already exists — fetching id"
    local slug; slug=$(echo "$payload" | jq -r '.slug')
    resp=$(curl -s -w '\n%{http_code}' -X GET "$V1/booking-types/$slug" \
      -H "Authorization: Bearer $token")
    status=$(echo "$resp" | tail -1)
    body=$(echo "$resp" | sed '$d')
    [[ "$status" == "200" ]] || fail "Could not fetch existing booking type '$slug'"
    echo "$body" | jq -r '.id'
  else
    fail "Create booking type '$label' failed (HTTP $status): $body"
  fi
}

create_staff() {
  local token="$1" payload="$2" label="$3"
  local resp status body
  resp=$(curl -s -w '\n%{http_code}' -X POST "$V1/staff" \
    -H "Authorization: Bearer $token" \
    -H "Content-Type: application/json" \
    -d "$payload")
  status=$(echo "$resp" | tail -1)
  body=$(echo "$resp" | sed '$d')
  if [[ "$status" == "201" ]]; then
    ok "Staff: $label"
    echo "$body" | jq -r '.id'
  elif [[ "$status" == "409" ]]; then
    warn "Staff '$label' already exists — skipping"
    echo ""
  else
    warn "Create staff '$label' returned HTTP $status — skipping"
    echo ""
  fi
}

create_booking() {
  local token="$1" bt_slug="$2" start_time="$3" customer_email="$4" label="$5"
  local payload
  payload=$(jq -n --arg s "$start_time" --arg e "$customer_email" \
    '{"startTime":$s,"customerEmail":$e}')
  local resp status body
  resp=$(curl -s -w '\n%{http_code}' -X POST "$V1/booking-types/$bt_slug/bookings" \
    -H "Authorization: Bearer $token" \
    -H "Content-Type: application/json" \
    -d "$payload")
  status=$(echo "$resp" | tail -1)
  body=$(echo "$resp" | sed '$d')
  if [[ "$status" == "201" ]]; then
    ok "Booking: $label"
    echo "$body" | jq -r '.id'
  else
    warn "Create booking '$label' failed (HTTP $status) — skipping"
    echo ""
  fi
}

pay_booking() {
  local token="$1" booking_id="$2" label="$3"
  [[ -z "$booking_id" ]] && { warn "No booking id for '$label' — skip pay"; return; }
  local resp status
  resp=$(curl -s -w '\n%{http_code}' -X POST "$V1/bookings/$booking_id/pay" \
    -H "Authorization: Bearer $token" \
    -H "Content-Type: application/json" \
    -d '{"paymentReference":"DEMO-REF"}')
  status=$(echo "$resp" | tail -1)
  [[ "$status" == "200" || "$status" == "204" ]] \
    && ok "Paid: $label" \
    || warn "Pay '$label' returned HTTP $status — may already be paid"
}

confirm_booking() {
  local token="$1" booking_id="$2" bt_slug="$3" label="$4"
  [[ -z "$booking_id" ]] && { warn "No booking id for '$label' — skip confirm"; return; }
  local resp status body
  resp=$(curl -s -w '\n%{http_code}' -X POST "$V1/bookings/$booking_id/confirm" \
    -H "Authorization: Bearer $token" \
    -H "Content-Type: application/json" \
    -d "$(jq -n --arg s "$bt_slug" '{"bookingTypeSlug":$s}')")
  status=$(echo "$resp" | tail -1)
  body=$(echo "$resp" | sed '$d')
  [[ "$status" == "200" || "$status" == "204" ]] \
    && ok "Confirmed: $label" \
    || warn "Confirm '$label' returned HTTP $status: $body"
}

cancel_booking() {
  local token="$1" booking_id="$2" bt_slug="$3" label="$4"
  [[ -z "$booking_id" ]] && { warn "No booking id for '$label' — skip cancel"; return; }
  local resp status body
  resp=$(curl -s -w '\n%{http_code}' -X POST "$V1/bookings/$booking_id/cancel" \
    -H "Authorization: Bearer $token" \
    -H "Content-Type: application/json" \
    -d "$(jq -n --arg s "$bt_slug" '{"bookingTypeSlug":$s}')")
  status=$(echo "$resp" | tail -1)
  body=$(echo "$resp" | sed '$d')
  [[ "$status" == "200" || "$status" == "204" ]] \
    && ok "Cancelled: $label" \
    || warn "Cancel '$label' returned HTTP $status: $body"
}

# ─── Shared availability windows (Mon–Fri 09:00–18:00) ────────────────────
WEEKDAY_WINDOWS='[
  {"dayOfWeek":1,"startTime":"09:00:00","endTime":"18:00:00"},
  {"dayOfWeek":2,"startTime":"09:00:00","endTime":"18:00:00"},
  {"dayOfWeek":3,"startTime":"09:00:00","endTime":"18:00:00"},
  {"dayOfWeek":4,"startTime":"09:00:00","endTime":"18:00:00"},
  {"dayOfWeek":5,"startTime":"09:00:00","endTime":"18:00:00"}
]'

# ════════════════════════════════════════════════════════════════════════════
hdr "1. NEXOFLOW AUTOMATIONS"

AUTO_TOKEN=$(register_or_login \
  "Nexoflow Automations" \
  "nexoflow-automations" \
  "Asia/Manila" \
  "admin@nexoflow-automations.demo" \
  "Demo2026!")

sec "Booking Types"

BT_DISCOVERY=$(create_booking_type "$AUTO_TOKEN" \
  "$(jq -n --argjson w "$WEEKDAY_WINDOWS" '{
    "slug": "discovery-call",
    "name": "Discovery Call",
    "isTimeSlot": true,
    "capacity": 1,
    "paymentMode": "Manual",
    "priceInCentavos": 250000,
    "currency": "PHP",
    "requiresStaffAssignment": true,
    "durationMinutes": 30,
    "bufferBeforeMinutes": 0,
    "bufferAfterMinutes": 5,
    "availabilityWindows": $w
  }')" \
  "Discovery Call (30 min)")

BT_CONSULT=$(create_booking_type "$AUTO_TOKEN" \
  "$(jq -n --argjson w "$WEEKDAY_WINDOWS" '{
    "slug": "consultation",
    "name": "Consultation",
    "isTimeSlot": true,
    "capacity": 1,
    "paymentMode": "Manual",
    "priceInCentavos": 350000,
    "currency": "PHP",
    "requiresStaffAssignment": true,
    "durationMinutes": 45,
    "bufferBeforeMinutes": 0,
    "bufferAfterMinutes": 5,
    "availabilityWindows": $w
  }')" \
  "Consultation (45 min)")

BT_WORKSHOP=$(create_booking_type "$AUTO_TOKEN" \
  "$(jq -n --argjson w "$WEEKDAY_WINDOWS" '{
    "slug": "implementation-workshop",
    "name": "Implementation Workshop",
    "isTimeSlot": true,
    "capacity": 8,
    "paymentMode": "Manual",
    "priceInCentavos": 500000,
    "currency": "PHP",
    "requiresStaffAssignment": false,
    "durationMinutes": 60,
    "bufferBeforeMinutes": 0,
    "bufferAfterMinutes": 10,
    "availabilityWindows": $w
  }')" \
  "Implementation Workshop (60 min)")

STRATEGY_WINDOWS='[
  {"dayOfWeek":1,"startTime":"09:00:00","endTime":"17:00:00"},
  {"dayOfWeek":2,"startTime":"09:00:00","endTime":"17:00:00"},
  {"dayOfWeek":3,"startTime":"09:00:00","endTime":"17:00:00"},
  {"dayOfWeek":4,"startTime":"09:00:00","endTime":"17:00:00"},
  {"dayOfWeek":5,"startTime":"09:00:00","endTime":"17:00:00"}
]'

BT_STRATEGY=$(create_booking_type "$AUTO_TOKEN" \
  "$(jq -n --argjson w "$STRATEGY_WINDOWS" '{
    "slug": "strategy-session",
    "name": "Strategy Session",
    "isTimeSlot": true,
    "capacity": 1,
    "paymentMode": "Manual",
    "priceInCentavos": 1500000,
    "currency": "PHP",
    "requiresStaffAssignment": true,
    "durationMinutes": 120,
    "bufferBeforeMinutes": 0,
    "bufferAfterMinutes": 15,
    "availabilityWindows": $w
  }')" \
  "Strategy Session (120 min)")

sec "Staff"

STAFF_WINDOWS_WEEKDAY='[
  {"dayOfWeek":1,"startTime":"09:00:00","endTime":"18:00:00"},
  {"dayOfWeek":2,"startTime":"09:00:00","endTime":"18:00:00"},
  {"dayOfWeek":3,"startTime":"09:00:00","endTime":"18:00:00"},
  {"dayOfWeek":4,"startTime":"09:00:00","endTime":"18:00:00"},
  {"dayOfWeek":5,"startTime":"09:00:00","endTime":"18:00:00"}
]'

create_staff "$AUTO_TOKEN" \
  "$(jq -n --argjson w "$STAFF_WINDOWS_WEEKDAY" \
    '{"name":"Alex Rivera","email":"alex@nexoflow-automations.demo","availabilityWindows":$w}')" \
  "Alex Rivera"

create_staff "$AUTO_TOKEN" \
  "$(jq -n --argjson w "$STAFF_WINDOWS_WEEKDAY" \
    '{"name":"Sam Cruz","email":"sam@nexoflow-automations.demo","availabilityWindows":$w}')" \
  "Sam Cruz"

create_staff "$AUTO_TOKEN" \
  "$(jq -n --argjson w "$STAFF_WINDOWS_WEEKDAY" \
    '{"name":"Jordan Lee","email":"jordan@nexoflow-automations.demo","availabilityWindows":$w}')" \
  "Jordan Lee"

sec "Sample Bookings"

# Bookings have priceInCentavos > 0 → start at PendingPayment.
# pay → PendingVerification → confirm/cancel for desired final status.
# Using April 2026 Mon–Fri dates.

B1=$(create_booking "$AUTO_TOKEN" "discovery-call"           "2026-04-07T10:00:00+08:00" "customer1@demo.com" "Apr 7 discovery-call → Confirmed")
B2=$(create_booking "$AUTO_TOKEN" "consultation"             "2026-04-08T14:00:00+08:00" "customer2@demo.com" "Apr 8 consultation → Confirmed")
B3=$(create_booking "$AUTO_TOKEN" "implementation-workshop"  "2026-04-09T09:00:00+08:00" "customer3@demo.com" "Apr 9 workshop → PendingVerification")
B4=$(create_booking "$AUTO_TOKEN" "strategy-session"         "2026-04-10T10:00:00+08:00" "customer1@demo.com" "Apr 10 strategy → PendingVerification")
B5=$(create_booking "$AUTO_TOKEN" "discovery-call"           "2026-04-07T15:00:00+08:00" "customer2@demo.com" "Apr 7 discovery-call #2 → Cancelled")

pay_booking     "$AUTO_TOKEN" "$B1" "Apr 7 discovery-call"
confirm_booking "$AUTO_TOKEN" "$B1" "discovery-call" "Apr 7 discovery-call"
pay_booking     "$AUTO_TOKEN" "$B2" "Apr 8 consultation"
confirm_booking "$AUTO_TOKEN" "$B2" "consultation" "Apr 8 consultation"
pay_booking     "$AUTO_TOKEN" "$B3" "Apr 9 workshop"
# B3 stays at PendingVerification — no confirm
pay_booking     "$AUTO_TOKEN" "$B4" "Apr 10 strategy"
# B4 stays at PendingVerification — no confirm
pay_booking     "$AUTO_TOKEN" "$B5" "Apr 7 discovery-call #2"
cancel_booking  "$AUTO_TOKEN" "$B5" "discovery-call" "Apr 7 discovery-call #2"

# ════════════════════════════════════════════════════════════════════════════
hdr "2. NEXOFLOW RESORT"

RESORT_TOKEN=$(register_or_login \
  "Nexoflow Resort" \
  "nexoflow-resort" \
  "Asia/Manila" \
  "admin@nexoflow-resort.demo" \
  "Demo2026!")

sec "Booking Types"

BT_OVERNIGHT=$(create_booking_type "$RESORT_TOKEN" \
  "$(jq -n '{
    "slug": "overnight-stay",
    "name": "Overnight Stay",
    "isTimeSlot": false,
    "capacity": 5,
    "paymentMode": "Manual",
    "priceInCentavos": 1500000,
    "currency": "PHP",
    "requiresStaffAssignment": false,
    "availableDays": [0, 1, 2, 3, 4, 5, 6]
  }')" \
  "Overnight Stay (Calendar)")

sec "Staff"

STAFF_WINDOWS_RESORT='[
  {"dayOfWeek":0,"startTime":"08:00:00","endTime":"20:00:00"},
  {"dayOfWeek":1,"startTime":"08:00:00","endTime":"20:00:00"},
  {"dayOfWeek":2,"startTime":"08:00:00","endTime":"20:00:00"},
  {"dayOfWeek":3,"startTime":"08:00:00","endTime":"20:00:00"},
  {"dayOfWeek":4,"startTime":"08:00:00","endTime":"20:00:00"},
  {"dayOfWeek":5,"startTime":"08:00:00","endTime":"20:00:00"},
  {"dayOfWeek":6,"startTime":"08:00:00","endTime":"20:00:00"}
]'

create_staff "$RESORT_TOKEN" \
  "$(jq -n --argjson w "$STAFF_WINDOWS_RESORT" \
    '{"name":"Maria Santos","email":"maria@nexoflow-resort.demo","availabilityWindows":$w}')" \
  "Maria Santos"

create_staff "$RESORT_TOKEN" \
  "$(jq -n --argjson w "$STAFF_WINDOWS_RESORT" \
    '{"name":"Carlo Reyes","email":"carlo@nexoflow-resort.demo","availabilityWindows":$w}')" \
  "Carlo Reyes"

sec "Sample Bookings"

# Calendar bookings: startTime is any time on the target day.
R1=$(create_booking "$RESORT_TOKEN" "overnight-stay" "2026-04-10T12:00:00+08:00" "guest1@demo.com" "Apr 10 overnight → Confirmed")
R2=$(create_booking "$RESORT_TOKEN" "overnight-stay" "2026-04-11T12:00:00+08:00" "guest2@demo.com" "Apr 11 overnight → Confirmed")
R3=$(create_booking "$RESORT_TOKEN" "overnight-stay" "2026-04-12T12:00:00+08:00" "guest1@demo.com" "Apr 12 overnight → PendingVerification")
R4=$(create_booking "$RESORT_TOKEN" "overnight-stay" "2026-04-13T12:00:00+08:00" "guest3@demo.com" "Apr 13 overnight → Cancelled")

pay_booking     "$RESORT_TOKEN" "$R1" "Apr 10 overnight"
confirm_booking "$RESORT_TOKEN" "$R1" "overnight-stay" "Apr 10 overnight"
pay_booking     "$RESORT_TOKEN" "$R2" "Apr 11 overnight"
confirm_booking "$RESORT_TOKEN" "$R2" "overnight-stay" "Apr 11 overnight"
pay_booking     "$RESORT_TOKEN" "$R3" "Apr 12 overnight"
# R3 stays at PendingVerification
pay_booking     "$RESORT_TOKEN" "$R4" "Apr 13 overnight"
cancel_booking  "$RESORT_TOKEN" "$R4" "overnight-stay" "Apr 13 overnight"

# ════════════════════════════════════════════════════════════════════════════
echo "" >&2
echo "══════════════════════════════════════════" >&2
echo " SEED COMPLETE" >&2
echo "══════════════════════════════════════════" >&2
echo "" >&2
echo "  Nexoflow Automations  → admin@nexoflow-automations.demo / Demo2026!" >&2
echo "  Nexoflow Resort       → admin@nexoflow-resort.demo / Demo2026!" >&2
echo "" >&2
echo "  Swagger UI: $BASE_URL/swagger" >&2
echo "" >&2
