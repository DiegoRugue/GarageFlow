#!/usr/bin/env bash
set -Eeuo pipefail

if (( $# != 1 )); then
  echo "usage: smoke-container.sh <image>" >&2
  exit 2
fi

IMAGE="$1"
SUFFIX="$$"
NETWORK="garageflow-smoke-${SUFFIX}"
POSTGRES="garageflow-postgres-${SUFFIX}"
API="garageflow-api-${SUFFIX}"
WORK_DIR="$(mktemp -d "${TMPDIR:-/tmp}/garageflow-smoke.XXXXXX")"

SMOKE_DB_NAME="garageflow_smoke"
SMOKE_DB_USER="garageflow_smoke"
SMOKE_DB_PASSWORD="Synthetic.Db#2026"
SMOKE_ADMIN_EMAIL="smoke-admin@garageflow.local"
SMOKE_ADMIN_INITIAL_PASSWORD="Synthetic.Admin#123"
SMOKE_ADMIN_ACTIVE_PASSWORD="Synthetic.Admin#456"
SMOKE_JWT_KEY="synthetic-smoke-jwt-signing-key-2026-32chars"
SMOKE_WEBHOOK_SECRET="synthetic-smoke-webhook-secret-2026-32chars"
export SMOKE_DB_PASSWORD SMOKE_ADMIN_INITIAL_PASSWORD SMOKE_ADMIN_ACTIVE_PASSWORD
export SMOKE_JWT_KEY SMOKE_WEBHOOK_SECRET

cleanup() {
  docker rm -f "${API}" "${POSTGRES}" >/dev/null 2>&1 || true
  docker network rm "${NETWORK}" >/dev/null 2>&1 || true
  rm -rf "${WORK_DIR}"
}

sanitize_logs() {
  python3 -c '
import os, re, sys
text = sys.stdin.read()
for name in (
    "SMOKE_DB_PASSWORD",
    "SMOKE_ADMIN_INITIAL_PASSWORD",
    "SMOKE_ADMIN_ACTIVE_PASSWORD",
    "SMOKE_JWT_KEY",
    "SMOKE_WEBHOOK_SECRET",
):
    value = os.environ.get(name)
    if value:
        text = text.replace(value, "[REDACTED]")
text = re.sub(r"(?i)(password\s*[=:]\s*)[^;\s]+", r"\1[REDACTED]", text)
text = re.sub(r"(?i)(authorization:\s*bearer\s+)[A-Za-z0-9._~-]+", r"\1[REDACTED]", text)
text = re.sub(r"(?i)(\"token\"\s*:\s*\")[^\"]+", r"\1[REDACTED]", text)
sys.stderr.write(text)
'
}

print_logs() {
  echo "Smoke failed; sanitized container logs follow." >&2
  if docker inspect "${API}" >/dev/null 2>&1; then
    docker inspect --format 'API state: running={{.State.Running}} exitCode={{.State.ExitCode}} oomKilled={{.State.OOMKilled}}' "${API}" >&2 || true
    echo "--- API ---" >&2
    docker logs "${API}" 2>&1 | sanitize_logs || true
  fi
  if docker inspect "${POSTGRES}" >/dev/null 2>&1; then
    echo "--- PostgreSQL ---" >&2
    docker logs "${POSTGRES}" 2>&1 | sanitize_logs || true
  fi
}

on_error() {
  local status="$1"
  trap - ERR
  print_logs
  exit "${status}"
}

trap 'on_error "$?"' ERR
trap cleanup EXIT

fail() {
  echo "$1" >&2
  return 1
}

assert_status() {
  local expected="$1"
  local actual="$2"
  local description="$3"
  [[ "${actual}" == "${expected}" ]] || fail "${description}: expected HTTP ${expected}, received ${actual}."
}

request() {
  local method="$1"
  local url="$2"
  local body_file="$3"
  local response_file="$4"
  local headers_file="$5"
  local auth_config="${6:-}"
  local args=(
    --silent --show-error
    --connect-timeout 5 --max-time 20
    --request "${method}"
    --output "${response_file}"
    --dump-header "${headers_file}"
    --write-out '%{http_code}'
  )

  if [[ -n "${body_file}" ]]; then
    args+=(--header 'Content-Type: application/json' --data-binary "@${body_file}")
  fi
  if [[ -n "${auth_config}" ]]; then
    args+=(--config "${auth_config}")
  fi
  curl "${args[@]}" "${url}"
}

write_json() {
  local target="$1"
  local expression="$2"
  TARGET="${target}" EXPRESSION="${expression}" python3 - <<'PY'
import json, os
target = os.environ["TARGET"]
expression = os.environ["EXPRESSION"]
values = {
    "login-initial": {
        "email": os.environ["SMOKE_ADMIN_EMAIL"],
        "password": os.environ["SMOKE_ADMIN_INITIAL_PASSWORD"],
    },
    "change-password": {
        "currentPassword": os.environ["SMOKE_ADMIN_INITIAL_PASSWORD"],
        "newPassword": os.environ["SMOKE_ADMIN_ACTIVE_PASSWORD"],
    },
    "login-active": {
        "email": os.environ["SMOKE_ADMIN_EMAIL"],
        "password": os.environ["SMOKE_ADMIN_ACTIVE_PASSWORD"],
    },
    "intake": {
        "requestId": str(__import__("uuid").uuid4()),
        "customer": {
            "taxDocument": "52998224725",
            "fullName": "GarageFlow Smoke Customer",
            "email": "smoke-customer@garageflow.local",
            "phoneNumber": "+5511999999999",
        },
        "vehicle": {
            "plate": "SMK2E26",
            "year": 2026,
            "brand": "GarageFlow",
            "model": "Smoke",
            "color": "Black",
        },
        "services": [{"description": "Container smoke inspection", "price": 150.00}],
        "inventoryItems": [],
    },
}
with open(target, "w", encoding="utf-8") as stream:
    json.dump(values[expression], stream, separators=(",", ":"))
PY
  chmod 600 "${target}"
}

export SMOKE_ADMIN_EMAIL
docker network create "${NETWORK}" >/dev/null
docker run --detach --name "${POSTGRES}" --network "${NETWORK}" \
  --env "POSTGRES_DB=${SMOKE_DB_NAME}" \
  --env "POSTGRES_USER=${SMOKE_DB_USER}" \
  --env "POSTGRES_PASSWORD=${SMOKE_DB_PASSWORD}" \
  postgres:17-alpine >/dev/null

postgres_deadline=$((SECONDS + 60))
until docker exec "${POSTGRES}" pg_isready -U "${SMOKE_DB_USER}" -d "${SMOKE_DB_NAME}" >/dev/null 2>&1; do
  (( SECONDS < postgres_deadline )) || fail "PostgreSQL did not become ready within 60 seconds."
  sleep 1
done
echo "PostgreSQL ready."

common_env=(
  --env ASPNETCORE_ENVIRONMENT=Production
  --env Database__Provider=Postgres
  --env Database__AutoMigrate=false
  --env Database__AutoSeed=false
  --env "ConnectionStrings__GarageFlow=Host=${POSTGRES};Port=5432;Database=${SMOKE_DB_NAME};Username=${SMOKE_DB_USER};Password=${SMOKE_DB_PASSWORD}"
  --env Auth__Jwt__Issuer=GarageFlow.Smoke
  --env Auth__Jwt__Audience=GarageFlow.Smoke
  --env "Auth__Jwt__Key=${SMOKE_JWT_KEY}"
  --env Auth__Jwt__ExpiresMinutes=15
  --env 'Auth__BootstrapAdmin__FullName=GarageFlow Smoke Admin'
  --env "Auth__BootstrapAdmin__Email=${SMOKE_ADMIN_EMAIL}"
  --env Auth__BootstrapAdmin__BirthDate=1990-01-01
  --env "Auth__BootstrapAdmin__Password=${SMOKE_ADMIN_INITIAL_PASSWORD}"
  --env "Webhooks__EstimateDecisions__HmacSecret=${SMOKE_WEBHOOK_SECRET}"
  --env Integrations__Outbox__Enabled=false
  --env Integrations__Sns__Region=us-east-1
)

docker run --rm --network "${NETWORK}" "${common_env[@]}" "${IMAGE}" --migrate-only
echo "Database migration completed."

docker run --detach --name "${API}" --network "${NETWORK}" \
  --publish 127.0.0.1::8080 \
  "${common_env[@]}" \
  "${IMAGE}" >/dev/null

port_mapping="$(docker port "${API}" 8080/tcp)"
HOST_PORT="${port_mapping##*:}"
[[ "${HOST_PORT}" =~ ^[0-9]+$ ]] || fail "Could not determine the API host port."
BASE_URL="http://127.0.0.1:${HOST_PORT}"

ready_deadline=$((SECONDS + 120))
until curl --silent --connect-timeout 2 --max-time 5 --output /dev/null "${BASE_URL}/health/ready" 2>/dev/null; do
  if [[ "$(docker inspect --format '{{.State.Running}}' "${API}" 2>/dev/null || true)" != "true" ]]; then
    fail "API container stopped before becoming ready."
  fi
  (( SECONDS < ready_deadline )) || fail "API readiness did not succeed within 120 seconds."
  sleep 1
done
echo "API ready."

for endpoint in health health/live health/ready openapi/v1.json; do
  echo "Checking /${endpoint}."
  response="${WORK_DIR}/${endpoint//\//-}.response"
  headers="${response}.headers"
  status="$(request GET "${BASE_URL}/${endpoint}" '' "${response}" "${headers}")"
  assert_status 200 "${status}" "GET /${endpoint}"
done
python3 - "${WORK_DIR}/health.response" <<'PY'
import json, sys
with open(sys.argv[1], encoding="utf-8") as stream:
    payload = json.load(stream)
if payload != {"status": "ok"}:
    raise SystemExit("Unexpected /health payload.")
PY
python3 -m json.tool "${WORK_DIR}/openapi-v1.json.response" >/dev/null

write_json "${WORK_DIR}/login-initial.json" login-initial
status="$(request POST "${BASE_URL}/auth/login" "${WORK_DIR}/login-initial.json" "${WORK_DIR}/login-initial-response.json" "${WORK_DIR}/login-initial.headers")"
assert_status 200 "${status}" "Initial login"
INITIAL_TOKEN="$(python3 - "${WORK_DIR}/login-initial-response.json" <<'PY'
import json, sys
with open(sys.argv[1], encoding="utf-8") as stream:
    payload = json.load(stream)
if payload.get("mustChangePassword") is not True or not payload.get("token"):
    raise SystemExit("Initial login did not require a password change or return a token.")
print(payload["token"], end="")
PY
)"
printf 'header = "Authorization: Bearer %s"\n' "${INITIAL_TOKEN}" > "${WORK_DIR}/initial-auth.conf"
chmod 600 "${WORK_DIR}/initial-auth.conf"
unset INITIAL_TOKEN

write_json "${WORK_DIR}/change-password.json" change-password
status="$(request PUT "${BASE_URL}/users/me/password" "${WORK_DIR}/change-password.json" "${WORK_DIR}/change-password-response.json" "${WORK_DIR}/change-password.headers" "${WORK_DIR}/initial-auth.conf")"
assert_status 204 "${status}" "Mandatory password change"
[[ ! -s "${WORK_DIR}/change-password-response.json" ]] || fail "Password change returned an unexpected response body."
rm -f "${WORK_DIR}/initial-auth.conf"

write_json "${WORK_DIR}/login-active.json" login-active
status="$(request POST "${BASE_URL}/auth/login" "${WORK_DIR}/login-active.json" "${WORK_DIR}/login-active-response.json" "${WORK_DIR}/login-active.headers")"
assert_status 200 "${status}" "Active login"
ACTIVE_TOKEN="$(python3 - "${WORK_DIR}/login-active-response.json" <<'PY'
import json, sys
with open(sys.argv[1], encoding="utf-8") as stream:
    payload = json.load(stream)
if payload.get("mustChangePassword") is not False or not payload.get("token"):
    raise SystemExit("Active login returned an invalid contract.")
print(payload["token"], end="")
PY
)"
printf 'header = "Authorization: Bearer %s"\n' "${ACTIVE_TOKEN}" > "${WORK_DIR}/active-auth.conf"
chmod 600 "${WORK_DIR}/active-auth.conf"
unset ACTIVE_TOKEN

write_json "${WORK_DIR}/intake.json" intake
status="$(request POST "${BASE_URL}/work-orders/intake" "${WORK_DIR}/intake.json" "${WORK_DIR}/intake-response.json" "${WORK_DIR}/intake.headers" "${WORK_DIR}/active-auth.conf")"
assert_status 201 "${status}" "Complete intake"
WORK_ORDER_ID="$(python3 - "${WORK_DIR}/intake-response.json" "${WORK_DIR}/intake.headers" <<'PY'
import json, sys
with open(sys.argv[1], encoding="utf-8") as stream:
    payload = json.load(stream)
work_order_id = payload.get("workOrderId")
if not work_order_id or payload.get("status") != "Received":
    raise SystemExit("Intake response did not contain a Received work order.")
with open(sys.argv[2], encoding="iso-8859-1") as stream:
    headers = stream.read().splitlines()
location = next((line.split(":", 1)[1].strip() for line in headers if line.lower().startswith("location:")), None)
if location != f"/work-orders/{work_order_id}":
    raise SystemExit("Intake Location did not match the returned work-order ID.")
print(work_order_id, end="")
PY
)"

status="$(request GET "${BASE_URL}/work-orders/${WORK_ORDER_ID}/status" '' "${WORK_DIR}/status-response.json" "${WORK_DIR}/status.headers" "${WORK_DIR}/active-auth.conf")"
assert_status 200 "${status}" "Work-order status"
python3 - "${WORK_DIR}/status-response.json" "${WORK_ORDER_ID}" <<'PY'
import json, sys
with open(sys.argv[1], encoding="utf-8") as stream:
    payload = json.load(stream)
if payload.get("id") != sys.argv[2] or payload.get("status") != "Received" or not payload.get("updatedAt"):
    raise SystemExit("Work-order status response did not match the intake result.")
PY

echo "Container smoke passed."
