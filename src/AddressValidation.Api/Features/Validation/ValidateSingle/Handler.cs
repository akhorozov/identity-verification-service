namespace AddressValidation.Api.Features.Validation.ValidateSingle;

using System.Diagnostics;
using AddressValidation.Api.Domain;
using AddressValidation.Api.Domain.Events;
using AddressValidation.Api.Infrastructure.Metrics;
using AddressValidation.Api.Infrastructure.Providers;
using AddressValidation.Api.Infrastructure.Services.Audit;
using AddressValidation.Api.Infrastructure.Services.Caching;

/// <summary>
/// Result returned by <see cref="ValidateSingleHandler"/>.
/// Carries the response, cache source metadata, and stale-fallback flag.
/// </summary>
public sealed record HandlerResult(
    ValidateSingleResponse Response,
    string CacheSource,
    bool IsStale = false);

/// <summary>
/// Handles a single address validation request (FR-001).
/// Flow: L1 (Redis) → L2 (CosmosDB) → Provider → write-through.
/// Emits audit events for every significant outcome.
/// SRS Ref: FR-001, Section 9.3.1, ADR-005
/// </summary>
public sealed class ValidateSingleHandler
{
    private readonly CacheOrchestrator<ValidationResponse> _cache;
    private readonly IAddressValidationProvider _provider;
    private readonly IAuditEventStore _audit;
    private readonly ILogger<ValidateSingleHandler> _logger;
    private readonly AppMetrics _metrics;

    public ValidateSingleHandler(
        CacheOrchestrator<ValidationResponse> cache,
        IAddressValidationProvider provider,
        IAuditEventStore audit,
        ILogger<ValidateSingleHandler> logger,
        AppMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(metrics);

        _cache    = cache;
        _provider = provider;
        _audit    = audit;
        _logger   = logger;
        _metrics  = metrics;
    }

    /// <summary>
    /// Executes the validation flow and returns a <see cref="HandlerResult"/>.
    /// Returns <c>null</c> when the address is undeliverable (DPV code N).
    /// </summary>
    public async Task<HandlerResult?> HandleAsync(
        ValidateSingleRequest request,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        var sw      = Stopwatch.StartNew();
        var input   = request.ToAddressInput();
        var cacheKey = input.GenerateCacheKey();
        var hash    = input.ComputeHash();

        ValidationResponse? domainResponse = null;
        string cacheSource = "PROVIDER";
        bool isStale = false;

        try
        {
            var cacheResult = await _cache.GetAsync(
                cacheKey,
                async (_, ct) =>
                {
                    var providerResult = await _provider.ValidateAsync(input, ct);
                    return providerResult;
                },
                cancellationToken);

            domainResponse = cacheResult.Value;
            cacheSource    = NormaliseSource(cacheResult.Source?.Source ?? "PROVIDER");

            // Update cache hit ratio gauge (FR-006)
            var isHit = cacheSource != "PROVIDER";
            var tier  = cacheSource == "L1" ? "L1-Redis" : cacheSource == "L2" ? "L2-CosmosDB" : "provider";
            _metrics.CacheHitRatio.WithLabels(tier).Set(isHit ? 1.0 : 0.0);

            // Provider miss — address not found by provider
            if (domainResponse is null)
            {
                await EmitFailedEventAsync(hash, "ProviderNoMatch", null, correlationId, cancellationToken);
                return null;
            }

            // Emit cache-created event when freshly fetched from provider
            if (cacheSource == "PROVIDER")
            {
                await _audit.AppendAsync(new CacheEntryCreated
                {
                    AggregateId   = hash,
                    CacheKey      = cacheKey,
                    CacheLayer    = "L1",
                    TtlSeconds    = null,
                    RequestCorrelationId = correlationId
                }, cancellationToken);
            }

            sw.Stop();
            domainResponse.Metadata.CacheSource     = cacheSource;
            domainResponse.Metadata.RequestDurationMs = sw.ElapsedMilliseconds;
            domainResponse.Metadata.CorrelationId   = correlationId;

            // Emit validated or undeliverable audit event
            var dpv = domainResponse.Analysis?.DpvMatchCode ?? string.Empty;

            if (dpv == "N")
            {
                // Undeliverable — emit failed event then signal 404 to endpoint
                await EmitFailedEventAsync(hash, "Undeliverable", null, correlationId, cancellationToken);
                return null;
            }

            await _audit.AppendAsync(new AddressValidated
            {
                AggregateId          = hash,
                AddressHash          = hash,
                DpvMatchCode         = dpv,
                ProviderName         = domainResponse.Metadata.ProviderName,
                CacheSource          = cacheSource,
                DurationMs           = sw.ElapsedMilliseconds,
                RequestCorrelationId = correlationId
            }, cancellationToken);

            return new HandlerResult(
                ValidateSingleResponse.FromDomain(domainResponse),
                cacheSource,
                isStale);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Validation failed for address hash {Hash}", hash);
            await EmitFailedEventAsync(hash, ex.Message, null, correlationId, cancellationToken);
            throw;
        }
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task EmitFailedEventAsync(
        string hash,
        string reason,
        int? httpStatus,
        string? correlationId,
        CancellationToken ct)
    {
        try
        {
            await _audit.AppendAsync(new AddressValidationFailed
            {
                AggregateId          = hash,
                AddressHash          = hash,
                FailureReason        = reason,
                HttpStatusCode       = httpStatus,
                ProviderName         = _provider.ProviderName,
                RequestCorrelationId = correlationId
            }, ct);
        }
        catch (Exception ex)
        {
            // Audit failures must never break the main request path.
            _logger.LogWarning(ex, "Failed to emit AddressValidationFailed audit event.");
        }
    }

    /// <summary>Normalises cache source strings from the orchestrator to the canonical values.</summary>
    private static string NormaliseSource(string raw) => raw switch
    {
        var s when s.StartsWith("L1", StringComparison.OrdinalIgnoreCase) => "L1",
        var s when s.StartsWith("L2", StringComparison.OrdinalIgnoreCase) => "L2",
        _ => "PROVIDER"
    };
}
