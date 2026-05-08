# k6 Load Tests

Performance and load tests for the Address Validation API.  
**SRS Ref:** NFR-024 — p(95) < 500 ms (single), p(95) < 2000 ms (batch), error rate < 1 %

---

## Prerequisites

Install [k6](https://k6.io/docs/getting-started/installation/):

```bash
# Windows (winget)
winget install k6 --source winget

# macOS
brew install k6

# Docker
docker pull grafana/k6
```

---

## Directory layout

```
tests/k6/
├── helpers/
│   ├── config.js     # Base URL, API key, thresholds
│   └── checks.js     # Custom Trend/Rate/Counter metrics + check helpers
└── scenarios/
    ├── smoke.js            # 1 VU × 30 s — sanity check both endpoints
    ├── validate-single.js  # Single-address validation load scenarios
    └── validate-batch.js   # Batch-address validation load scenarios
```

---

## Environment variables

| Variable     | Default                  | Description                        |
|--------------|--------------------------|------------------------------------|
| `BASE_URL`   | `http://localhost:5000`  | Target API base URL                |
| `API_KEY`    | `test-api-key`           | Value sent in `X-Api-Key` header   |
| `API_VERSION`| `1.0`                    | Value sent in `Api-Version` header |
| `SCENARIO`   | `ramp` / `ramp`          | Active scenario name (see below)   |
| `BATCH_SIZE` | `10`                     | Addresses per batch request (max 100) |

---

## Running tests

### 1. Smoke test (always run first)

Verifies the service is reachable and both endpoints respond before a full run.

```bash
k6 run tests/k6/scenarios/smoke.js
# Against staging:
k6 run tests/k6/scenarios/smoke.js -e BASE_URL=https://staging.example.com -e API_KEY=<key>
```

---

### 2. Single-address validation

#### Ramp to 500 RPS (default)

Ramps from 10 → 500 requests/sec over 2 min, holds for 5 min, ramps down.

```bash
k6 run tests/k6/scenarios/validate-single.js
# Explicit:
k6 run tests/k6/scenarios/validate-single.js -e SCENARIO=ramp
```

#### Smoke (quick pre-check)

```bash
k6 run tests/k6/scenarios/validate-single.js -e SCENARIO=smoke
```

#### Soak (30-min sustained @ 200 RPS)

Detects slow memory or connection leaks under sustained load.

```bash
k6 run tests/k6/scenarios/validate-single.js -e SCENARIO=soak
```

---

### 3. Batch-address validation

#### Ramp (default — 50 concurrent VUs)

```bash
k6 run tests/k6/scenarios/validate-batch.js
# With a larger batch size:
k6 run tests/k6/scenarios/validate-batch.js -e BATCH_SIZE=25
```

#### Stress (100 concurrent VUs)

```bash
k6 run tests/k6/scenarios/validate-batch.js -e SCENARIO=stress
```

---

## Thresholds

Tests **fail** (`exit 1`) if any threshold is breached:

| Metric | Threshold | Scenario |
|---|---|---|
| `validate_single_duration p(95)` | < 500 ms | ramp, soak |
| `validate_batch_duration p(95)`  | < 2000 ms | ramp, stress |
| `validate_single_success_rate`   | > 99 % | all |
| `validate_batch_success_rate`    | > 99 % | all |
| `http_req_failed`                | < 1 % | all |

---

## CI integration

Run the smoke test as a post-deploy gate (no external infrastructure needed):

```yaml
# .github/workflows example step
- name: k6 smoke test
  run: k6 run tests/k6/scenarios/smoke.js -e BASE_URL=${{ env.API_URL }} -e API_KEY=${{ secrets.API_KEY }}
```

For the full ramp scenario, use a dedicated performance environment and the `grafana/k6` Docker image:

```yaml
- name: k6 load test (500 RPS)
  run: |
    docker run --rm -v ${{ github.workspace }}:/tests \
      grafana/k6 run /tests/tests/k6/scenarios/validate-single.js \
      -e BASE_URL=${{ env.API_URL }} -e API_KEY=${{ secrets.API_KEY }} -e SCENARIO=ramp
```

---

## Output & reporting

k6 prints a summary to stdout. For HTML/JSON reports:

```bash
# JSON output (machine-readable)
k6 run tests/k6/scenarios/validate-single.js --out json=results.json

# Grafana Cloud k6 (streaming)
k6 run tests/k6/scenarios/validate-single.js -o cloud
```
