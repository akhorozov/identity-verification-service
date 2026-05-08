/**
 * Reusable check and trend helpers for k6 load test scripts.
 */

import { check }  from 'k6';
import { Trend, Rate, Counter } from 'k6/metrics';

// ── Custom metrics ────────────────────────────────────────────────────────────

/** Latency trend for single-address validation responses. */
export const validateSingleDuration = new Trend('validate_single_duration', true);

/** Latency trend for batch-address validation responses. */
export const validateBatchDuration = new Trend('validate_batch_duration', true);

/** Rate of HTTP 200 responses for single-validate. */
export const validateSingleSuccessRate = new Rate('validate_single_success_rate');

/** Rate of HTTP 200/207 responses for batch-validate. */
export const validateBatchSuccessRate = new Rate('validate_batch_success_rate');

/** Total validation errors (4xx/5xx). */
export const validationErrors = new Counter('validation_errors');

// ── Check helpers ─────────────────────────────────────────────────────────────

/**
 * Assert a single-validate response and record custom metrics.
 * @param {import('k6/http').RefinedResponse} res
 */
export function checkSingleValidate(res) {
  const ok = check(res, {
    'single validate status is 200': (r) => r.status === 200,
    'single validate has json body':  (r) => r.headers['Content-Type'] && r.headers['Content-Type'].includes('json'),
    'single validate body has address field': (r) => {
      try { return JSON.parse(r.body).address !== undefined; } catch { return false; }
    },
  });

  validateSingleDuration.add(res.timings.duration);
  validateSingleSuccessRate.add(ok);
  if (!ok) validationErrors.add(1);
}

/**
 * Assert a batch-validate response and record custom metrics.
 * @param {import('k6/http').RefinedResponse} res
 */
export function checkBatchValidate(res) {
  const ok = check(res, {
    'batch validate status is 200 or 207': (r) => r.status === 200 || r.status === 207,
    'batch validate has json body':         (r) => r.headers['Content-Type'] && r.headers['Content-Type'].includes('json'),
    'batch validate body is array':         (r) => {
      try { return Array.isArray(JSON.parse(r.body)); } catch { return false; }
    },
  });

  validateBatchDuration.add(res.timings.duration);
  validateBatchSuccessRate.add(ok);
  if (!ok) validationErrors.add(1);
}
