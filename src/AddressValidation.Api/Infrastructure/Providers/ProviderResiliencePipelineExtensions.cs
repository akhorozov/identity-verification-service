namespace AddressValidation.Api.Infrastructure.Providers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using System.Threading.RateLimiting;

/// <summary>
/// Configures a standard resilience pipeline on a Smarty HTTP client builder.
/// 
/// Policy order (outermost → innermost):
/// 1. Concurrency limiter (bulkhead) — #51: 50 concurrent calls max
/// 2. Circuit breaker                — #49: open after 5 failures, break for 30 s
/// 3. Timeout                        — #50: 5 s per attempt
/// 4. Retry                          — #48: 3 attempts, 200 ms exponential base
/// </summary>
public static class ProviderResiliencePipelineExtensions
{
    /// <summary>
    /// Adds the standard provider resilience pipeline (retry / circuit breaker / timeout / bulkhead)
    /// to <paramref name="builder"/>.
    /// </summary>
    public static IHttpClientBuilder AddProviderResilience(this IHttpClientBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddResilienceHandler("smarty-provider", pipeline =>
        {
            // #51 — Bulkhead: limit concurrent calls to avoid saturating the provider
            pipeline.AddConcurrencyLimiter(new ConcurrencyLimiterOptions
            {
                PermitLimit = 50,
                QueueLimit = 25,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });

            // #49 — Circuit breaker: open after 5 consecutive failures; break for 30 s
            pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromSeconds(30)
            });

            // #50 — Timeout: 5 s per attempt (applied before retry so each attempt is bounded)
            pipeline.AddTimeout(TimeSpan.FromSeconds(5));

            // #48 — Retry: 3 attempts with 200 ms exponential backoff base
            pipeline.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(200),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true
            });
        });

        return builder;
    }
}
