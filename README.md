# Address Validation Proxy Service

A cloud-native .NET 10 microservice that acts as an intelligent proxy between internal consumers and the [Smarty US Street API](https://www.smarty.com/docs/cloud/us-street-api). It provides US address validation, USPS standardization, geocoding enrichment, and two-tier caching to minimize vendor API costs while maintaining sub-second response times.

> **Classification:** Internal — Confidential  
> **Version:** 2.0 | May 6, 2026  
> **Author:** Alex Khorozov

---

## Table of Contents

- [Overview](#overview)
- [Features](#features)
- [Architecture](#architecture)
- [Technology Stack](#technology-stack)
- [API Reference](#api-reference)
- [Caching Strategy](#caching-strategy)
- [Getting Started](#getting-started)
- [Configuration](#configuration)
- [Deployment](#deployment)
- [Testing](#testing)
- [Observability](#observability)

---

## Overview

The Address Validation Proxy Service receives address validation requests from internal API consumers, checks a two-tier cache (Redis → Azure Cosmos DB) for previously validated addresses, and only calls the Smarty US Street API on a cache miss. Validated results are persisted in Cosmos DB and hot-cached in Redis, achieving an estimated **85–95% cache hit rate**.

### Consumers

- E-Commerce Checkout
- CRM System
- Shipping Module
- Blazor Frontend (Operations Team)

---

## Features

| Feature | Description |
|---------|-------------|
| **Single Address Validation** | Validate one US address with CASS certification, DPV analysis, and geocoding |
| **Batch Address Validation** | Validate up to 100 addresses per request with partial cache-hit optimization |
| **Two-Tier Caching** | Redis (L1, 1h TTL) + Azure Cosmos DB (L2, 90-day TTL) |
| **Audit Logging** | Append-only event sourcing in CosmosDB with 365-day retention |
| **Cache Management** | Stats, per-key invalidation, and Redis flush endpoints |
| **Health Checks** | Liveness, readiness, and startup probes |
| **Prometheus Metrics** | Request rate, latency percentiles, cache hit ratio, circuit breaker state |
| **Provider Abstraction** | Vendor-agnostic `IAddressValidationProvider` interface for future provider swaps |
| **Resilience** | Polly v8 retry, circuit breaker (opens after 5 failures), timeout (5s), bulkhead (25 concurrent) |
| **Header-Based API Versioning** | `Api-Version` header required; `AssumeDefaultVersionWhenUnspecified = false` |

---

## Architecture

The service uses **Vertical Slice Architecture** with the following key components:

### Solution Structure

```
AddressValidation/
├── AddressValidation.Api                 # Core validation service
├── AddressValidation.Gateway             # YARP reverse proxy (NEW)
├── AddressValidation.AppHost             # Aspire orchestrator
├── AddressValidation.ServiceDefaults     # Shared infrastructure
├── AddressValidation.Tests.Unit          # Unit tests
├── AddressValidation.Tests.Integration   # Integration tests
└── Directory.Packages.props              # Central Package Management (NEW)
```

### Key Architectural Patterns

- **Vertical Slice Architecture (VSA)**: Each feature organized as an isolated slice
- **YARP Gateway**: Reverse proxy for traffic routing and load balancing
- **Central Package Management (CPM)**: Centralized NuGet versioning
- **Aspire Orchestration**: Local development with Redis and CosmosDB emulator
- **Distributed Tracing**: OpenTelemetry integration for observability

### Gateway Layer

The **AddressValidation.Gateway** project routes all requests through YARP:

```
Client → Gateway (YARP) → API Service
         ├── Security Headers
         ├── CORS Handling
         ├── Health Checks
         └── Request Correlation
```

See [ARCHITECTURE.md](./docs/ARCHITECTURE.md) for detailed architecture documentation.

The service uses **Vertical Slice Architecture (VSA)** — each feature is a self-contained folder with its own endpoint, handler, validator, and request/response models. Shared infrastructure (caching, providers, events) lives in a dedicated `Infrastructure` namespace.

### Project Structure

```
src/AddressValidation.Api/
├── Features/
│   ├── ValidateSingle/          # Endpoint, Handler, Validator, Request/Response
│   ├── ValidateBatch/           # Endpoint, Handler, Validator, Request/Response
│   ├── CacheManagement/         # Stats, Invalidate, Flush endpoints
│   └── HealthCheck/             # Liveness, Readiness handlers
├── Infrastructure/
│   ├── Caching/                 # ICacheService, RedisCacheService, CosmosCacheService, CacheOrchestrator
│   ├── Providers/               # IAddressValidationProvider, SmartyProvider, ISmartyApi (Refit)
│   ├── Resilience/              # PollyPolicies, ResiliencePipelineConfig
│   ├── Versioning/              # ApiVersioningConfig
│   └── Events/                  # IAuditEventStore, CosmosAuditEventStore, DomainEvent
├── Shared/
│   ├── Models/                  # AddressInput, ValidatedAddress, AddressAnalysis, GeocodingResult, ValidationMetadata
│   └── Extensions/              # ServiceCollectionExtensions, AddressHashExtensions
├── Program.cs
└── appsettings.json
```

### CQRS Flow

The service applies a **CQRS-lite** pattern:

- **Command Path (Write):** `ValidateAddressCommand` → validate input → cache lookup → provider call → write to CosmosDB + Redis → emit audit event
- **Query Path (Read):** `GetCachedAddressQuery` / `GetCacheStatsQuery` / `GetAuditTrailQuery` — read-only, no side effects

---

## Technology Stack

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 10, C# 13 |
| Framework | ASP.NET Core Minimal API |
| Gateway | YARP (Yet Another Reverse Proxy) |
| Package Management | Central Package Management (CPM) via Directory.Packages.props |
| Orchestration | .NET Aspire |
| HTTP Client | Refit |
| Persistent Cache | Azure Cosmos DB (NoSQL, SQL API) |
| Hot Cache | Redis (StackExchange.Redis) |
| Resilience | Polly v8 |
| Validation | FluentValidation |
| Observability | OpenTelemetry, Prometheus, Grafana, Serilog |
| Deployment (Prod) | Azure Container Apps (ACA) |
| Deployment (Non-Prod) | .NET Aspire local orchestration |
| API Versioning | `Asp.Versioning.Http` (`HeaderApiVersionReader`) |
| CI/CD | Azure DevOps Pipelines (YAML multi-stage) |
| Testing | xUnit, FluentAssertions, NSubstitute, Testcontainers, Verify |

---

## API Reference

All endpoints require the `Api-Version` header. Omitting it returns `HTTP 400`. Consumer-facing endpoints also require `X-Api-Key`.

### Versioning

```http
Api-Version: 1.0
```

> Omitting this header returns `HTTP 400` — no default version is assumed.

### Authentication

```http
X-Api-Key: {your-api-key}
```

Health check (`/health/*`) and metrics (`/metrics`) endpoints do **not** require authentication.

### Endpoints

#### `POST /api/addresses/validate`

Validate a single US address.

**Request:**
```json
{
  "street": "1600 Amphitheatre Pkwy",
  "city": "Mountain View",
  "state": "CA",
  "zipCode": "94043",
  "addressee": "Google LLC"
}
```

**Response (200 OK):**
```json
{
  "validatedAddress": { "deliveryLine1": "...", "zipCode": "94043", "plus4Code": "1351", "..." : "..." },
  "analysis": { "dpvMatchCode": "Y", "dpvFootnotes": "AABB", "dpvVacant": "N", "..." : "..." },
  "geocoding": { "latitude": 37.4224, "longitude": -122.0842, "precision": "Zip9" },
  "metadata": { "providerName": "Smarty", "cacheSource": "PROVIDER", "apiVersion": "1.0", "requestDurationMs": 342 }
}
```

Response header: `X-Cache-Source: L1 | L2 | PROVIDER`

**Rate limit:** 1,000 requests/minute per API key.

---

#### `POST /api/addresses/validate/batch`

Validate up to 100 addresses. Only cache misses are forwarded to Smarty. Input order is preserved in the response.

**Request:**
```json
{
  "addresses": [
    { "street": "1600 Amphitheatre Pkwy", "city": "Mountain View", "state": "CA", "zipCode": "94043" },
    { "street": "1 Microsoft Way", "city": "Redmond", "state": "WA", "zipCode": "98052" }
  ]
}
```

**Response (200 OK):** Array of `ValidationResponse` objects each with `inputIndex`, plus a `summary` block with totals and cache hit/miss counts.

---

#### Cache Management

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/api/cache/stats` | GET | Any key | Redis + CosmosDB hit/miss ratios and entry counts |
| `/api/cache/{key}` | DELETE | Admin role | Invalidate a specific cache entry in both tiers |
| `/api/cache/flush` | DELETE | Admin role | Flush Redis (L1) only; CosmosDB data retained |

---

#### Health & Metrics

| Endpoint | Description |
|----------|-------------|
| `GET /health/live` | Liveness probe — is the process alive? |
| `GET /health/ready` | Readiness probe — are Redis, CosmosDB, and Smarty reachable? |
| `GET /health/startup` | Startup probe — have all dependencies initialized? |
| `GET /metrics` | Prometheus-compatible metrics exposition |

---

### Error Responses

All errors follow [RFC 7807 Problem Details](https://tools.ietf.org/html/rfc7807):

```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Validation Failed",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "instance": "/api/addresses/validate",
  "errors": { "street": ["Street is required."] },
  "traceId": "00-abc123def456789-0123456789abcdef-01"
}
```

---

## Caching Strategy

| Tier | Technology | TTL | Latency Target | Purpose |
|------|-----------|-----|----------------|---------|
| **L1 — Hot** | Redis | 1 hour | < 5ms p99 | Sub-millisecond reads for frequently accessed addresses |
| **L2 — Persistent** | Azure Cosmos DB | 90 days | < 15ms p99 | Durable store surviving Redis evictions and restarts |

**Cache key format:** `addr:v{apiVersion}:{sha256_hash}` — normalized input (lowercase, trimmed, punctuation removed), hashed with SHA-256, prefixed with the API version to prevent cross-version contamination.

**Lookup flow:** L1 (Redis) → L2 (CosmosDB) → Smarty API. On any miss, the result is written to both tiers.

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [.NET Aspire workload](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/setup-tooling): `dotnet workload install aspire`
- Docker Desktop (for containerized Redis and CosmosDB Emulator)

### Local Development

```bash
# Clone the repository
git clone <repo-url>
cd address-validation-proxy

# Configure user secrets (never commit sensitive values)
dotnet user-secrets set "Smarty:AuthId" "<your-smarty-auth-id>"
dotnet user-secrets set "Smarty:AuthToken" "<your-smarty-auth-token>"

# Run via .NET Aspire (starts Redis, CosmosDB Emulator, and the API)
dotnet run --project src/AddressValidation.AppHost
```

The Aspire Developer Dashboard opens automatically for service monitoring.

---

## Configuration

> ⚠️ **Never** store `Smarty:AuthId`, `Smarty:AuthToken`, or `CosmosDb:Key` in `appsettings.json` or source control. Use Azure Key Vault (production) or `dotnet user-secrets` (local development).

| Setting | Default | Description |
|---------|---------|-------------|
| `Smarty:AuthId` | — | Smarty API authentication ID (Key Vault) |
| `Smarty:AuthToken` | — | Smarty API authentication token (Key Vault) |
| `Smarty:BaseUrl` | `https://us-street.api.smarty.com` | Smarty US Street API base URL |
| `Smarty:MatchMode` | `enhanced` | `strict` / `invalid` / `enhanced` |
| `Smarty:License` | `us-rooftop-geocoding-cloud` | Smarty license for geocoding precision |
| `Redis:ConnectionString` | — | Redis connection string |
| `Redis:TtlSeconds` | `3600` | Redis cache TTL (1 hour) |
| `CosmosDb:Endpoint` | — | Cosmos DB account endpoint |
| `CosmosDb:Key` | — | Cosmos DB account key (Key Vault) |
| `CosmosDb:DatabaseName` | `address-validation` | Database name |
| `CosmosDb:AddressContainer` | `validated-addresses` | Container for validated addresses |
| `CosmosDb:AuditContainer` | `audit-events` | Container for audit events |
| `CosmosDb:TtlSeconds` | `7776000` | Address cache TTL (90 days) |
| `CosmosDb:AuditTtlSeconds` | `31536000` | Audit event TTL (365 days) |
| `RateLimit:RequestsPerMinute` | `1000` | Max requests per minute per API key |
| `Resilience:RetryCount` | `3` | Smarty API retry attempts |
| `Resilience:CircuitBreakerThreshold` | `5` | Consecutive failures before circuit opens |
| `Resilience:CircuitBreakerDurationSec` | `30` | Duration the circuit stays open |
| `Resilience:TimeoutSeconds` | `5` | Per-request timeout for Smarty API calls |
| `Resilience:BulkheadMaxParallelism` | `25` | Max concurrent Smarty API calls |
| `FeatureToggles:ProviderName` | `Smarty` | Active provider (for provider switching) |
| `FeatureToggles:EnableL1Cache` | `true` | Enable/disable Redis L1 cache |
| `FeatureToggles:EnableAuditEvents` | `true` | Enable/disable audit event emission |

---

## Deployment

### Production (Azure Container Apps)

| Property | Value |
|----------|-------|
| Platform | Azure Container Apps (ACA) |
| Min / Max Replicas | 2 / 10 (auto-scale) |
| Scale Triggers | HTTP concurrency > 50 per replica; CPU > 70% |
| Ingress | External HTTPS with managed TLS |
| Container Registry | Azure Container Registry (ACR) |
| Region | East US (primary) |

### CI/CD Pipeline (Azure DevOps)

The pipeline is defined in `.azure-pipelines/azure-pipelines.yml` and triggered on every push to `main`.

```
Push to main
  → Build (.NET 10 restore + build)
  → Test (xUnit unit + Testcontainers integration)
  → Quality (Coverlet ≥ 80% coverage + Verify snapshots)
  → Package (Docker multi-stage build → push to ACR)
  → Deploy: Staging (ACA + smoke tests)
  → [Manual Approval Gate]
  → Deploy: Production (ACA)
```

---

## Testing

| Type | Tools | Target |
|------|-------|--------|
| Unit | xUnit, FluentAssertions, NSubstitute | Handler logic, validation rules, hash computation, response mapping |
| Integration | Testcontainers, WebApplicationFactory | Full HTTP pipeline with real containerized Redis + CosmosDB Emulator |
| Contract | Verify (snapshot testing) | API response schema — prevents breaking changes |
| Load | k6 | 500 RPS sustained, p95 latency targets, cache hit ratio under load (against staging) |
| Security | OWASP ZAP, manual review | Auth bypass, input injection, PII leakage, TLS configuration |

**Coverage target:** ≥ 80% combined (unit + integration), measured via Coverlet in CI.

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

---

## Observability

### Prometheus Metrics

| Metric | Type | Description |
|--------|------|-------------|
| `address_validation_requests_total` | Counter | Requests by endpoint, status, api_version |
| `address_validation_duration_seconds` | Histogram | Latency by endpoint, cache_source |
| `cache_hit_ratio` | Gauge | Hit ratio by cache tier (L1 / L2) |
| `smarty_api_calls_total` | Counter | Smarty API calls by status code |
| `smarty_api_errors_total` | Counter | Smarty API errors by error type |
| `active_circuit_breakers` | Gauge | Open circuit breakers by provider |

### Performance Targets (SLA)

| Scenario | Target |
|----------|--------|
| Cache hit (Redis L1) | < 200ms p95 |
| Cache miss (Smarty API) | < 800ms p95 |
| Batch (100 addresses) | < 2s p95 |
| Redis read | < 5ms p99 |
| CosmosDB read | < 15ms p99 |
| Service uptime | 99.9% (≤ 8.76 hours downtime/year) |

---

## Security Notes

- All communications are secured via **TLS 1.2+**
- Secrets are managed via **Azure Key Vault** (production) / `dotnet user-secrets` (local)
- **No raw PII is logged** — addresses are SHA-256 hashed in all audit events and structured logs
- Admin endpoints (`DELETE /api/cache/*`) require an `admin` RBAC role; non-admin requests return `HTTP 403`
- Input validation is enforced via **FluentValidation** on every endpoint before any processing

---

*For the full Software Requirements Specification, see [docs/Address-Validation-Proxy-SRS.md](docs/Address-Validation-Proxy-SRS.md).*
