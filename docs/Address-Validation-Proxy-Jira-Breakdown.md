# Address Validation Proxy Service — Jira Breakdown Structure

**Project:** Address Validation Proxy Service  
**Version:** 1.0  
**Date:** May 2026  
**Author:** Alex Khorozov  
**Status:** Draft  
**SRS Reference:** Address-Validation-Proxy-SRS.md v2.0

---

## 📋 Table of Contents

1. [Epic](#epic)
2. [Task 1 — Project Scaffold & Infrastructure Setup](#task-1--project-scaffold--infrastructure-setup)
3. [Task 2 — Core Domain Models](#task-2--core-domain-models)
4. [Task 3 — Infrastructure: Caching Layer](#task-3--infrastructure-caching-layer)
5. [Task 4 — Infrastructure: Provider Abstraction & Resilience](#task-4--infrastructure-provider-abstraction--resilience)
6. [Task 5 — Infrastructure: Event Sourcing & Audit](#task-5--infrastructure-event-sourcing--audit)
7. [Task 6 — Feature Slice: FR-001 Validate Single Address](#task-6--feature-slice-fr-001-validate-single-address)
8. [Task 7 — Feature Slice: FR-002 Validate Batch Addresses](#task-7--feature-slice-fr-002-validate-batch-addresses)
9. [Task 8 — Feature Slice: FR-003 Cache Management](#task-8--feature-slice-fr-003-cache-management)
10. [Task 9 — Feature Slice: FR-005 Health Checks](#task-9--feature-slice-fr-005-health-checks)
11. [Task 10 — Feature Slice: FR-006 Metrics Exposure](#task-10--feature-slice-fr-006-metrics-exposure)
12. [Task 11 — Security & API Gateway](#task-11--security--api-gateway)
13. [Task 12 — Observability & Monitoring](#task-12--observability--monitoring)
14. [Task 13 — Testing](#task-13--testing)
15. [Task 14 — Containerization & Deployment](#task-14--containerization--deployment)
16. [Task 15 — CosmosDB Capacity & Data Management](#task-15--cosmosdb-capacity--data-management)
17. [Summary](#summary)

---

## Epic

> **EPIC:** Address Validation Proxy Service — v1.0 Implementation
>
> `.NET 10 cloud-native microservice for US postal address validation, standardization & two-tier caching`

| Field        | Value                                                                 |
|--------------|-----------------------------------------------------------------------|
| **Type**     | Epic                                                                  |
| **Priority** | P1                                                                    |
| **Labels**   | `dotnet`, `azure`, `microservice`, `address-validation`, `caching`   |
| **SRS Ref**  | Address-Validation-Proxy-SRS.md v2.0                                 |

---

## Task 1 — Project Scaffold & Infrastructure Setup

> *VSA project structure, .NET Aspire host, DI wiring, configuration*

| Field        | Value                                        |
|--------------|----------------------------------------------|
| **Type**     | Task                                         |
| **Priority** | P1                                           |
| **Labels**   | `scaffold`, `aspire`, `configuration`, `di`  |
| **SRS Ref**  | Section 3.1.1, Section 2.4, Appendix A       |

### Subtasks

| #    | Subtask                                                                                  | Priority | SRS Ref         |
|------|------------------------------------------------------------------------------------------|----------|-----------------|
| 1.1  | Create solution structure per SRS Section 3.1.1 (VSA folders)                           | P1       | ADR-003         |
| 1.2  | Configure .NET Aspire AppHost with Redis + CosmosDB emulator                             | P1       | Section 2.4     |
| 1.3  | Wire `appsettings.json` with all Appendix A configuration keys                           | P1       | Appendix A      |
| 1.4  | Set up Azure Key Vault integration for secrets (Smarty, CosmosDB)                        | P1       | NFR-016         |
| 1.5  | Configure `dotnet user-secrets` for local development                                    | P1       | Appendix A      |
| 1.6  | Register all DI services in `ServiceCollectionExtensions.cs`                             | P1       | Section 3.1     |
| 1.7  | Configure `Asp.Versioning.Http` with `HeaderApiVersionReader("Api-Version")`             | P1       | ADR-001, Sec 3.2|
| 1.8  | Configure Serilog structured logging with OpenTelemetry enrichment                       | P1       | NFR-020         |
| 1.9  | Configure OpenTelemetry SDK (traces, metrics, logs)                                      | P1       | NFR-021         |
| 1.10 | Configure FluentValidation auto-registration                                             | P1       | Section 2.4     |

---

## Task 2 — Core Domain Models

> *Shared models, cache key strategy, address hashing*

| Field        | Value                                              |
|--------------|----------------------------------------------------|
| **Type**     | Task                                               |
| **Priority** | P1                                                 |
| **Labels**   | `domain-models`, `caching`, `hashing`              |
| **SRS Ref**  | Section 11.1, Section 3.3.1                        |

### Subtasks

| #   | Subtask                                                                            | Priority | SRS Ref          |
|-----|------------------------------------------------------------------------------------|----------|------------------|
| 2.1 | Implement `AddressInput` model with validation annotations                         | P1       | Section 11.1     |
| 2.2 | Implement `ValidatedAddress` model (USPS components)                               | P1       | Section 11.1     |
| 2.3 | Implement `AddressAnalysis` model (DPV fields)                                     | P1       | Section 11.1     |
| 2.4 | Implement `GeocodingResult` model                                                  | P1       | Section 11.1     |
| 2.5 | Implement `ValidationMetadata` model                                               | P1       | Section 11.1     |
| 2.6 | Implement `ValidationResponse` aggregate model                                     | P1       | Section 11.1     |
| 2.7 | Implement `AddressHashExtensions.ComputeHash()` (SHA-256, normalize + trim)        | P1       | Section 3.3.1    |
| 2.8 | Implement cache key format `addr:v{version}:{sha256}`                              | P1       | Section 3.3.1    |

---

## Task 3 — Infrastructure: Caching Layer

> *Redis L1, CosmosDB L2, CacheOrchestrator, TTL management*

| Field        | Value                                                        |
|--------------|--------------------------------------------------------------|
| **Type**     | Task                                                         |
| **Priority** | P1                                                           |
| **Labels**   | `redis`, `cosmosdb`, `caching`, `ttl`, `infrastructure`      |
| **SRS Ref**  | Section 3.3, Section 8.1, Section 8.3, ADR-004              |

### Subtasks

| #    | Subtask                                                                                         | Priority | SRS Ref               |
|------|-------------------------------------------------------------------------------------------------|----------|-----------------------|
| 3.1  | Implement `ICacheService<T>` interface (`GetAsync`, `SetAsync`, `RemoveAsync`)                  | P1       | Section 11.2          |
| 3.2  | Implement `RedisCacheService` with StackExchange.Redis                                          | P1       | Section 3.3, NFR-004  |
| 3.3  | Configure Redis Brotli compression + System.Text.Json source generators                         | P1       | Section 8.3           |
| 3.4  | Configure Redis TTL (3,600s), database 0, `allkeys-lru` eviction policy                        | P1       | Section 8.3           |
| 3.5  | Implement `CosmosCacheService` with CosmosDB SDK                                                | P1       | Section 3.3, NFR-005  |
| 3.6  | Configure CosmosDB `validated-addresses` container (partition `/stateCode`, TTL 90d)            | P1       | Section 8.1           |
| 3.7  | Configure CosmosDB indexing policy per Section 8.1.2                                            | P1       | Section 8.1.2         |
| 3.8  | Implement `CacheOrchestrator` — L1 → L2 → Provider lookup flow                                 | P1       | Section 3.3.2         |
| 3.9  | Implement cache write-through on provider response (L2 then L1)                                 | P1       | FR-001                |
| 3.10 | Implement cache warming on startup from recent `AddressValidated` events                        | P2       | Section 7.4           |

---

## Task 4 — Infrastructure: Provider Abstraction & Resilience

> *`IAddressValidationProvider`, SmartyProvider, Polly resilience pipeline*

| Field        | Value                                                               |
|--------------|---------------------------------------------------------------------|
| **Type**     | Task                                                                |
| **Priority** | P1                                                                  |
| **Labels**   | `smarty`, `polly`, `resilience`, `refit`, `provider`, `abstraction`|
| **SRS Ref**  | FR-007, Section 11.2, Appendix A, NFR-008, ADR-006                 |

### Subtasks

| #    | Subtask                                                                                        | Priority | SRS Ref              |
|------|------------------------------------------------------------------------------------------------|----------|----------------------|
| 4.1  | Define `IAddressValidationProvider` interface (`ValidateAsync`, `ValidateBatchAsync`)          | P1       | FR-007, Section 11.2 |
| 4.2  | Implement `ISmartyApi` Refit interface                                                         | P1       | Section 2.4          |
| 4.3  | Implement `SmartyProvider` with SmartyStreets SDK                                              | P1       | FR-001, FR-002       |
| 4.4  | Implement `MapToResponse(Candidate)` — map Smarty response to `ValidationResponse`            | P1       | Appendix D           |
| 4.5  | Configure Polly retry (3 attempts, 200ms exponential backoff)                                  | P1       | Appendix A, NFR-008  |
| 4.6  | Configure Polly circuit breaker (open after 5 failures, half-open after 30s)                  | P1       | NFR-008, Section 10.4|
| 4.7  | Configure Polly timeout (5s per request)                                                       | P1       | Appendix A           |
| 4.8  | Configure Polly bulkhead isolation (max 25 concurrent calls)                                   | P1       | Appendix A           |
| 4.9  | Implement `ResiliencePipelineConfig` composing all Polly policies                              | P1       | Section 3.1          |
| 4.10 | Implement feature toggle for runtime provider switching via `FeatureToggles:ProviderName`      | P2       | FR-007, NFR-025      |

---

## Task 5 — Infrastructure: Event Sourcing & Audit

> *Domain events, `IAuditEventStore`, CosmosDB audit container, Change Feed*

| Field        | Value                                                               |
|--------------|---------------------------------------------------------------------|
| **Type**     | Task                                                                |
| **Priority** | P1                                                                  |
| **Labels**   | `event-sourcing`, `audit`, `cosmosdb`, `domain-events`, `pii`      |
| **SRS Ref**  | FR-004, Section 7, Section 8.2, ADR-005                            |

### Subtasks

| #   | Subtask                                                                                          | Priority | SRS Ref              |
|-----|--------------------------------------------------------------------------------------------------|----------|----------------------|
| 5.1 | Define `DomainEvent` base class with all Section 7.3 schema fields                               | P1       | Section 7.3          |
| 5.2 | Implement all 8 domain event types (Section 7.2)                                                 | P1       | Section 7.2          |
| 5.3 | Define `IAuditEventStore` interface (`AppendAsync`, `AppendBatchAsync`, `QueryAsync`)            | P1       | Section 11.2         |
| 5.4 | Implement `CosmosAuditEventStore` (append-only writes, strong consistency)                       | P1       | FR-004, Section 7.1  |
| 5.5 | Configure CosmosDB `audit-events` container (partition `/requestDate`, TTL 365d)                 | P1       | Section 8.2          |
| 5.6 | Enable CosmosDB Change Feed on `audit-events` container                                          | P2       | Section 7.4, ADR-002 |
| 5.7 | Implement `GetAuditTrailQuery` handler with date/type/aggregateId filters                        | P2       | Section 6.2          |
| 5.8 | Ensure no raw PII stored — all address data SHA-256 hashed in events                             | P1       | FR-004, NFR-019      |

---

## Task 6 — Feature Slice: FR-001 Validate Single Address

> *`POST /api/addresses/validate` — full end-to-end vertical slice*

| Field        | Value                                                                    |
|--------------|--------------------------------------------------------------------------|
| **Type**     | Task                                                                     |
| **Priority** | P1                                                                       |
| **Labels**   | `feature-slice`, `validation`, `endpoint`, `cache`, `fr-001`            |
| **SRS Ref**  | FR-001, Section 9.3.1, Section 12.4                                     |

### Subtasks

| #   | Subtask                                                                                       | Priority | SRS Ref           |
|-----|-----------------------------------------------------------------------------------------------|----------|-------------------|
| 6.1 | Implement `ValidateSingleRequest` / `ValidateSingleResponse` models                           | P1       | FR-001            |
| 6.2 | Implement `ValidateSingleValidator` (street required, state 2-char, ZIP regex)                | P1       | FR-001, NFR-018   |
| 6.3 | Implement `ValidateSingleHandler.HandleAsync()` with full cache orchestration                 | P1       | FR-001, Sec 12.4  |
| 6.4 | Implement `ValidateSingleEndpoint` — `MapPost`, `[ApiVersion("1.0")]`, auth, rate limit       | P1       | FR-001, Sec 9.3.1 |
| 6.5 | Add `X-Cache-Source` response header (L1 / L2 / PROVIDER)                                    | P1       | FR-001 AC-4       |
| 6.6 | Add `X-Cache-Stale: true` header for circuit breaker fallback responses                       | P1       | NFR-007           |
| 6.7 | Return HTTP 404 for undeliverable addresses (DPV match code `N`)                              | P1       | FR-001            |
| 6.8 | Emit all relevant audit events per Section 7.2 on each request                               | P1       | FR-004            |

---

## Task 7 — Feature Slice: FR-002 Validate Batch Addresses

> *`POST /api/addresses/validate/batch` — parallel cache lookup, partial hits*

| Field        | Value                                                                         |
|--------------|-------------------------------------------------------------------------------|
| **Type**     | Task                                                                          |
| **Priority** | P1                                                                            |
| **Labels**   | `feature-slice`, `batch`, `validation`, `endpoint`, `fr-002`                 |
| **SRS Ref**  | FR-002, Section 9.3.2, Appendix D                                            |

### Subtasks

| #   | Subtask                                                                                          | Priority | SRS Ref           |
|-----|--------------------------------------------------------------------------------------------------|----------|-------------------|
| 7.1 | Implement `ValidateBatchRequest` / `ValidateBatchResponse` models with `inputIndex`              | P1       | FR-002            |
| 7.2 | Implement `ValidateBatchValidator` (max 100 addresses, each address valid)                       | P1       | FR-002, NFR-018   |
| 7.3 | Implement `ValidateBatchHandler` — parallel Redis MGET for all addresses                         | P1       | FR-002            |
| 7.4 | Implement CosmosDB batch lookup for Redis misses                                                  | P1       | FR-002            |
| 7.5 | Implement Smarty batch call for remaining misses (max 100 per SDK call)                          | P1       | FR-002, Appx D    |
| 7.6 | Implement result merge maintaining original `inputIndex` order                                   | P1       | FR-002 AC-2       |
| 7.7 | Implement HTTP 207 partial success for individual address failures                               | P1       | FR-002            |
| 7.8 | Implement `ValidateBatchEndpoint` — `MapPost`, `[ApiVersion("1.0")]`, auth, rate limit           | P1       | FR-002, Sec 9.3.2 |
| 7.9 | Include batch `summary` object (total, validated, failed, cacheHits, cacheMisses, duration)      | P1       | Section 9.3.2     |

---

## Task 8 — Feature Slice: FR-003 Cache Management

> *Cache stats, key invalidation, Redis flush — admin RBAC protected*

| Field        | Value                                                               |
|--------------|---------------------------------------------------------------------|
| **Type**     | Task                                                                |
| **Priority** | P2                                                                  |
| **Labels**   | `feature-slice`, `cache-management`, `rbac`, `admin`, `fr-003`     |
| **SRS Ref**  | FR-003, Section 9.3.3–9.3.5, NFR-017                              |

### Subtasks

| #   | Subtask                                                                                         | Priority | SRS Ref           |
|-----|-------------------------------------------------------------------------------------------------|----------|-------------------|
| 8.1 | Implement `CacheStatsHandler` aggregating Redis INFO + CosmosDB diagnostics                     | P2       | FR-003            |
| 8.2 | Implement `CacheStatsEndpoint` — `GET /api/cache/stats`                                         | P2       | FR-003, Sec 9.3.3 |
| 8.3 | Implement `InvalidateCacheEndpoint` — `DELETE /api/cache/{key}` with RBAC admin check           | P2       | FR-003, Sec 9.3.4 |
| 8.4 | Implement key invalidation — remove from Redis + mark `isStale: true` in CosmosDB              | P2       | FR-003, Sec 6.1   |
| 8.5 | Implement `FlushCacheEndpoint` — `DELETE /api/cache/flush` (Redis only, CosmosDB retained)      | P2       | FR-003, Sec 9.3.5 |
| 8.6 | Implement RBAC middleware for admin endpoint protection (return 403 for non-admin)               | P2       | FR-003, NFR-017   |
| 8.7 | Emit `CacheEntryInvalidated` and `CacheFlushed` domain events                                   | P2       | Section 7.2       |

---

## Task 9 — Feature Slice: FR-005 Health Checks

> *Liveness, readiness, startup probes — no auth required*

| Field        | Value                                                               |
|--------------|---------------------------------------------------------------------|
| **Type**     | Task                                                                |
| **Priority** | P1                                                                  |
| **Labels**   | `feature-slice`, `health-checks`, `probes`, `aca`, `fr-005`        |
| **SRS Ref**  | FR-005, Section 9.3.6                                              |

### Subtasks

| #   | Subtask                                                                                           | Priority | SRS Ref         |
|-----|---------------------------------------------------------------------------------------------------|----------|-----------------|
| 9.1 | Implement liveness probe `GET /health/live` (process alive check)                                 | P1       | FR-005          |
| 9.2 | Implement readiness probe `GET /health/ready` (Redis + CosmosDB + Smarty connectivity)            | P1       | FR-005 AC-2     |
| 9.3 | Implement startup probe `GET /health/startup` (config + dependency initialization)                | P1       | FR-005 AC-3     |
| 9.4 | Return structured response `{ status, checks: [{ name, status, duration, description }] }`       | P1       | FR-005          |
| 9.5 | Exclude health endpoints from API key authentication                                              | P1       | FR-005, Sec 9.2 |
| 9.6 | Return HTTP 503 when any dependency is unhealthy                                                  | P1       | FR-005 AC-2     |

---

## Task 10 — Feature Slice: FR-006 Metrics Exposure

> *Prometheus-compatible `/metrics` endpoint — 6 metric types*

| Field        | Value                                                                     |
|--------------|---------------------------------------------------------------------------|
| **Type**     | Task                                                                      |
| **Priority** | P2                                                                        |
| **Labels**   | `feature-slice`, `prometheus`, `metrics`, `opentelemetry`, `fr-006`      |
| **SRS Ref**  | FR-006, Section 9.3.6, NFR-001/002/022                                   |

### Subtasks

| #     | Subtask                                                                                          | Priority | SRS Ref              |
|-------|--------------------------------------------------------------------------------------------------|----------|----------------------|
| 10.1  | Configure `prometheus-net` with OpenTelemetry Prometheus exporter                               | P2       | FR-006               |
| 10.2  | Implement `address_validation_requests_total` counter (labels: endpoint, status, api_version)   | P2       | FR-006               |
| 10.3  | Implement `address_validation_duration_seconds` histogram (labels: endpoint, cache_source)      | P2       | FR-006, NFR-001/002  |
| 10.4  | Implement `cache_hit_ratio` gauge (labels: cache_tier)                                          | P2       | FR-006               |
| 10.5  | Implement `smarty_api_calls_total` counter (labels: status_code)                                | P2       | FR-006               |
| 10.6  | Implement `smarty_api_errors_total` counter (labels: error_type)                                | P2       | FR-006               |
| 10.7  | Implement `active_circuit_breakers` gauge (labels: provider)                                    | P2       | FR-006               |
| 10.8  | Expose `GET /metrics` endpoint, no auth, Prometheus text format                                 | P2       | FR-006, Sec 9.3.6    |
| 10.9  | Configure histogram buckets 10ms–5s range                                                       | P2       | FR-006 AC-3          |

---

## Task 11 — Security & API Gateway

> *API key auth, rate limiting, RFC 7807 error handling, TLS, input sanitization*

| Field        | Value                                                                           |
|--------------|---------------------------------------------------------------------------------|
| **Type**     | Task                                                                            |
| **Priority** | P1                                                                              |
| **Labels**   | `security`, `auth`, `rate-limiting`, `tls`, `rfc7807`, `middleware`            |
| **SRS Ref**  | NFR-014–019, Section 9.1–9.5                                                   |

### Subtasks

| #    | Subtask                                                                                            | Priority | SRS Ref          |
|------|----------------------------------------------------------------------------------------------------|----------|------------------|
| 11.1 | Implement `X-Api-Key` authentication middleware (return 401 if missing/invalid)                    | P1       | NFR-015, Sec 9.2 |
| 11.2 | Implement rate limiting middleware — 1,000 req/min per API key, fixed window                       | P1       | Section 9.5      |
| 11.3 | Return `Retry-After`, `X-RateLimit-Limit`, `X-RateLimit-Remaining`, `X-RateLimit-Reset` headers   | P1       | Section 9.5      |
| 11.4 | Implement RFC 7807 `ProblemDetails` error handler middleware                                       | P1       | Section 9.4      |
| 11.5 | Return HTTP 400 with RFC 7807 body when `Api-Version` header is missing                           | P1       | Section 9.1      |
| 11.6 | Enforce TLS 1.2+ for all inbound and outbound communications                                      | P1       | NFR-014          |
| 11.7 | Configure `Sunset` response header for deprecated API versions                                    | P2       | Section 3.2      |
| 11.8 | Implement PII sanitization — ensure no raw addresses in structured logs                           | P1       | NFR-019          |
| 11.9 | Implement `CorrelationId` middleware with OpenTelemetry propagation                               | P1       | NFR-020          |

---

## Task 12 — Observability & Monitoring

> *Distributed tracing, Grafana dashboards, alerting rules*

| Field        | Value                                                                      |
|--------------|----------------------------------------------------------------------------|
| **Type**     | Task                                                                       |
| **Priority** | P1/P2                                                                      |
| **Labels**   | `observability`, `opentelemetry`, `grafana`, `alerting`, `serilog`         |
| **SRS Ref**  | NFR-020–023, Section 5.5                                                   |

### Subtasks

| #    | Subtask                                                                                         | Priority | SRS Ref     |
|------|-------------------------------------------------------------------------------------------------|----------|-------------|
| 12.1 | Configure OpenTelemetry traces for Redis, CosmosDB, and Smarty API spans                       | P1       | NFR-021     |
| 12.2 | Configure Serilog with correlation ID enrichment on every log entry                            | P1       | NFR-020     |
| 12.3 | Create Grafana dashboard — request rate, latency percentiles, cache hit ratio, error rate      | P2       | NFR-022     |
| 12.4 | Create Grafana dashboard — circuit breaker state, CosmosDB RU consumption                     | P2       | NFR-022     |
| 12.5 | Configure alert: error rate > 1% over 5-minute window                                         | P2       | NFR-023     |
| 12.6 | Configure alert: p95 latency exceeds SLA targets (NFR-001/002/003)                            | P2       | NFR-023     |
| 12.7 | Configure alert: circuit breaker open state                                                    | P2       | NFR-023     |
| 12.8 | Configure alert: CosmosDB RU consumption > 80%                                                | P2       | NFR-023     |

---

## Task 13 — Testing

> *Full test pyramid — unit, integration, contract, load — Appendix C*

| Field        | Value                                                                              |
|--------------|------------------------------------------------------------------------------------|
| **Type**     | Task                                                                               |
| **Priority** | P1                                                                                 |
| **Labels**   | `testing`, `xunit`, `testcontainers`, `verify`, `k6`, `coverage`                  |
| **SRS Ref**  | Appendix C, NFR-024                                                               |

### Subtasks

| #     | Subtask                                                                                        | Priority | SRS Ref           |
|-------|------------------------------------------------------------------------------------------------|----------|-------------------|
| 13.1  | Set up xUnit test project with FluentAssertions + NSubstitute                                  | P1       | Appendix C        |
| 13.2  | Set up Testcontainers project (Redis + CosmosDB Emulator containers)                           | P1       | Appendix C        |
| 13.3  | Set up Verify snapshot testing project                                                         | P1       | Appendix C        |
| 13.4  | Write unit tests for `ValidateSingleHandler` (150–200 tests target)                            | P1       | Appendix C, FR-001|
| 13.5  | Write unit tests for `ValidateBatchHandler`                                                    | P1       | Appendix C, FR-002|
| 13.6  | Write unit tests for `CacheOrchestrator` — all L1/L2/Provider paths                           | P1       | Appendix C        |
| 13.7  | Write unit tests for `AddressHashExtensions` + all validators                                  | P1       | Appendix C        |
| 13.8  | Write unit tests for `SmartyProvider` response mapping                                        | P1       | Appendix C, Appx D|
| 13.9  | Write integration tests for full HTTP pipeline (WebApplicationFactory)                        | P1       | Appendix C        |
| 13.10 | Write integration tests for Redis cache flow (Testcontainers)                                 | P1       | Appendix C        |
| 13.11 | Write integration tests for CosmosDB cache flow (Testcontainers)                              | P1       | Appendix C        |
| 13.12 | Write integration tests for Polly resilience policies (retry, circuit breaker, timeout)       | P1       | Appendix C        |
| 13.13 | Write contract/snapshot tests for all API response schemas (Verify)                           | P1       | Appendix C        |
| 13.14 | Configure Coverlet + ReportGenerator — enforce ≥ 80% coverage gate in CI                     | P1       | NFR-024           |
| 13.15 | Write k6 load test scenarios (500 RPS, p95 latency targets, cache hit ratio)                  | P2       | Appendix C        |

---

## Task 14 — Containerization & Deployment

> *Docker, ACA, CI/CD pipeline — Appendix B*

| Field        | Value                                                                        |
|--------------|------------------------------------------------------------------------------|
| **Type**     | Task                                                                         |
| **Priority** | P1                                                                           |
| **Labels**   | `docker`, `aca`, `ci-cd`, `github-actions`, `acr`, `deployment`             |
| **SRS Ref**  | Appendix B, NFR-011/012/014                                                 |

### Subtasks

| #    | Subtask                                                                                        | Priority | SRS Ref           |
|------|------------------------------------------------------------------------------------------------|----------|-------------------|
| 14.1 | Create multi-stage Dockerfile (.NET 10 Linux runtime image)                                   | P1       | Appendix B.1      |
| 14.2 | Configure ACA with min 2 / max 10 replicas, HTTP concurrency + CPU scale triggers             | P1       | NFR-011, Appx B.1 |
| 14.3 | Configure ACA external HTTPS ingress with managed TLS certificate                             | P1       | NFR-014, Appx B.1 |
| 14.4 | Configure Azure Container Registry (ACR) with geo-replication                                 | P2       | Appendix B.1      |
| 14.5 | Enable Dapr sidecar on ACA for service invocation                                             | P2       | Appendix B.1      |
| 14.6 | Create 10-step GitHub Actions CI/CD pipeline (Appendix B.3)                                  | P1       | Appendix B.3      |
| 14.7 | Add manual approval gate for production ACA deployment (Step 10)                              | P1       | Appendix B.3      |
| 14.8 | Add smoke tests against health endpoints post-staging deploy (Step 9)                         | P1       | Appendix B.3      |
| 14.9 | Configure CosmosDB auto-scale 400–4,000 RU/s for production                                  | P1       | NFR-012, Appx E   |

---

## Task 15 — CosmosDB Capacity & Data Management

> *Capacity planning, partition analysis, TTL management — Appendix E*

| Field        | Value                                                               |
|--------------|---------------------------------------------------------------------|
| **Type**     | Task                                                                |
| **Priority** | P1/P2/P3                                                            |
| **Labels**   | `cosmosdb`, `capacity`, `partitioning`, `ttl`, `change-feed`       |
| **SRS Ref**  | Appendix E, Section 8.1/8.2, ADR-002                               |

### Subtasks

| #    | Subtask                                                                                       | Priority | SRS Ref           |
|------|-----------------------------------------------------------------------------------------------|----------|-------------------|
| 15.1 | Implement CosmosDB auto-scale throughput configuration (400–4,000 RU/s)                      | P1       | ADR-002, Appx E   |
| 15.2 | Validate partition key `/stateCode` distribution (50 states + DC)                            | P2       | Appendix E.4      |
| 15.3 | Verify 90-day TTL on `validated-addresses` and 365-day on `audit-events`                     | P1       | Section 8.1/8.2   |
| 15.4 | Implement CosmosDB Change Feed processor for downstream analytics                             | P2       | Section 7.4       |
| 15.5 | Create capacity planning runbook based on Appendix E estimates                               | P3       | Appendix E.2/E.3  |

---

## Summary

| Task | Name                                      | Subtasks | Priority  |
|------|-------------------------------------------|----------|-----------|
| T1   | Project Scaffold & Infrastructure Setup   | 10       | P1        |
| T2   | Core Domain Models                        | 8        | P1        |
| T3   | Infrastructure: Caching Layer             | 10       | P1/P2     |
| T4   | Infrastructure: Provider Abstraction      | 10       | P1/P2     |
| T5   | Infrastructure: Event Sourcing & Audit    | 8        | P1/P2     |
| T6   | FR-001 Validate Single Address            | 8        | P1        |
| T7   | FR-002 Validate Batch Addresses           | 9        | P1        |
| T8   | FR-003 Cache Management                   | 7        | P2        |
| T9   | FR-005 Health Checks                      | 6        | P1        |
| T10  | FR-006 Metrics Exposure                   | 9        | P2        |
| T11  | Security & API Gateway                    | 9        | P1/P2     |
| T12  | Observability & Monitoring                | 8        | P1/P2     |
| T13  | Testing                                   | 15       | P1/P2     |
| T14  | Containerization & Deployment             | 9        | P1/P2     |
| T15  | CosmosDB Capacity & Data Management       | 5        | P1/P2/P3  |
|      | **TOTAL**                                 | **131**  |           |

---

*Generated from Address-Validation-Proxy-SRS.md v2.0 — May 2026*
