/**
 * Shared k6 configuration.
 *
 * Override via environment variables when running against non-local targets:
 *   k6 run -e BASE_URL=https://staging.example.com -e API_KEY=secret ...
 */

export const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
export const API_KEY  = __ENV.API_KEY  || 'test-api-key';
export const API_VERSION = __ENV.API_VERSION || '1.0';

/** Common request params attached to every call. */
export const defaultParams = {
  headers: {
    'Content-Type':  'application/json',
    'Accept':        'application/json',
    'X-Api-Key':     API_KEY,
    'Api-Version':   API_VERSION,
  },
  timeout: '10s',
};

/**
 * P95 latency thresholds (ms) per SRS NFR-024.
 * - Validate single : p(95) < 500 ms
 * - Validate batch  : p(95) < 2000 ms
 */
export const THRESHOLDS = {
  single: { p95: 500 },
  batch:  { p95: 2000 },
};
