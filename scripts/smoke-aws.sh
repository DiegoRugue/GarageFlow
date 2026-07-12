#!/usr/bin/env bash
set -Eeuo pipefail
umask 077

fail() {
  echo "AWS smoke failed: $1" >&2
  exit 1
}

for name in BASE_URL BOOTSTRAP_EMAIL BOOTSTRAP_INITIAL_PASSWORD BOOTSTRAP_ACTIVE_PASSWORD; do
  [[ -n "${!name:-}" ]] || fail "required environment variable ${name} is missing."
done

[[ "${BASE_URL}" =~ ^https?://[^/[:space:]]+$ ]] || fail 'BASE_URL must be an HTTP(S) origin without a path.'
BASE_URL="${BASE_URL%/}"

WORK_DIR="$(mktemp -d "${RUNNER_TEMP:-${TMPDIR:-/tmp}}/garageflow-aws-smoke.XXXXXX")"
cleanup() {
  rm -rf -- "${WORK_DIR}"
}
trap cleanup EXIT

create_private_file() {
  local target="$1"
  : > "${target}"
  chmod 600 "${target}"
}

assert_status() {
  local expected="$1"
  local actual="$2"
  local description="$3"
  [[ "${actual}" == "${expected}" ]] || fail "${description}: expected HTTP ${expected}, received ${actual}."
}

request() {
  local method="$1"
  local path="$2"
  local body_file="$3"
  local response_file="$4"
  local headers_file="$5"
  local auth_config="${6:-}"
  create_private_file "${response_file}"
  create_private_file "${headers_file}"
  local args=(
    --silent --show-error
    --connect-timeout 5 --max-time 20
    --request "${method}"
    --output "${response_file}"
    --dump-header "${headers_file}"
    --write-out '%{http_code}'
  )
  [[ -z "${body_file}" ]] || args+=(--header 'Content-Type: application/json' --data-binary "@${body_file}")
  [[ -z "${auth_config}" ]] || args+=(--config "${auth_config}")
  curl "${args[@]}" "${BASE_URL}${path}"
}

write_login_body() {
  local target="$1"
  local password="$2"
  create_private_file "${target}"
  TARGET="${target}" PASSWORD="${password}" python3 - <<'PY'
import json, os
with open(os.environ["TARGET"], "w", encoding="utf-8") as stream:
    json.dump(
        {"email": os.environ["BOOTSTRAP_EMAIL"], "password": os.environ["PASSWORD"]},
        stream,
        separators=(",", ":"),
    )
PY
}

extract_login_token() {
  local response_file="$1"
  local expected_change="$2"
  python3 - "${response_file}" "${expected_change}" <<'PY'
import json, sys
with open(sys.argv[1], encoding="utf-8") as stream:
    payload = json.load(stream)
expected = sys.argv[2].lower() == "true"
if payload.get("mustChangePassword") is not expected or not payload.get("token"):
    raise SystemExit("Login response did not match the expected password-change contract.")
print(payload["token"], end="")
PY
}

write_auth_config() {
  local target="$1"
  local token="$2"
  create_private_file "${target}"
  printf 'header = "Authorization: Bearer %s"\n' "${token}" > "${target}"
}

ready_deadline=$((SECONDS + 180))
while true; do
  ready_status="$(curl --silent --connect-timeout 3 --max-time 10 \
    --output /dev/null --write-out '%{http_code}' "${BASE_URL}/health/ready" 2>/dev/null || true)"
  [[ "${ready_status}" == '200' ]] && break
  (( SECONDS < ready_deadline )) || fail '/health/ready did not return HTTP 200 within 180 seconds.'
  sleep 5
done

for endpoint in /health /health/live /health/ready /openapi/v1.json; do
  safe_name="${endpoint//\//-}"
  status="$(request GET "${endpoint}" '' \
    "${WORK_DIR}/${safe_name}.response" "${WORK_DIR}/${safe_name}.headers")"
  assert_status 200 "${status}" "GET ${endpoint}"
done

python3 - "${WORK_DIR}/-health.response" <<'PY'
import json, sys
with open(sys.argv[1], encoding="utf-8") as stream:
    payload = json.load(stream)
if payload != {"status": "ok"}:
    raise SystemExit("Unexpected /health response contract.")
PY
python3 -m json.tool "${WORK_DIR}/-openapi-v1.json.response" >/dev/null
echo 'Health, liveness, readiness, and OpenAPI checks passed.'

# Try the active password first so a successful prior deployment can be smoked again.
write_login_body "${WORK_DIR}/login-active.json" "${BOOTSTRAP_ACTIVE_PASSWORD}"
active_status="$(request POST /auth/login "${WORK_DIR}/login-active.json" \
  "${WORK_DIR}/login-active.response" "${WORK_DIR}/login-active.headers")"

if [[ "${active_status}" == '200' ]]; then
  active_token="$(extract_login_token "${WORK_DIR}/login-active.response" false)"
elif [[ "${active_status}" == '401' ]]; then
  write_login_body "${WORK_DIR}/login-initial.json" "${BOOTSTRAP_INITIAL_PASSWORD}"
  initial_status="$(request POST /auth/login "${WORK_DIR}/login-initial.json" \
    "${WORK_DIR}/login-initial.response" "${WORK_DIR}/login-initial.headers")"
  assert_status 200 "${initial_status}" 'Initial bootstrap login after active-password 401'
  initial_token="$(extract_login_token "${WORK_DIR}/login-initial.response" true)"
  write_auth_config "${WORK_DIR}/initial-auth.conf" "${initial_token}"
  unset initial_token

  create_private_file "${WORK_DIR}/change-password.json"
  TARGET="${WORK_DIR}/change-password.json" python3 - <<'PY'
import json, os
with open(os.environ["TARGET"], "w", encoding="utf-8") as stream:
    json.dump(
        {
            "currentPassword": os.environ["BOOTSTRAP_INITIAL_PASSWORD"],
            "newPassword": os.environ["BOOTSTRAP_ACTIVE_PASSWORD"],
        },
        stream,
        separators=(",", ":"),
    )
PY
  change_status="$(request PUT /users/me/password "${WORK_DIR}/change-password.json" \
    "${WORK_DIR}/change-password.response" "${WORK_DIR}/change-password.headers" \
    "${WORK_DIR}/initial-auth.conf")"
  assert_status 204 "${change_status}" 'Mandatory bootstrap password change'
  [[ ! -s "${WORK_DIR}/change-password.response" ]] || fail 'password change returned an unexpected response body.'
  rm -f -- "${WORK_DIR}/initial-auth.conf"

  write_login_body "${WORK_DIR}/login-active-again.json" "${BOOTSTRAP_ACTIVE_PASSWORD}"
  relogin_status="$(request POST /auth/login "${WORK_DIR}/login-active-again.json" \
    "${WORK_DIR}/login-active-again.response" "${WORK_DIR}/login-active-again.headers")"
  assert_status 200 "${relogin_status}" 'Active login after mandatory password change'
  active_token="$(extract_login_token "${WORK_DIR}/login-active-again.response" false)"
else
  fail "active bootstrap login returned HTTP ${active_status}; initial-password fallback is allowed only after HTTP 401."
fi

write_auth_config "${WORK_DIR}/active-auth.conf" "${active_token}"
unset active_token
echo 'Rerun-safe bootstrap authentication passed.'

create_private_file "${WORK_DIR}/intake.json"
TARGET="${WORK_DIR}/intake.json" python3 - <<'PY'
import json, os, secrets, string, uuid

def fresh_cpf():
    digits = [secrets.randbelow(10) for _ in range(9)]
    if len(set(digits)) == 1:
        digits[-1] = (digits[-1] + 1) % 10
    for length in (9, 10):
        weight = length + 1
        total = sum(value * (weight - index) for index, value in enumerate(digits))
        remainder = (total * 10) % 11
        digits.append(0 if remainder == 10 else remainder)
    return "".join(str(value) for value in digits)

request_id = uuid.uuid4()
suffix = request_id.hex[:10]
plate = (
    "".join(secrets.choice(string.ascii_uppercase) for _ in range(3))
    + str(secrets.randbelow(10))
    + secrets.choice(string.ascii_uppercase)
    + f"{secrets.randbelow(100):02d}"
)
payload = {
    "requestId": str(request_id),
    "customer": {
        "taxDocument": fresh_cpf(),
        "fullName": "GarageFlow AWS Smoke Customer",
        "email": f"smoke-{suffix}@garageflow.local",
        "phoneNumber": "+55119" + f"{secrets.randbelow(100000000):08d}",
    },
    "vehicle": {
        "plate": plate,
        "year": 2026,
        "brand": "GarageFlow",
        "model": "AWS Smoke",
        "color": "Blue",
    },
    "services": [{"description": "AWS smoke inspection", "price": 150.00}],
    "inventoryItems": [],
}
with open(os.environ["TARGET"], "w", encoding="utf-8") as stream:
    json.dump(payload, stream, separators=(",", ":"))
PY

intake_status="$(request POST /work-orders/intake "${WORK_DIR}/intake.json" \
  "${WORK_DIR}/intake.response" "${WORK_DIR}/intake.headers" \
  "${WORK_DIR}/active-auth.conf")"
assert_status 201 "${intake_status}" 'Complete synthetic work-order intake'

work_order_id="$(python3 - "${WORK_DIR}/intake.response" "${WORK_DIR}/intake.headers" <<'PY'
import json, sys
with open(sys.argv[1], encoding="utf-8") as stream:
    payload = json.load(stream)
work_order_id = payload.get("workOrderId")
if not work_order_id or payload.get("status") != "Received":
    raise SystemExit("Intake did not return a Received work order.")
with open(sys.argv[2], encoding="iso-8859-1") as stream:
    headers = stream.read().splitlines()
location = next((line.split(":", 1)[1].strip() for line in headers if line.lower().startswith("location:")), None)
if location != f"/work-orders/{work_order_id}":
    raise SystemExit("Intake Location header does not match the work-order ID.")
print(work_order_id, end="")
PY
)"

status_code="$(request GET "/work-orders/${work_order_id}/status" '' \
  "${WORK_DIR}/status.response" "${WORK_DIR}/status.headers" \
  "${WORK_DIR}/active-auth.conf")"
assert_status 200 "${status_code}" 'Synthetic work-order status'
python3 - "${WORK_DIR}/status.response" "${work_order_id}" <<'PY'
import json, sys
with open(sys.argv[1], encoding="utf-8") as stream:
    payload = json.load(stream)
if payload.get("id") != sys.argv[2] or payload.get("status") != "Received" or not payload.get("updatedAt"):
    raise SystemExit("Work-order status response does not match the intake result.")
PY

echo "AWS smoke passed; created work-order ID: ${work_order_id}"
