/**
 * k6 load test — POST /api/addresses/validate/batch
 *
 * Scenarios
 * ─────────
 *  smoke  : 1 VU × 30 s — reachability check
 *  ramp   : ramp from 5 → 50 concurrent VUs over 3 min, hold 5 min, ramp down
 *  stress : push to 100 VUs to find the breaking point
 *
 * Select a scenario at runtime:
 *   k6 run tests/k6/scenarios/validate-batch.js -e SCENARIO=smoke
 *   k6 run tests/k6/scenarios/validate-batch.js -e SCENARIO=ramp
 *   k6 run tests/k6/scenarios/validate-batch.js -e SCENARIO=stress
 *
 * Override target:
 *   k6 run tests/k6/scenarios/validate-batch.js -e BASE_URL=https://staging.example.com -e API_KEY=secret -e BATCH_SIZE=25
 *
 * SRS Ref: NFR-024 — p(95) < 2000 ms for batch, error rate < 1 %
 */

import http from 'k6/http';
import { sleep } from 'k6';
import { BASE_URL, defaultParams, THRESHOLDS } from '../helpers/config.js';
import { checkBatchValidate } from '../helpers/checks.js';

// ── Scenario definitions ───────────────────────────────────────────────────────

const SCENARIOS = {
  smoke: {
    executor: 'constant-vus',
    vus: 1,
    duration: '30s',
    tags: { scenario: 'smoke' },
  },
  ramp: {
    executor: 'ramping-vus',
    startVUs: 1,
    stages: [
      { duration: '2m', target: 20 },
      { duration: '3m', target: 50 },
      { duration: '5m', target: 50 },
      { duration: '1m', target: 0  },
    ],
    tags: { scenario: 'ramp' },
  },
  stress: {
    executor: 'ramping-vus',
    startVUs: 1,
    stages: [
      { duration: '2m', target: 50  },
      { duration: '3m', target: 100 },
      { duration: '5m', target: 100 },
      { duration: '2m', target: 0   },
    ],
    tags: { scenario: 'stress' },
  },
};

const ACTIVE_SCENARIO = __ENV.SCENARIO || 'ramp';

/** Number of addresses per batch — override with -e BATCH_SIZE=N (max 100). */
const BATCH_SIZE = Math.min(parseInt(__ENV.BATCH_SIZE || '10', 10), 100);

export const options = {
  scenarios: {
    [ACTIVE_SCENARIO]: SCENARIOS[ACTIVE_SCENARIO],
  },
  thresholds: {
    'validate_batch_duration{scenario:ramp}':   [`p(95)<${THRESHOLDS.batch.p95}`],
    'validate_batch_duration{scenario:stress}': [`p(95)<${THRESHOLDS.batch.p95}`],
    'validate_batch_success_rate':              ['rate>0.99'],
    'http_req_failed':                          ['rate<0.01'],
  },
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)'],
};

// ── Address pool ───────────────────────────────────────────────────────────────

const ADDRESS_POOL = [
  { street: '1600 Pennsylvania Ave NW', city: 'Washington',   state: 'DC', zipCode: '20500' },
  { street: '350 Fifth Ave',            city: 'New York',     state: 'NY', zipCode: '10118' },
  { street: '233 S Wacker Dr',          city: 'Chicago',      state: 'IL', zipCode: '60606' },
  { street: '1 Infinite Loop',          city: 'Cupertino',    state: 'CA', zipCode: '95014' },
  { street: '1600 Amphitheatre Pkwy',   city: 'Mountain View',state: 'CA', zipCode: '94043' },
  { street: '410 Terry Ave N',          city: 'Seattle',      state: 'WA', zipCode: '98109' },
  { street: '1 Microsoft Way',          city: 'Redmond',      state: 'WA', zipCode: '98052' },
  { street: '300 Crescent Ct',          city: 'Dallas',       state: 'TX', zipCode: '75201' },
  { street: '3500 Deer Creek Rd',       city: 'Palo Alto',    state: 'CA', zipCode: '94304' },
  { street: '77 W Wacker Dr',           city: 'Chicago',      state: 'IL', zipCode: '60601' },
  { street: '30 Rockefeller Plaza',     city: 'New York',     state: 'NY', zipCode: '10112' },
  { street: '1 Hacker Way',             city: 'Menlo Park',   state: 'CA', zipCode: '94025' },
  { street: '500 Oracle Pkwy',          city: 'Redwood Shores',state:'CA', zipCode: '94065' },
  { street: '100 Main St',              city: 'Boston',       state: 'MA', zipCode: '02129' },
  { street: '2000 Purchase St',         city: 'Purchase',     state: 'NY', zipCode: '10577' },
];

/** Build a random batch of `size` addresses drawn from the pool. */
function buildBatch(size) {
  const batch = [];
  for (let i = 0; i < size; i++) {
    batch.push(ADDRESS_POOL[Math.floor(Math.random() * ADDRESS_POOL.length)]);
  }
  return batch;
}

// ── VU code ────────────────────────────────────────────────────────────────────

export default function () {
  const payload = buildBatch(BATCH_SIZE);

  const res = http.post(
    `${BASE_URL}/api/addresses/validate/batch`,
    JSON.stringify(payload),
    defaultParams,
  );

  checkBatchValidate(res);

  sleep(0.5);
}
