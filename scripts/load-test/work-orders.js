import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  stages: [
    { duration: '30s', target: 10 },
    { duration: '60s', target: 50 },
    { duration: '90s', target: 100 },
    { duration: '30s', target: 0 },
  ],
  thresholds: {
    http_req_failed: ['rate<0.02'],
    http_req_duration: ['p(95)<1000'],
  },
};

const activeStatuses = new Set([
  'Received',
  'Diagnosing',
  'WaitingApproval',
  'InProgress',
]);
const uuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
const originPattern = /^(https?):\/\/(\[[0-9a-f:.]+\]|[a-z0-9.-]+)(?::([0-9]{1,5}))?\/?$/i;

function requireEnvironment(name) {
  const value = __ENV[name];
  if (typeof value !== 'string' || value.trim().length === 0) {
    throw new Error(`Required environment variable ${name} is missing or blank.`);
  }

  return value;
}

function validateBaseUrl(value) {
  const candidate = value.trim();
  const match = originPattern.exec(candidate);
  if (!match) {
    throw new Error('BASE_URL must be an HTTP(S) origin without credentials, path, query, or fragment.');
  }

  if (match[3] && Number(match[3]) > 65535) {
    throw new Error('BASE_URL contains an invalid TCP port.');
  }

  return candidate.endsWith('/') ? candidate.slice(0, -1) : candidate;
}

const baseUrl = validateBaseUrl(requireEnvironment('BASE_URL'));
const staffEmail = requireEnvironment('STAFF_EMAIL');
const staffPassword = requireEnvironment('STAFF_PASSWORD');
const workOrderId = requireEnvironment('WORK_ORDER_ID').trim();

if (!uuidPattern.test(workOrderId)) {
  throw new Error('WORK_ORDER_ID must be a valid nonempty UUID.');
}

let token;

function parseJson(response) {
  try {
    return response.json();
  } catch (_) {
    return null;
  }
}

function login() {
  if (token) {
    return token;
  }

  const response = http.post(
    `${baseUrl}/auth/login`,
    JSON.stringify({ email: staffEmail, password: staffPassword }),
    {
      headers: { 'Content-Type': 'application/json' },
      tags: { endpoint: 'auth-login' },
    },
  );
  const payload = parseJson(response);
  const authenticated = check(response, {
    'login returns 200': (result) => result.status === 200,
    'login does not require password change': () => payload?.mustChangePassword === false,
    'login returns a token': () => typeof payload?.token === 'string' && payload.token.length > 0,
  });

  if (!authenticated) {
    throw new Error('Authentication failed; verify the local process environment and active staff account.');
  }

  token = payload.token;
  return token;
}

function authorizationParams() {
  return {
    headers: { Authorization: `Bearer ${login()}` },
  };
}

function isListSchema(payload) {
  return payload !== null
    && Array.isArray(payload.items)
    && Number.isInteger(payload.totalCount)
    && payload.totalCount >= 0
    && payload.page === 1
    && payload.pageSize === 20;
}

function isActiveStatusSchema(payload) {
  return payload !== null
    && typeof payload.id === 'string'
    && payload.id.toLowerCase() === workOrderId.toLowerCase()
    && activeStatuses.has(payload.status)
    && typeof payload.updatedAt === 'string'
    && !Number.isNaN(Date.parse(payload.updatedAt));
}

export function setup() {
  console.log(`GarageFlow bounded HPA load target: ${baseUrl}`);
  console.log('Stages: 30s→10, 60s→50, 90s→100, 30s→0; limits: failures<2%, p95<1000ms.');
  console.log('Synthetic-data demonstration only; no credentials, token, or personal data are logged.');
}

export default function () {
  const params = authorizationParams();
  const listResponse = http.get(
    `${baseUrl}/work-orders?page=1&pageSize=20`,
    { ...params, tags: { endpoint: 'work-orders-list' } },
  );
  const listPayload = parseJson(listResponse);
  check(listResponse, {
    'work-order list returns 200': (response) => response.status === 200,
    'work-order list schema is valid': () => isListSchema(listPayload),
  });

  const statusResponse = http.get(
    `${baseUrl}/work-orders/${workOrderId}/status`,
    { ...params, tags: { endpoint: 'work-order-status' } },
  );
  const statusPayload = parseJson(statusResponse);
  check(statusResponse, {
    'work-order status returns 200': (response) => response.status === 200,
    'work-order status is recognized and active': () => isActiveStatusSchema(statusPayload),
  });

  sleep(0.2 + (Math.random() * 0.3));
}
