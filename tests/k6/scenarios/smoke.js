/**
 * k6 smoke test — quick sanity check across both endpoints.
 *
 * Runs 1 VU for 30 seconds against single-validate and batch-validate.
 * Use before any full load run to confirm the service is up and responding correctly.
 *
 * Usage:
 *   k6 run tests/k6/scenarios/smoke.js
 *   k6 run tests/k6/scenarios/smoke.js -e BASE_URL=https://staging.example.com -e API_KEY=secret
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { BASE_URL, defaultParams } from '../helpers/config.js';

export const options = {
  vus: 1,
  duration: '30s',
  thresholds: {
    http_req_failed:   ['rate<0.01'],
    http_req_duration: ['p(95)<1000'],
  },
};

const singlePayload = JSON.stringify({
  street: '1 Infinite Loop',
  city: 'Cupertino',
  state: 'CA',
  zipCode: '95014',
});

const batchPayload = JSON.stringify([
  { street: '1600 Pennsylvania Ave NW', city: 'Washington', state: 'DC', zipCode: '20500' },
  { street: '350 Fifth Ave',            city: 'New York',   state: 'NY', zipCode: '10118' },
  { street: '233 S Wacker Dr',          city: 'Chicago',    state: 'IL', zipCode: '60606' },
]);

export default function () {
  // Single validate
  const single = http.post(`${BASE_URL}/api/addresses/validate`, singlePayload, defaultParams);
  check(single, {
    'smoke: single status not 5xx': (r) => r.status < 500,
    'smoke: single has body':       (r) => r.body && r.body.length > 0,
  });

  sleep(0.5);

  // Batch validate
  const batch = http.post(`${BASE_URL}/api/addresses/validate/batch`, batchPayload, defaultParams);
  check(batch, {
    'smoke: batch status not 5xx': (r) => r.status < 500,
    'smoke: batch has body':       (r) => r.body && r.body.length > 0,
  });

  sleep(0.5);
}
