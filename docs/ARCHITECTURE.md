# Architecture & Design

This document outlines the architecture, design patterns, and infrastructure setup for the Address Validation Proxy Service.

---

## Table of Contents

1. [Solution Structure](#solution-structure)
2. [Architectural Patterns](#architectural-patterns)
3. [Central Package Management (CPM)](#central-package-management-cpm)
4. [YARP Reverse Proxy Gateway](#yarp-reverse-proxy-gateway)
5. [Observability & Monitoring](#observability--monitoring)
6. [Security & Resilience](#security--resilience)
7. [Configuration Management](#configuration-management)
8. [Deployment Architecture](#deployment-architecture)

---

## Solution Structure

The solution uses a **Vertical Slice Architecture (VSA)** with clear separation of concerns. The orchestration and shared-defaults projects reside at the solution root; application code lives under `src/`; tests under `tests/`.

```
IdentityVerification.slnx
├── src/
│   ├── AddressValidation.Api/               # Core validation service (port 5000)
│   │   ├── Program.cs
│   │   ├── Domain/                          # Domain models (flat, no slice folders yet)
│   │   │   ├── AddressInput.cs
│   │   │   ├── ValidatedAddress.cs
│   │   │   ├── AddressAnalysis.cs
│   │   │   ├── GeocodingResult.cs
│   │   │   ├── ValidationMetadata.cs
│   │   │   ├── ValidationResponse.cs
│   │   │   └── AddressHashExtensions.cs
│   │   ├── Infrastructure/
│   │   │   ├── Caching/                     # Legacy IDistributedCache abstraction
│   │   │   │   └── IDistributedCache.cs
│   │   │   ├── CosmosDb/                    # Legacy CosmosDbCache (pre-T3)
│   │   │   │   └── CosmosDbCache.cs
│   │   │   ├── Redis/                       # Legacy RedisCache (pre-T3)
│   │   │   │   └── RedisCache.cs
│   │   │   ├── Services/
│   │   │   │   └── Caching/                 # T3 multi-level cache services
│   │   │   │       ├── ICacheService.cs
│   │   │   │       ├── CacheOrchestrator.cs
│   │   │   │       ├── RedisCacheService.cs
│   │   │   │       ├── CosmosCacheService.cs
│   │   │   │       ├── CacheWarmingService.cs
│   │   │   │       └── CosmosDbInitializationService.cs
│   │   │   ├── Middleware/
│   │   │   │   ├── CorrelationIdMiddleware.cs
│   │   │   │   ├── ExceptionHandlingMiddleware.cs
│   │   │   │   └── SecurityHeadersMiddleware.cs
│   │   │   ├── Configuration/
│   │   │   │   └── AzureKeyVaultConfiguration.cs
│   │   │   └── ServiceCollectionExtensions.cs
│   │   └── appsettings*.json
│   │
│   └── AddressValidation.Gateway/           # YARP reverse proxy (port 5001)
│       ├── Program.cs
│       └── appsettings.json
│
├── AddressValidation.AppHost/               # Aspire orchestrator
│   ├── AppHost.cs
│   └── aspire.config.json
│
├── AddressValidation.ServiceDefaults/       # Shared telemetry & resilience defaults
│   └── Extensions.cs
│
├── tests/
│   ├── Unit/
│   │   └── AddressValidation.Tests.Unit/
│   │       ├── Domain_Models_Tests.cs
│   │       ├── Infrastructure/
│   │       │   └── Services/Caching/
│   │       │       └── CacheServiceTests.cs
│   │       └── UnitTestFixture.cs
│   └── Integration/
│       └── AddressValidation.Tests.Integration/
│           ├── Caching/
│           │   └── CacheHierarchyIntegrationTests.cs
│           └── IntegrationTestFixture.cs
│
├── Directory.Packages.props                 # Central Package Management
├── IdentityVerification.slnx
├── README.md
└── docs/
```

### Project Responsibilities

| Project | Purpose |
|---------|---------|
| **AddressValidation.Api** | Core validation service; domain models, T3 multi-level caching, middleware, resilience |
| **AddressValidation.Gateway** | YARP reverse proxy for traffic routing, security headers, and CORS |
| **AddressValidation.AppHost** | Aspire orchestrator — wires Redis, CosmosDB emulator, Api, and Gateway for local dev |
| **AddressValidation.ServiceDefaults** | Shared defaults for OpenTelemetry, health checks, and resilience across services |
| **AddressValidation.Tests.Unit** | xUnit unit tests for domain models and caching services (NSubstitute mocks) |
| **AddressValidation.Tests.Integration** | xUnit integration tests for cache hierarchy using Testcontainers |

---

## Architectural Patterns

### 1. Vertical Slice Architecture (VSA)

Domain models are currently organized in a flat `Domain/` folder inside `AddressValidation.Api`. Future iterations will reorganize per-feature into vertical slices:

```
Features/                    ← planned; not yet implemented
└── AddressValidation/
    ├── Domain/           # Core business entities & value objects
    ├── Application/      # Use cases, validators, business logic
    ├── Endpoints/        # API route handlers
    └── [Repositories, Services]
```

Current `Domain/` models (flat layout, all implemented):

| Model | Responsibility |
|-------|---------------|
| `AddressInput` | Client request with data annotations + cross-field validation |
| `ValidatedAddress` | USPS CASS-certified standardized address |
| `AddressAnalysis` | DPV deliverability indicators |
| `GeocodingResult` | Latitude, longitude, precision |
| `ValidationMetadata` | Provider name, timing, cache source |
| `ValidationResponse` | Aggregate response combining all models |
| `AddressHashExtensions` | Deterministic SHA-256 hashing & cache key utilities (`addr:v1:{hash}`) |

**Benefits of VSA (target state):**
- Feature isolation and independent deployment
- Clear feature ownership
- Easier testing and maintenance
- Minimal cross-cutting dependencies

### 2. Reverse Proxy Gateway Pattern (YARP)

The Gateway acts as a single entry point:

```
Client Requests
       ↓
   [YARP Gateway]
       ↓
    [Routes]
       ↓
[AddressValidation.Api]
[Future Services...]
```

**Benefits:**
- Single entry point for all requests
- Cross-cutting concerns (logging, rate limiting) centralized
- Easy service evolution without client changes
- Load balancing and traffic distribution

### 3. Clean Architecture Principles

- **Domain Layer**: No external dependencies
- **Application Layer**: Business logic & use cases
- **Infrastructure Layer**: Database, caching, external APIs
- **Presentation Layer**: API endpoints via Minimal APIs

---

## T3 Caching Layer (feat/t3-caching-layer)

The T3 milestone introduced a generic, multi-level cache abstraction that replaces the earlier single-provider `IDistributedCache` pattern.

### Core Abstractions

| Type | Role |
|------|------|
| `ICacheService<T>` | Generic interface: `GetAsync`, `SetAsync`, `RemoveAsync`, `ExistsAsync` |
| `CacheOrchestrator<T>` | L1 → L2 → Provider lookup with write-through on miss |
| `RedisCacheService<T>` | L1 implementation using `StackExchange.Redis`; default TTL 1 h |
| `CosmosCacheService<T>` | L2 implementation using Azure Cosmos DB; default TTL 90 days |
| `CacheWarmingService` | Hosted service that pre-warms caches on startup (placeholder, extensible) |
| `CosmosDbInitializationService` | Ensures Cosmos DB database and cache container exist at startup |

### Cache Hierarchy

```
Client Request
      ↓
[CacheOrchestrator<T>]
      ↓
 L1: RedisCacheService<T>        (hit → return + record latency)
      ↓ miss
 L2: CosmosCacheService<T>       (hit → back-fill L1 + return)
      ↓ miss
 Provider (Smarty API)           (hit → write-through to L2 + L1 + return)
```

`CacheResult<T>` carries the value and a `CacheSourceMetadata` record identifying the source (`L1`, `L2`, or `PROVIDER`) plus retrieval latency.

### Cache Key Format

```
addr:v1:{64-char-SHA-256-hex}
```

The hash is computed over the normalized (uppercase, trimmed) address fields by `AddressHashExtensions.ComputeCacheKey`. Bumping the version prefix (`v1` → `v2`) invalidates all prior entries automatically.

### Configuration

```json
"Redis": {
  "Enabled": true,
  "ConnectionString": "localhost:6379",
  "DefaultDatabase": 0,
  "DefaultTtlSeconds": 3600,
  "Ssl": false,
  "AbortOnConnectFail": true,
  "KeepAlive": 180,
  "ConnectTimeout": 5000,
  "SyncTimeout": 5000
},
"Cosmos": {
  "Enabled": true,
  "Endpoint": "https://...",
  "DatabaseId": "AddressValidation",
  "CacheContainerId": "ValidatedAddresses",
  "PartitionKeyPath": "/pk",
  "DefaultTtlSeconds": 7776000
}
```

---

## Central Package Management (CPM)

### Overview

All NuGet package versions are centralized in **Directory.Packages.props** at the solution root. This ensures:

- **Consistent versions** across all projects
- **Reduced maintenance** (single source of truth)
- **Transitive dependency clarity**
- **Easier upgrades** and security patching

### Directory.Packages.props

Located at: `./Directory.Packages.props`

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackagesFile>Directory.Packages.props</CentralPackagesFile>
  </PropertyGroup>

  <ItemGroup>
    <!-- Framework -->
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection" Version="10.0.0" />
    <!-- API & Web -->
    <PackageVersion Include="Yarp.ReverseProxy" Version="2.3.0" />
    <!-- Observability -->
    <PackageVersion Include="OpenTelemetry.Extensions.Hosting" Version="1.15.0" />
    <!-- ... more packages ... -->
  </ItemGroup>
</Project>
```

### Usage in Project Files

Remove version attributes from PackageReference elements:

```xml
<!-- Before -->
<PackageReference Include="Serilog.AspNetCore" Version="10.0.0" />

<!-- After (with CPM) -->
<PackageReference Include="Serilog.AspNetCore" />
```

### Reference Documentation

- [Microsoft Learn: Central Package Management](https://learn.microsoft.com/en-us/nuget/consume/central-package-management/overview)
- [NuGet Documentation](https://learn.microsoft.com/en-us/nuget/)

---

## YARP Reverse Proxy Gateway

### Overview

YARP (Yet Another Reverse Proxy) is used as the API gateway for routing, load balancing, and cross-cutting concerns.

### Configuration

**File**: `src/AddressValidation.Gateway/appsettings.json`

```json
{
  "ReverseProxy": {
    "Routes": {
      "addressValidationRoute": {
        "ClusterId": "addressValidationCluster",
        "Match": {
          "Path": "/api/address/{**catch-all}"
        },
        "Transforms": [
          { "PathPrefix": "/api/address" }
        ]
      }
    },
    "Clusters": {
      "addressValidationCluster": {
        "HttpClient": {
          "Timeout": "00:00:30"
        },
        "Destinations": {
          "destination1": {
            "Address": "http://localhost:5000"
          }
        }
      }
    }
  }
}
```

### Routes

| Route | Destination | Purpose |
|-------|-------------|---------|
| `/api/address/*` | AddressValidation.Api:5000 | Core validation endpoints |
| `/health` | Gateway | Health check endpoint |
| `/` | Redirect to `/health` | Default gateway endpoint |

### Security Features

The Gateway implements:

- **Security Headers Middleware**: X-Content-Type-Options, X-Frame-Options, etc.
- **CORS Configuration**: Restricts cross-origin requests
- **Health Checks**: Service health monitoring
- **Rate Limiting**: Available for future implementation
- **Request Correlation**: Distributed tracing support

### Running Locally

```bash
# Via Aspire AppHost
dotnet run --project src/AddressValidation.AppHost

# Direct (if needed)
dotnet run --project src/AddressValidation.Gateway
```

**Access Points:**
- Gateway: `http://localhost:5001`
- Health: `http://localhost:5001/health`
- API (via Gateway): `http://localhost:5001/api/address/validate`

### Reference Documentation

- [YARP Documentation](https://microsoft.github.io/reverse-proxy/)
- [YARP Configuration](https://microsoft.github.io/reverse-proxy/articles/config-files.html)

---

## Observability & Monitoring

### Logging

**Framework**: Serilog

- Structured JSON logging for easy machine parsing
- Correlation ID tracking across requests
- Configuration via `appsettings.json`

**Key Configuration**:

```json
{
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      { "Name": "Console" },
      { "Name": "File", "Args": { "path": "logs/app-.txt" } }
    ],
    "Enrich": ["FromLogContext", "WithCorrelationIdHeader"]
  }
}
```

### Distributed Tracing

**Framework**: OpenTelemetry + Console Exporter (development)

Collects:
- HTTP request/response traces
- Redis cache operations
- CosmosDB queries
- Custom application spans

**Production Setup**: Configure OTLP exporter to send traces to observability backend (e.g., Jaeger, Datadog, Azure Monitor).

### Metrics

**Framework**: Prometheus

Exposes metrics on `/metrics` endpoint (when configured):

- HTTP request duration & counts
- Cache hit/miss rates
- API response times

---

## Security & Resilience

### Authentication & Authorization

- JWT token validation (configured at API layer)
- Claims-based authorization
- API key support for service-to-service calls

### Resilience Patterns

- **Polly Policies**: Retry, circuit breaker, timeout
- **Rate Limiting**: Per-client and global limits
- **Distributed Caching**: Redis with fallback to CosmosDB

### Input Validation

- **FluentValidation** for request DTOs
- Automatic validation via middleware
- Custom validators per feature

---

## Configuration Management

### Configuration Hierarchy

1. **appsettings.json**: Default configuration
2. **appsettings.{Environment}.json**: Environment-specific overrides
3. **User Secrets**: Local development secrets (not committed)
4. **Environment Variables**: Container/server-level overrides
5. **Azure Key Vault**: Production secrets (if configured)

### Key Configuration Sections

```json
{
  "Logging": { ... },
  "AddressValidation": {
    "EnableDetailedErrors": false,
    "CacheSettings": { ... }
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  },
  "CosmosDb": {
    "ConnectionString": "...",
    "DatabaseId": "AddressValidation"
  },
  "AzureKeyVault": {
    "Enabled": false,
    "VaultUri": "https://your-vault.vault.azure.net/"
  }
}
```

---

## Deployment Architecture

### Local Development

**Tool**: .NET Aspire

```
Aspire AppHost
├── Redis (Emulator or container)
├── CosmosDB (Emulator)
├── AddressValidation.Api (port 5000)
└── AddressValidation.Gateway (port 5001)
```

**Start**: `dotnet run --project src/AddressValidation.AppHost`

### Production Deployment

**Recommended Stack:**
- **Container**: Docker / OCI
- **Orchestration**: Azure Container Apps or AKS
- **Networking**: Azure Front Door + YARP Gateway
- **Caching**: Azure Cache for Redis
- **Database**: Azure Cosmos DB
- **Observability**: Azure Monitor / Application Insights
- **Service Discovery**: Built-in via Aspire / managed networking

---

## Decision Records

Key architectural decisions are documented in:

- **ADR-001**: Vertical Slice Architecture
- **ADR-002**: Minimal API Framework
- **ADR-003**: YARP Gateway Pattern
- **ADR-004**: Central Package Management

See `docs/` folder for full decision records.

---

## Build & Test

### Local Build

```bash
# Full solution
dotnet build AddressValidation.slnx -c Release

# Specific project
dotnet build src/AddressValidation.Api/AddressValidation.Api.csproj
```

### Running Tests

```bash
# All tests
dotnet test AddressValidation.slnx

# Unit tests only
dotnet test src/AddressValidation.Tests.Unit/

# Integration tests
dotnet test src/AddressValidation.Tests.Integration/
```

### Code Quality

- **Static Analysis**: .NET Analyzer (built-in)
- **Style**: EditorConfig (`.editorconfig` in repo)
- **Security**: Aikido SAST scans on pull requests

---

## Performance Considerations

1. **Caching Strategy**:
   - Redis for hot data (O(1) access)
   - Cosmos DB TTL for distributed state
   - Correlation ID for cross-service tracing

2. **Rate Limiting**:
   - Per-IP limits at gateway
   - Per-user limits at API layer
   - Quota management for SmartyStreets API

3. **Connection Pooling**:
   - HttpClient factories
   - Redis connection multiplexer
   - Cosmos DB client singleton

---

## Troubleshooting

### Build Issues

```bash
# Clear NuGet cache
dotnet nuget locals all --clear

# Restore packages
dotnet restore AddressValidation.slnx

# Full rebuild
dotnet clean && dotnet build
```

### Runtime Issues

Check logs:
```bash
# Serilog files
tail -f logs/app-*.txt

# Docker logs (if containerized)
docker logs <container-id>
```

---

## Contributing

When modifying architecture or adding new projects:

1. Update this ARCHITECTURE.md file
2. Add corresponding ADR if making significant decisions
3. Update solution file (.slnx)
4. Run full build and tests: `dotnet test AddressValidation.slnx`

---

**Last Updated**: 2026-05-07  
**Maintained By**: Development Team
