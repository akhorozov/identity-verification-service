namespace AddressValidation.Api.Features.Validation.ValidateBatch;

using System.Diagnostics;
using AddressValidation.Api.Domain;
using AddressValidation.Api.Domain.Events;
using AddressValidation.Api.Infrastructure.Providers;
using AddressValidation.Api.Infrastructure.Services.Audit;
using AddressValidation.Api.Infrastructure.Services.Caching;

/// <summary>
/// Handles a batch address validation request (FR-002).
/// Flow per address: L1 (Redis) → L2 (CosmosDB) → Provider → write-through.
/// All cache lookups are issued in parallel; provider calls are chunked (max 100/call).
/// Emits audit events for validated, failed, and cache-created outcomes.
/// SRS Ref: FR-002, Section 9.3.2, ADR-005
/// </summary>
public sealed class ValidateBatchHandler
{
    private readonly CacheOrchestrator<ValidationResponse> _cache;
    private readonly IAddressValidationProvider _provider;
    private readonly IAuditEventStore _audit;
    private readonly ILogger<ValidateBatchHandler> _logger;

    public ValidateBatchHandler(
        CacheOrchestrator<ValidationResponse> cache,
        IAddressValidationProvider provider,
        IAuditEventStore audit,
        ILogger<ValidateBatchHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(logger);

        _cache    = cache;
        _provider = provider;
        _audit    = audit;
        _logger   = logger;
    }

    /// <summary>
    /// Validates all addresses in the batch request.
    /// Results are returned in the same order as the input array.
    /// </summary>
    public async Task<ValidateBatchResponse> HandleAsync(
        ValidateBatchRequest request,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        // Compute inputs and cache keys up front
        var items = request.Addresses
            .Select((item, idx) => (Item: item, Index: idx, Input: item.ToAddressInput()))
            .ToArray();

        // ── Phase 1: Parallel cache lookups (L1 → L2) ────────────────────────
        var cacheResults = await Task.WhenAll(
            items.Select(x => LookupCacheAsync(x.Input, cancellationToken)));

        // Separate hits from misses
        var hits   = new List<(int Index, AddressInput Input, ValidationResponse Response, string Source)>();
        var misses = new List<(int Index, AddressInput Input)>();

        for (var i = 0; i < items.Length; i++)
        {
            var (_, idx, input) = items[i];
            var (response, source) = cacheResults[i];

            if (response is not null)
                hits.Add((idx, input, response, source));
            else
                misses.Add((idx, input));
        }

        // ── Phase 2: Provider calls for cache misses (parallel per address) ──
        var providerResults = new Dictionary<int, (ValidationResponse? Response, string Source)>();

        if (misses.Count > 0)
        {
            var providerTasks = misses.Select(async m =>
            {
                try
                {
                    var result = await _provider.ValidateAsync(m.Input, cancellationToken);

                    if (result is not null)
                    {
                        // Write-through to cache
                        var key = m.Input.GenerateCacheKey();
                        _ = WriteToCacheAsync(key, result, cancellationToken);
                    }

                    return (m.Index, Response: result, Source: "PROVIDER");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Provider validation failed for index {Index}", m.Index);
                    return (m.Index, Response: (ValidationResponse?)null, Source: "PROVIDER");
                }
            });

            var providerOutcomes = await Task.WhenAll(providerTasks);
            foreach (var (idx, response, source) in providerOutcomes)
                providerResults[idx] = (response, source);
        }

        // ── Phase 3: Merge results in original inputIndex order ───────────────
        var results        = new ValidateBatchResultItem[items.Length];
        var validated      = 0;
        var failed         = 0;
        var cacheHits      = 0;
        var cacheMisses    = 0;
        var auditTasks     = new List<Task>();

        foreach (var (item, idx, input) in items)
        {
            ValidationResponse? response;
            string              source;

            var hit = hits.FirstOrDefault(h => h.Index == idx);
            if (hit != default)
            {
                response = hit.Response;
                source   = hit.Source;
                cacheHits++;
            }
            else
            {
                (response, source) = providerResults.TryGetValue(idx, out var pr) ? pr : (null, "PROVIDER");
                cacheMisses++;
            }

            var hash = input.ComputeHash();

            if (response is null)
            {
                results[idx] = ValidateBatchResultItem.Failed(idx, input, "Address could not be validated or is undeliverable.");
                failed++;
                auditTasks.Add(EmitFailedEventAsync(hash, "ProviderNoMatch", correlationId, CancellationToken.None));
                continue;
            }

            var dpv = response.Analysis?.DpvMatchCode ?? string.Empty;

            if (dpv == "N")
            {
                results[idx] = ValidateBatchResultItem.Failed(idx, input, "Address is undeliverable (DPV code N).");
                failed++;
                auditTasks.Add(EmitFailedEventAsync(hash, "Undeliverable", correlationId, CancellationToken.None));
                continue;
            }

            // Successful result — stamp metadata
            response.Metadata.CacheSource = source;
            response.Metadata.CorrelationId = correlationId;

            results[idx] = ValidateBatchResultItem.FromDomain(idx, response);
            validated++;

            auditTasks.Add(_audit.AppendAsync(new AddressValidated
            {
                AggregateId          = hash,
                AddressHash          = hash,
                DpvMatchCode         = dpv,
                ProviderName         = response.Metadata.ProviderName,
                CacheSource          = source,
                DurationMs           = sw.ElapsedMilliseconds,
                RequestCorrelationId = correlationId
            }, CancellationToken.None));

            if (source == "PROVIDER")
            {
                auditTasks.Add(_audit.AppendAsync(new CacheEntryCreated
                {
                    AggregateId          = hash,
                    CacheKey             = input.GenerateCacheKey(),
                    CacheLayer           = "L1",
                    TtlSeconds           = null,
                    RequestCorrelationId = correlationId
                }, CancellationToken.None));
            }
        }

        // Fire audit events without blocking the response
        _ = Task.WhenAll(auditTasks).ContinueWith(t =>
        {
            if (t.IsFaulted)
                _logger.LogWarning(t.Exception, "One or more batch audit events failed to emit.");
        }, TaskContinuationOptions.OnlyOnFaulted);

        sw.Stop();

        var summary = new ValidateBatchSummary
        {
            Total      = items.Length,
            Validated  = validated,
            Failed     = failed,
            CacheHits  = cacheHits,
            CacheMisses = cacheMisses,
            DurationMs = sw.ElapsedMilliseconds,
        };

        return new ValidateBatchResponse { Results = results, Summary = summary };
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task<(ValidationResponse? Response, string Source)> LookupCacheAsync(
        AddressInput input,
        CancellationToken ct)
    {
        try
        {
            var key = input.GenerateCacheKey();
            var result = await _cache.GetAsync(key, async (_, token) =>
            {
                // Return null here — provider is handled separately in batch
                await Task.CompletedTask;
                return null;
            }, ct);

            if (result.Value is null) return (null, "PROVIDER");

            var source = NormaliseSource(result.Source?.Source ?? "PROVIDER");
            return (result.Value, source);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache lookup failed for address; treating as miss.");
            return (null, "PROVIDER");
        }
    }

    private async Task WriteToCacheAsync(string key, ValidationResponse value, CancellationToken ct)
    {
        try
        {
            await _cache.SetAsync(key, value, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Write-through to cache failed for key {Key}.", key);
        }
    }


    private async Task EmitFailedEventAsync(
        string hash,
        string reason,
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
                HttpStatusCode       = null,
                ProviderName         = _provider.ProviderName,
                RequestCorrelationId = correlationId
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to emit AddressValidationFailed audit event.");
        }
    }

    private static string NormaliseSource(string raw) => raw switch
    {
        var s when s.StartsWith("L1", StringComparison.OrdinalIgnoreCase) => "L1",
        var s when s.StartsWith("L2", StringComparison.OrdinalIgnoreCase) => "L2",
        _ => "PROVIDER"
    };
}
