namespace AddressValidation.Api.Infrastructure.Telemetry;

using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

/// <summary>
/// Extension methods for wiring up OpenTelemetry tracing and metrics (T12 / issue #13).
/// </summary>
public static class ObservabilityExtensions
{
    /// <summary>
    /// Registers OpenTelemetry tracing and metrics for the Address Validation API.
    /// Reads configuration from the <c>OpenTelemetry</c> section:
    /// <list type="bullet">
    ///   <item><c>OpenTelemetry:Enabled</c> — master toggle.</item>
    ///   <item><c>OpenTelemetry:Tracing:Enabled</c> — enables distributed tracing.</item>
    ///   <item><c>OpenTelemetry:Metrics:Enabled</c> — enables OTel metric pipeline.</item>
    ///   <item><c>OpenTelemetry:Otlp:Endpoint</c> — OTLP gRPC/HTTP exporter endpoint (optional).</item>
    ///   <item><c>OpenTelemetry:AzureMonitor:Enabled</c> — enables Azure Monitor exporter (optional).</item>
    /// </list>
    /// </summary>
    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (!configuration.GetValue<bool>("OpenTelemetry:Enabled"))
            return services;

        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(
                serviceName: AddressValidationActivitySource.ServiceName,
                serviceVersion: AddressValidationActivitySource.ServiceVersion)
            .AddAttributes([
                new KeyValuePair<string, object>("deployment.environment",
                    configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production")
            ]);

        var otlpEndpoint = configuration["OpenTelemetry:Otlp:Endpoint"];
        var azureMonitorEnabled = configuration.GetValue<bool>("OpenTelemetry:AzureMonitor:Enabled");

        var otelBuilder = services.AddOpenTelemetry()
            .ConfigureResource(rb => rb
                .AddService(
                    serviceName: AddressValidationActivitySource.ServiceName,
                    serviceVersion: AddressValidationActivitySource.ServiceVersion)
                .AddAttributes([
                    new KeyValuePair<string, object>("deployment.environment",
                        configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production")
                ]));

        // ── Tracing ───────────────────────────────────────────────────────
        if (configuration.GetValue<bool>("OpenTelemetry:Tracing:Enabled"))
        {
            otelBuilder.WithTracing(tracing =>
            {
                tracing
                    .AddSource(AddressValidationActivitySource.ActivitySource.Name)
                    .AddAspNetCoreInstrumentation(opts =>
                    {
                        // Record exception details on spans
                        opts.RecordException = true;
                        // Exclude health/metrics endpoints from traces (noise)
                        opts.Filter = ctx =>
                            !ctx.Request.Path.StartsWithSegments("/health") &&
                            !ctx.Request.Path.StartsWithSegments("/metrics");
                    })
                    .AddHttpClientInstrumentation(opts =>
                    {
                        opts.RecordException = true;
                        // Suppress Smarty auth header from span attributes
                        opts.FilterHttpRequestMessage = req =>
                            req.RequestUri?.Host?.Contains("smarty", StringComparison.OrdinalIgnoreCase) == true
                            || req.RequestUri?.IsLoopback == false;
                    });

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                    tracing.AddOtlpExporter(opts => opts.Endpoint = new Uri(otlpEndpoint));

                if (configuration.GetValue<bool>("OpenTelemetry:Console:Enabled"))
                    tracing.AddConsoleExporter();
            });
        }

        // ── Metrics ───────────────────────────────────────────────────────
        if (configuration.GetValue<bool>("OpenTelemetry:Metrics:Enabled"))
        {
            otelBuilder.WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();

                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                    metrics.AddOtlpExporter(opts => opts.Endpoint = new Uri(otlpEndpoint));

                if (configuration.GetValue<bool>("OpenTelemetry:Console:Enabled"))
                    metrics.AddConsoleExporter();
            });
        }

        // ── Azure Monitor ─────────────────────────────────────────────────
        if (azureMonitorEnabled)
        {
            // UseAzureMonitor wires both tracing and metrics to Application Insights.
            otelBuilder.UseAzureMonitor();
        }

        return services;
    }
}
