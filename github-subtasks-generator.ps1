#!/usr/bin/env pwsh

# GitHub Subtasks Generator for Address Validation Proxy Service
# Creates all 131 subtasks from the breakdown and updates parent issues

$repo = "akhorozov/identity-verification-service"
$owner = "akhorozov"
$repoName = "identity-verification-service"

# Track created issue numbers
$createdIssues = @{}

function Create-Issue {
    param(
        [string]$title,
        [string]$body,
        [string[]]$labels
    )

    try {
        $result = gh issue create --repo $repo --title $title --body $body --label ($labels -join ",") 2>&1
        # Extract issue number from output (typically "#123")
        if ($result -match '#(\d+)') {
            return [int]$matches[1]
        }
        Write-Host "⚠️  Could not parse issue number from: $result"
        return $null
    }
    catch {
        Write-Host "❌ Error creating issue: $_"
        return $null
    }
}

function Update-IssueBody {
    param(
        [int]$issueNumber,
        [string]$newBody
    )

    try {
        gh issue edit $issueNumber --repo $repo --body $newBody 2>&1 | Out-Null
        Write-Host "✅ Updated #$issueNumber"
    }
    catch {
        Write-Host "❌ Error updating #$issueNumber : $_"
    }
}

# ===== T1: PROJECT SCAFFOLD & INFRASTRUCTURE SETUP =====
Write-Host "📦 Creating T1 subtasks (1.1-1.10)..." -ForegroundColor Cyan

$t1_1 = Create-Issue `
    -title "1.1: Create solution structure per SRS Section 3.1.1 (VSA folders)" `
    -body @"
## Sub-task 1.1 — Create Solution Structure (VSA)

**Parent Task:** #2 T1: Project Scaffold & Infrastructure Setup
**Priority:** P1 | **SRS Ref:** Section 3.1.1, ADR-003

### Description
Create the Vertical Slice Architecture (VSA) folder structure for the Address Validation Proxy Service as defined in SRS Section 3.1.1 and ADR-003.

### Acceptance Criteria
- [ ] Solution file created: ``AddressValidation.sln``
- [ ] Project ``AddressValidation.Api`` created (ASP.NET Core Minimal API)
- [ ] Project ``AddressValidation.AppHost`` created (.NET Aspire host)
- [ ] Project ``AddressValidation.ServiceDefaults`` created (shared Aspire defaults)
- [ ] Project ``AddressValidation.Tests.Unit`` created (xUnit)
- [ ] Project ``AddressValidation.Tests.Integration`` created (xUnit + Testcontainers)
- [ ] VSA feature folders inside Api: ``Features/Validation/``, ``Features/Cache/``, ``Features/Health/``, ``Features/Metrics/``
- [ ] Shared folders: ``Infrastructure/``, ``Domain/``, ``Common/``
- [ ] All projects reference correct SDK versions (.NET 10)

### Technical Notes
- Follow ADR-003 for VSA naming conventions
- Each feature folder contains: ``Endpoint.cs``, ``Handler.cs``, ``Models.cs``, ``Validator.cs``
- No cross-feature dependencies — only shared ``Domain`` and ``Infrastructure`` layers
"@ `
    -labels @("subtask", "P1", "scaffold", "vsa")

$t1_2 = Create-Issue `
    -title "1.2: Configure .NET Aspire AppHost with Redis + CosmosDB emulator" `
    -body @"
## Sub-task 1.2 — Configure .NET Aspire AppHost

**Parent Task:** #2 T1: Project Scaffold & Infrastructure Setup
**Priority:** P1 | **SRS Ref:** Section 2.4

### Description
Configure the ``AddressValidation.AppHost`` project to wire Redis and CosmosDB emulator as Aspire resources for local development.

### Acceptance Criteria
- [ ] ``AppHost/Program.cs`` registers Redis resource via ``builder.AddRedis("redis")``
- [ ] ``AppHost/Program.cs`` registers CosmosDB emulator via ``builder.AddAzureCosmosDB("cosmosdb").RunAsEmulator()``
- [ ] Api project references both resources via ``WithReference(redis)`` and ``WithReference(cosmosdb)``
- [ ] Aspire dashboard accessible at ``https://localhost:15888`` during local run
- [ ] Health checks visible in Aspire dashboard for all resources
- [ ] ``dotnet run --project AddressValidation.AppHost`` starts all services successfully

### Technical Notes
- Use ``Aspire.Hosting.Azure.CosmosDB`` package for CosmosDB emulator
- Use ``Aspire.Hosting.Redis`` package for Redis
- Emulator should auto-create required databases/containers on startup
"@ `
    -labels @("subtask", "P1", "aspire", "infrastructure")

$t1_3 = Create-Issue `
    -title "1.3: Wire appsettings.json with all Appendix A configuration keys" `
    -body @"
## Sub-task 1.3 — Wire appsettings.json Configuration

**Parent Task:** #2 T1: Project Scaffold & Infrastructure Setup
**Priority:** P1 | **SRS Ref:** Appendix A

### Description
Create and populate ``appsettings.json`` and ``appsettings.Development.json`` with all configuration keys defined in SRS Appendix A. Create a strongly-typed ``AppSettings`` options class with validation.

### Acceptance Criteria
- [ ] ``appsettings.json`` contains all Appendix A keys with sensible production defaults
- [ ] ``appsettings.Development.json`` contains development overrides
- [ ] Strongly-typed options classes created: ``SmartyOptions``, ``CacheOptions``, ``ResilienceOptions``, ``RateLimitOptions``, ``FeatureToggles``
- [ ] Options registered via ``AddOptions<T>().BindConfiguration().ValidateDataAnnotations().ValidateOnStart()``
- [ ] Startup fails fast if required config keys are missing
- [ ] Configuration schema documented in code XML comments

### Key Configuration Sections (Appendix A)
\`\`\`json
{
  "Smarty": { "AuthId": "", "AuthToken": "", "MaxBatchSize": 100, "TimeoutSeconds": 5 },
  "Cache": { "RedisConnectionString": "", "RedisTtlSeconds": 3600, "CosmosDbConnectionString": "", "CosmosDbDatabase": "address-validation", "L2TtlDays": 90 },
  "Resilience": { "RetryCount": 3, "RetryBaseDelayMs": 200, "CircuitBreakerFailureThreshold": 5, "CircuitBreakerDurationSeconds": 30, "BulkheadMaxConcurrency": 25 },
  "RateLimit": { "RequestsPerMinute": 1000 },
  "FeatureToggles": { "ProviderName": "Smarty" }
}
\`\`\`
"@ `
    -labels @("subtask", "P1", "configuration")

$t1_4 = Create-Issue `
    -title "1.4: Set up Azure Key Vault integration for secrets (Smarty, CosmosDB)" `
    -body @"
## Sub-task 1.4 — Azure Key Vault Integration

**Parent Task:** #2 T1: Project Scaffold & Infrastructure Setup
**Priority:** P1 | **SRS Ref:** NFR-016

### Description
Integrate Azure Key Vault as the secrets provider for production secrets (Smarty credentials, CosmosDB connection string, API keys). Use Managed Identity for authentication.

### Acceptance Criteria
- [ ] ``Azure.Extensions.AspNetCore.Configuration.Secrets`` NuGet package added
- [ ] Key Vault URI configured via ``KeyVault:Uri`` in ``appsettings.json``
- [ ] ``builder.Configuration.AddAzureKeyVault(...)`` added in ``Program.cs`` (production only)
- [ ] Managed Identity (``DefaultAzureCredential``) used — no client secret
- [ ] Secret names follow naming: ``Smarty--AuthId``, ``Smarty--AuthToken``, ``Cache--RedisConnectionString``, etc.
- [ ] Local development falls back to ``dotnet user-secrets`` (not Key Vault)
- [ ] Key Vault access failure logs clear error and prevents startup

### Technical Notes
- Use ``Azure.Identity`` ``DefaultAzureCredential`` for auth
- Key Vault should be referenced as Aspire resource for staging/prod
- Never log or expose secret values — only Key Vault URIs
"@ `
    -labels @("subtask", "P1", "security", "azure-keyvault")

$t1_5 = Create-Issue `
    -title "1.5: Configure dotnet user-secrets for local development" `
    -body @"
## Sub-task 1.5 — Configure dotnet user-secrets for Local Dev

**Parent Task:** #2 T1: Project Scaffold & Infrastructure Setup
**Priority:** P1 | **SRS Ref:** Appendix A

### Description
Configure ``dotnet user-secrets`` as the local development secrets provider so developers never commit sensitive values to source control.

### Acceptance Criteria
- [ ] ``UserSecretsId`` GUID set in ``AddressValidation.Api.csproj``
- [ ] ``README.md`` updated with setup instructions for required secrets
- [ ] ``.gitignore`` includes ``secrets.json`` and ``*.pfx`` patterns
- [ ] ``appsettings.Development.json`` contains only non-sensitive dev config (no credentials)
- [ ] All sensitive keys documented with ``dotnet user-secrets set`` examples in README
- [ ] CI/CD pipeline uses environment variables or Key Vault — never user-secrets

### Setup Instructions (for README)
\`\`\`bash
dotnet user-secrets set "Smarty:AuthId" "<your-auth-id>" --project src/AddressValidation.Api
dotnet user-secrets set "Smarty:AuthToken" "<your-auth-token>" --project src/AddressValidation.Api
dotnet user-secrets set "Cache:RedisConnectionString" "localhost:6379" --project src/AddressValidation.Api
\`\`\`
"@ `
    -labels @("subtask", "P1", "security", "configuration")

$t1_6 = Create-Issue `
    -title "1.6: Register all DI services in ServiceCollectionExtensions.cs" `
    -body @"
## Sub-task 1.6 — DI Service Registration

**Parent Task:** #2 T1: Project Scaffold & Infrastructure Setup
**Priority:** P1 | **SRS Ref:** Section 3.1

### Description
Create ``ServiceCollectionExtensions.cs`` as the central DI registration hub. All application services, infrastructure services, and middleware should be registered through organized extension methods.

### Acceptance Criteria
- [ ] ``AddAddressValidationServices()`` extension method created
- [ ] Sub-methods: ``AddCaching()``, ``AddProviders()``, ``AddResilience()``, ``AddAudit()``, ``AddValidators()``
- [ ] All interfaces mapped to implementations with correct lifetimes
- [ ] ``ICacheService<T>`` → ``RedisCacheService`` (Singleton)
- [ ] ``IAddressValidationProvider`` → ``SmartyProvider`` (Singleton)
- [ ] ``IAuditEventStore`` → ``CosmosAuditEventStore`` (Singleton)
- [ ] ``CacheOrchestrator`` registered as Singleton
- [ ] All options classes bound and validated at registration time
- [ ] No service locator pattern — pure constructor injection only

### Lifetime Guidelines
| Service | Lifetime | Reason |
|---------|----------|--------|
| Redis client | Singleton | Shared connection pool |
| CosmosDB client | Singleton | SDK recommendation |
| SmartyProvider | Singleton | HttpClient managed by IHttpClientFactory |
| Handlers | Scoped | Per-request state |
"@ `
    -labels @("subtask", "P1", "dependency-injection")

$t1_7 = Create-Issue `
    -title "1.7: Configure Asp.Versioning.Http with HeaderApiVersionReader" `
    -body @"
## Sub-task 1.7 — API Versioning Configuration

**Parent Task:** #2 T1: Project Scaffold & Infrastructure Setup
**Priority:** P1 | **SRS Ref:** ADR-001, Section 3.2

### Description
Configure ``Asp.Versioning.Http`` package with ``HeaderApiVersionReader`` so API version is read from the ``Api-Version`` request header as per ADR-001.

### Acceptance Criteria
- [ ] ``Asp.Versioning.Http`` NuGet package added
- [ ] Version reader configured with ``Api-Version`` header
- [ ] Default API version set to 1.0
- [ ] All endpoints decorated with ``[ApiVersion("1.0")]``
- [ ] Missing ``Api-Version`` header returns HTTP 400 with RFC 7807 body
- [ ] ``api-supported-versions`` and ``api-deprecated-versions`` response headers included
- [ ] Version ``2.0`` reserved/documented for future use
- [ ] ``Sunset`` header support implemented for deprecation

### Version Header Examples
\`\`\`
Request:  Api-Version: 1.0
Response: api-supported-versions: 1.0, api-deprecated-versions: (none)
\`\`\`
"@ `
    -labels @("subtask", "P1", "versioning")

$t1_8 = Create-Issue `
    -title "1.8: Configure Serilog structured logging with OpenTelemetry enrichment" `
    -body @"
## Sub-task 1.8 — Serilog Structured Logging Setup

**Parent Task:** #2 T1: Project Scaffold & Infrastructure Setup
**Priority:** P1 | **SRS Ref:** NFR-020

### Description
Configure Serilog as the structured logging provider with OpenTelemetry enrichment, correlation ID injection, and PII scrubbing. All logs must be structured JSON.

### Acceptance Criteria
- [ ] ``Serilog.AspNetCore``, ``Serilog.Enrichers.CorrelationId``, ``Serilog.Sinks.OpenTelemetry`` packages added
- [ ] ``UseSerilog()`` configured in ``Program.cs``
- [ ] Log output format: structured JSON with ``TraceId``, ``SpanId``, ``CorrelationId``, ``RequestPath``, ``StatusCode``
- [ ] ``Destructurama.Attributed`` used to mark PII fields with ``[NotLogged]`` attribute
- [ ] Minimum log level: ``Information`` in production, ``Debug`` in development
- [ ] Serilog sink configured to emit to OpenTelemetry collector
- [ ] Request logging middleware enabled via ``app.UseSerilogRequestLogging()``
- [ ] Sensitive fields (addresses, API keys) never appear in log output

### Log Structure Example
\`\`\`json
{ "Level": "Information", "TraceId": "abc123", "CorrelationId": "req-456", "Message": "Address validated", "CacheSource": "L1", "Duration": 12 }
\`\`\`
"@ `
    -labels @("subtask", "P1", "serilog", "observability")

$t1_9 = Create-Issue `
    -title "1.9: Configure OpenTelemetry SDK (traces, metrics, logs)" `
    -body @"
## Sub-task 1.9 — OpenTelemetry SDK Configuration

**Parent Task:** #2 T1: Project Scaffold & Infrastructure Setup
**Priority:** P1 | **SRS Ref:** NFR-021

### Description
Configure the OpenTelemetry SDK for distributed tracing, metrics, and logs. Export to OTLP endpoint (Aspire dashboard locally, Azure Monitor in production).

### Acceptance Criteria
- [ ] ``OpenTelemetry.Extensions.Hosting``, ``OpenTelemetry.Instrumentation.AspNetCore``, ``OpenTelemetry.Instrumentation.Http``, ``OpenTelemetry.Instrumentation.StackExchangeRedis``, ``Azure.Monitor.OpenTelemetry.AspNetCore`` packages added
- [ ] Traces configured: ASP.NET Core, HttpClient, Redis, CosmosDB instrumentation
- [ ] Metrics configured: ASP.NET Core, HttpClient, Runtime metrics
- [ ] Logs configured: routed through OpenTelemetry log bridge
- [ ] OTLP exporter configured for local Aspire dashboard
- [ ] Azure Monitor exporter configured for production (connection string from Key Vault)
- [ ] ``ActivitySource`` named ``AddressValidation`` for custom spans
- [ ] Service name, version, and environment set as resource attributes

### Resource Attributes
\`\`\`csharp
ResourceBuilder.CreateDefault()
  .AddService("AddressValidation.Api", serviceVersion: "1.0.0")
  .AddAttributes(new[] { new KeyValuePair<string,object>("deployment.environment", env) })
\`\`\`
"@ `
    -labels @("subtask", "P1", "opentelemetry", "observability")

$t1_10 = Create-Issue `
    -title "1.10: Configure FluentValidation auto-registration" `
    -body @"
## Sub-task 1.10 — FluentValidation Auto-Registration

**Parent Task:** #2 T1: Project Scaffold & Infrastructure Setup
**Priority:** P1 | **SRS Ref:** Section 2.4

### Description
Configure FluentValidation with automatic validator discovery from the Api assembly. Wire validation failures to RFC 7807 ProblemDetails responses.

### Acceptance Criteria
- [ ] ``FluentValidation.AspNetCore`` NuGet package added
- [ ] ``services.AddValidatorsFromAssemblyContaining<Program>()`` registered
- [ ] Validation middleware added for Minimal APIs or endpoint-level validators
- [ ] ``ValidationException`` mapped to HTTP 400 with RFC 7807 ``ProblemDetails`` body
- [ ] Error response includes field-level validation messages
- [ ] Custom validators inherit from ``AbstractValidator<T>``
- [ ] All validators registered as Scoped lifetime
- [ ] Custom rules for address validation: ZIP code format, state codes, etc.

### Validation Error Response Format
\`\`\`json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Validation Failed",
  "status": 400,
  "errors": { "zipCode": ["ZIP code must be 5 or 9 digits"] }
}
\`\`\`
"@ `
    -labels @("subtask", "P1", "validation", "fluentvalidation")

Write-Host "✅ Created T1 subtasks: #$t1_1, #$t1_2, #$t1_3, #$t1_4, #$t1_5, #$t1_6, #$t1_7, #$t1_8, #$t1_9, #$t1_10" -ForegroundColor Green

# Store for later parent issue update
$createdIssues["T1"] = @($t1_1, $t1_2, $t1_3, $t1_4, $t1_5, $t1_6, $t1_7, $t1_8, $t1_9, $t1_10)

# ===== UPDATE PARENT ISSUE #2 =====
Write-Host "📝 Updating parent #2..." -ForegroundColor Yellow

$t1ParentBody = @"
## Overview
Set up the solution structure, .NET Aspire host, dependency injection wiring, and all foundational configuration required before any feature work begins.

**Epic:** #1  
**SRS Reference:** Section 3.1.1, Section 2.4, Appendix A  
**Priority:** P1

## Sub-tasks
- #$t1_1: Create solution structure per SRS Section 3.1.1 (VSA folders)
- #$t1_2: Configure .NET Aspire AppHost with Redis + CosmosDB emulator
- #$t1_3: Wire appsettings.json with all Appendix A configuration keys
- #$t1_4: Set up Azure Key Vault integration for secrets (Smarty, CosmosDB)
- #$t1_5: Configure dotnet user-secrets for local development
- #$t1_6: Register all DI services in ServiceCollectionExtensions.cs
- #$t1_7: Configure Asp.Versioning.Http with HeaderApiVersionReader
- #$t1_8: Configure Serilog structured logging with OpenTelemetry enrichment
- #$t1_9: Configure OpenTelemetry SDK (traces, metrics, logs)
- #$t1_10: Configure FluentValidation auto-registration

## Acceptance Criteria
- [ ] Solution compiles without errors with all projects
- [ ] ``dotnet run --project AddressValidation.AppHost`` starts successfully
- [ ] Aspire dashboard shows Redis and CosmosDB as healthy resources
- [ ] Configuration system loads all required keys from appsettings
- [ ] All DI services resolve without circular dependencies
- [ ] Structured logging outputs JSON format with correlation IDs
- [ ] OpenTelemetry exports to Aspire dashboard
- [ ] API versioning enforced on all endpoints
"@

Update-IssueBody -issueNumber 2 -newBody $t1ParentBody

Write-Host "📦 T1 subtasks complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Created Issues Summary:" -ForegroundColor Cyan
Write-Host "T1 Subtasks (#1.1-#1.10): $(($createdIssues['T1'] -join ', '))"
