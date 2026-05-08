/**
 * k6 load test — POST /api/addresses/validate (single address)
 *
 * Scenarios
 * ─────────
 *  smoke    : 1 VU × 30 s  — confirm the endpoint is reachable before a full run
 *  ramp     : ramp to 500 RPS over 2 min, hold 5 min, ramp down 1 min
 *  soak     : 200 RPS sustained for 30 min — catches slow memory / connection leaks
 *
 * Select a scenario at runtime:
 *   k6 run tests/k6/scenarios/validate-single.js -e SCENARIO=smoke
 *   k6 run tests/k6/scenarios/validate-single.js -e SCENARIO=ramp
 *   k6 run tests/k6/scenarios/validate-single.js -e SCENARIO=soak
 *
 * Override target:
 *   k6 run tests/k6/scenarios/validate-single.js -e BASE_URL=https://staging.example.com -e API_KEY=secret
 *
 * SRS Ref: NFR-024 — p(95) < 500 ms, error rate < 1 %
 */

import http from 'k6/http';
import { sleep } from 'k6';
import { BASE_URL, defaultParams, THRESHOLDS } from '../helpers/config.js';
import { checkSingleValidate } from '../helpers/checks.js';

// ── Scenario definitions ───────────────────────────────────────────────────────

const SCENARIOS = {
  smoke: {
    executor: 'constant-vus',
    vus: 1,
    duration: '30s',
    tags: { scenario: 'smoke' },
  },
  ramp: {
    executor: 'ramping-arrival-rate',
    startRate: 10,
    timeUnit: '1s',
    preAllocatedVUs: 100,
    maxVUs: 600,
    stages: [
      { duration: '2m', target: 500 },  // ramp to 500 RPS
      { duration: '5m', target: 500 },  // hold at 500 RPS
      { duration: '1m', target: 0   },  // ramp down
    ],
    tags: { scenario: 'ramp' },
  },
  soak: {
    executor: 'constant-arrival-rate',
    rate: 200,
    timeUnit: '1s',
    duration: '30m',
    preAllocatedVUs: 250,
    maxVUs: 400,
    tags: { scenario: 'soak' },
  },
};

const ACTIVE_SCENARIO = __ENV.SCENARIO || 'ramp';

export const options = {
  scenarios: {
    [ACTIVE_SCENARIO]: SCENARIOS[ACTIVE_SCENARIO],
  },
  thresholds: {
    // SRS NFR-024 targets
    'validate_single_duration{scenario:ramp}': [`p(95)<${THRESHOLDS.single.p95}`],
    'validate_single_duration{scenario:soak}': [`p(95)<${THRESHOLDS.single.p95}`],
    'validate_single_success_rate':            ['rate>0.99'],  // < 1 % error rate
    'http_req_failed':                         ['rate<0.01'],
  },
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)'],
};

// ── Realistic address pool ─────────────────────────────────────────────────────

const ADDRESSES = [
  { street: '1600 Pennsylvania Ave NW', city: 'Washington', state: 'DC', zipCode: '20500' },
  { street: '350 Fifth Ave',            city: 'New York',   state: 'NY', zipCode: '10118' },
  { street: '233 S Wacker Dr',          city: 'Chicago',    state: 'IL', zipCode: '60606' },
  { street: '1 Infinite Loop',          city: 'Cupertino',  state: 'CA', zipCode: '95014' },
  { street: '1600 Amphitheatre Pkwy',   city: 'Mountain View', state: 'CA', zipCode: '94043' },
  { street: '410 Terry Ave N',          city: 'Seattle',    state: 'WA', zipCode: '98109' },
  { street: '1 Microsoft Way',          city: 'Redmond',    state: 'WA', zipCode: '98052' },
  { street: '300 Crescent Ct',          city: 'Dallas',     state: 'TX', zipCode: '75201' },
  { street: '3500 Deer Creek Rd',       city: 'Palo Alto',  state: 'CA', zipCode: '94304' },
  { street: '77 W Wacker Dr',           city: 'Chicago',    state: 'IL', zipCode: '60601' },
];

// ── VU code ────────────────────────────────────────────────────────────────────

export default function () {
  const addr = ADDRESSES[Math.floor(Math.random() * ADDRESSES.length)];

  const res = http.post(
    `${BASE_URL}/api/addresses/validate`,
    JSON.stringify(addr),
    defaultParams,
  );

  checkSingleValidate(res);

  // Minimal think time — keeps arrival-rate executors in control of throughput
  sleep(0.1);
}
