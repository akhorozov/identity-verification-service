namespace AddressValidation.Api.Infrastructure.Metrics;

using Prometheus;

/// <summary>
/// Singleton holder for all application-level Prometheus metrics (FR-006).
/// All metrics are pre-created at startup so they appear in /metrics even before
/// any requests arrive.
/// </summary>
public sealed class AppMetrics
{
    // ---------------------------------------------------------------------------
    // SRS FR-006: address_validation_requests_total
    // Labels: endpoint, status, api_version
    // ---------------------------------------------------------------------------

    /// <summary>Total address validation requests, labelled by endpoint, HTTP status, and API version.</summary>
    public Counter ValidationRequestsTotal { get; } =
        Metrics.CreateCounter(
            "address_validation_requests_total",
            "Total number of address validation requests processed.",
            labelNames: ["endpoint", "status", "api_version"]);

    // ---------------------------------------------------------------------------
    // SRS FR-006: address_validation_duration_seconds
    // Labels: endpoint, cache_source
    // Buckets: 10ms → 5s (SRS #94)
    // ---------------------------------------------------------------------------

    /// <summary>Validation request duration histogram, labelled by endpoint and cache source.</summary>
    public Histogram ValidationDurationSeconds { get; } =
        Metrics.CreateHistogram(
            "address_validation_duration_seconds",
            "Duration of address validation requests in seconds.",
            new HistogramConfiguration
            {
                LabelNames = ["endpoint", "cache_source"],
                // Buckets from 10ms to 5s as required by SRS acceptance criteria
                Buckets = [0.010, 0.025, 0.050, 0.100, 0.200, 0.400, 0.800, 1.5, 3.0, 5.0],
            });

    // ---------------------------------------------------------------------------
    // SRS FR-006: cache_hit_ratio
    // Labels: cache_tier
    // ---------------------------------------------------------------------------

    /// <summary>Current cache hit ratio (0.0–1.0) per cache tier (L1-Redis, L2-CosmosDB).</summary>
    public Gauge CacheHitRatio { get; } =
        Metrics.CreateGauge(
            "cache_hit_ratio",
            "Current cache hit ratio (0.0-1.0) per cache tier.",
            labelNames: ["cache_tier"]);

    // ---------------------------------------------------------------------------
    // SRS FR-006: smarty_api_calls_total
    // Labels: status_code
    // ---------------------------------------------------------------------------

    /// <summary>Total calls made to the Smarty API, labelled by HTTP status code.</summary>
    public Counter SmartyApiCallsTotal { get; } =
        Metrics.CreateCounter(
            "smarty_api_calls_total",
            "Total number of calls made to the Smarty address validation API.",
            labelNames: ["status_code"]);

    // ---------------------------------------------------------------------------
    // SRS FR-006: smarty_api_errors_total
    // Labels: error_type
    // ---------------------------------------------------------------------------

    /// <summary>Total errors from the Smarty API, labelled by error type.</summary>
    public Counter SmartyApiErrorsTotal { get; } =
        Metrics.CreateCounter(
            "smarty_api_errors_total",
            "Total number of errors returned by the Smarty address validation API.",
            labelNames: ["error_type"]);

    // ---------------------------------------------------------------------------
    // SRS FR-006: active_circuit_breakers
    // Labels: provider
    // ---------------------------------------------------------------------------

    /// <summary>Number of currently open circuit breakers, labelled by provider name.</summary>
    public Gauge ActiveCircuitBreakers { get; } =
        Metrics.CreateGauge(
            "active_circuit_breakers",
            "Number of currently open circuit breakers per provider.",
            labelNames: ["provider"]);
}
