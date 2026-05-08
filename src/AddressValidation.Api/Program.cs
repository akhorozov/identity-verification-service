using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Net.Security;
using System.Security.Authentication;
using AddressValidation.Api.Features.Validation.ValidateSingle;
using AddressValidation.Api.Features.Validation.ValidateBatch;
using AddressValidation.Api.Features.Cache;
using AddressValidation.Api.Features.Health;
using AddressValidation.Api.Infrastructure;
using AddressValidation.Api.Infrastructure.Metrics;
using FluentValidation;
using AddressValidation.Api.Infrastructure.Configuration;
using AddressValidation.Api.Infrastructure.Middleware;
using AddressValidation.Api.Infrastructure.Logging;
using Serilog;
using Asp.Versioning;
using FluentValidation.AspNetCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// ==================== TLS Enforcement ====================
// NFR-016: Enforce minimum TLS 1.2 for all Kestrel HTTPS connections
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ConfigureHttpsDefaults(httpsOptions =>
    {
        httpsOptions.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
        httpsOptions.OnAuthenticate = (context, sslOptions) =>
        {
            sslOptions.EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
        };
    });
});

// ==================== Configuration Setup ====================
// Load configuration from appsettings
var configuration = builder.Configuration;

// Add Azure Key Vault if enabled
if (configuration.GetValue<bool>("AzureKeyVault:Enabled"))
{
    configuration.AddAzureKeyVault(builder);
}

// ==================== Serilog Setup ====================
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .Enrich.WithProperty("Application", "AddressValidation.Api")
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
    .Enrich.With<PiiSanitizingEnricher>()
    .CreateLogger();

builder.Host.UseSerilog();

try
{
    // ==================== Services Registration ====================

    // Add API versioning
    builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true; // Defaults to v1.0 when Api-Version header is omitted
        options.ReportApiVersions = true;
        options.ApiVersionReader = new HeaderApiVersionReader("Api-Version");
    });

    // Add CORS
    var corsPolicy = configuration.GetSection("Security:Cors");
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("default", policy =>
        {
            policy
                .WithOrigins(corsPolicy.GetSection("AllowedOrigins").Get<string[]>() ?? [])
                .WithMethods(corsPolicy.GetSection("AllowedMethods").Get<string[]>() ?? [])
                .WithHeaders(corsPolicy.GetSection("AllowedHeaders").Get<string[]>() ?? [])
                .WithExposedHeaders("api-version")
                .SetPreflightMaxAge(TimeSpan.FromSeconds(corsPolicy.GetValue<int>("MaxAgeInSeconds", 3600)));

            if (corsPolicy.GetValue<bool>("AllowCredentials"))
            {
                policy.AllowCredentials();
            }
        });
    });

    // Add rate limiting
    var rateLimitConfig = configuration.GetSection("Security:RateLimiting");
    if (rateLimitConfig.GetValue<bool>("Enabled"))
    {
        var permitLimit = rateLimitConfig.GetValue<int>("PermitLimit", 1000);
        var windowSeconds = rateLimitConfig.GetValue<int>("WindowSizeInSeconds", 60);
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (ctx, ct) =>
            {
                var window = TimeSpan.FromSeconds(windowSeconds);
                var resetAt = DateTimeOffset.UtcNow.Add(window);
                ctx.HttpContext.Response.Headers["Retry-After"] = window.TotalSeconds.ToString("0");
                ctx.HttpContext.Response.Headers["X-RateLimit-Limit"] = permitLimit.ToString();
                ctx.HttpContext.Response.Headers["X-RateLimit-Remaining"] = "0";
                ctx.HttpContext.Response.Headers["X-RateLimit-Reset"] = resetAt.ToUnixTimeSeconds().ToString();
                ctx.HttpContext.Response.ContentType = "application/problem+json";
                await ctx.HttpContext.Response.WriteAsJsonAsync(new
                {
                    type = "https://tools.ietf.org/html/rfc6585#section-4",
                    title = "Too Many Requests",
                    status = StatusCodes.Status429TooManyRequests,
                    detail = $"Rate limit of {permitLimit} requests per {windowSeconds}s exceeded. Retry after {window.TotalSeconds}s.",
                    retryAfter = (int)window.TotalSeconds,
                }, ct);
            };
            options.AddFixedWindowLimiter("fixed", opt =>
            {
                opt.PermitLimit = permitLimit;
                opt.Window = TimeSpan.FromSeconds(windowSeconds);
                opt.QueueLimit = rateLimitConfig.GetValue<int>("QueueLimit", 5);
                opt.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
            });
        });
    }

    // Add health checks (FR-005 / T9)
    builder.Services.AddAppHealthChecks(configuration);

    // RFC 7807 ProblemDetails support
    builder.Services.AddProblemDetails();

    // Add FluentValidation
    builder.Services
        .AddFluentValidationAutoValidation()
        .AddFluentValidationClientsideAdapters();

    // Add OpenTelemetry
    if (configuration.GetValue<bool>("OpenTelemetry:Enabled"))
    {
        builder.Services.AddOpenTelemetry()
            .WithTracing(tracerProvider =>
            {
                if (configuration.GetValue<bool>("OpenTelemetry:Tracing:Enabled"))
                {
                    tracerProvider
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation();
                }
            })
            .WithMetrics(meterProvider =>
            {
                if (configuration.GetValue<bool>("OpenTelemetry:Metrics:Enabled"))
                {
                    meterProvider
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation();
                }
            });
    }

    // Add infrastructure services
    builder.Services.AddInfrastructure(configuration);
    builder.Services.AddProviders(configuration);
    builder.Services.AddAuditEventStore(configuration);
    builder.Services.AddValidationCaching(configuration);
    builder.Services.AddCacheManagement(configuration);
    builder.Services.AddApiKeyAuthentication();

    // Register ValidateSingle feature
    builder.Services.AddScoped<ValidateSingleHandler>();
    builder.Services.AddScoped<IValidator<ValidateSingleRequest>, ValidateSingleRequestValidator>();

    // Register ValidateBatch feature
    builder.Services.AddScoped<ValidateBatchHandler>();
    builder.Services.AddScoped<IValidator<ValidateBatchRequest>, ValidateBatchRequestValidator>();

    // Register Cache Management feature (T8 / FR-003)
    builder.Services.AddSingleton<CacheStatsHandler>();
    builder.Services.AddScoped<InvalidateCacheHandler>();
    builder.Services.AddScoped<FlushCacheHandler>();

    // Register Prometheus metrics (T10 / FR-006)
    builder.Services.AddAppMetrics();

    // Add controllers and minimal APIs
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    // ==================== Middleware Pipeline ====================

    // Security headers
    if (configuration.GetValue<bool>("Security:SecurityHeaders:EnableHSTS"))
    {
        app.UseHsts();
    }

    app.UseHttpsRedirection();

    // Add security headers middleware
    app.UseMiddleware<SecurityHeadersMiddleware>();

    // Add correlation ID middleware
    app.UseMiddleware<CorrelationIdMiddleware>();

    // Add exception handling middleware
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    // Enforce Api-Version header on all /api/* paths (NFR-015 / issue #99)
    app.UseMiddleware<ApiVersionRequiredMiddleware>();

    // Add Sunset header for deprecated API versions (RFC 8594 / issue #101)
    app.UseMiddleware<SunsetHeaderMiddleware>();

    // OpenAPI/Swagger
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "AddressValidation.Api v1");
        });
    }

    // CORS
    app.UseCors("default");

    // Rate limiting
    if (configuration.GetValue<bool>("Security:RateLimiting:Enabled"))
    {
        app.UseRateLimiter();
    }

    // Logging
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("CorrelationId", httpContext.Items["CorrelationId"]);
            diagnosticContext.Set("RequestId", httpContext.TraceIdentifier);
            diagnosticContext.Set("UserId", httpContext.User?.Identity?.Name ?? "anonymous");
        };
    });

    // Routing
    app.UseRouting();

    // Prometheus HTTP metrics middleware (FR-006) — records validation counters/histograms
    app.UseMiddleware<MetricsMiddleware>();

    // Authentication + Authorization
    app.UseAuthentication();
    app.UseAuthorization();

    // Health check probes (FR-005) — no auth required (issue #84)
    var healthCheckOptions = new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        ResponseWriter = HealthCheckResponseWriter.WriteResponse,
    };

    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("live"),
        ResultStatusCodes =
        {
            [HealthStatus.Healthy] = StatusCodes.Status200OK,
            [HealthStatus.Degraded] = StatusCodes.Status200OK,
            [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
        },
        ResponseWriter = HealthCheckResponseWriter.WriteResponse,
    }).AllowAnonymous();

    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
        ResultStatusCodes =
        {
            [HealthStatus.Healthy] = StatusCodes.Status200OK,
            [HealthStatus.Degraded] = StatusCodes.Status200OK,
            [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
        },
        ResponseWriter = HealthCheckResponseWriter.WriteResponse,
    }).AllowAnonymous();

    app.MapHealthChecks("/health/startup", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("startup"),
        ResultStatusCodes =
        {
            [HealthStatus.Healthy] = StatusCodes.Status200OK,
            [HealthStatus.Degraded] = StatusCodes.Status200OK,
            [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
        },
        ResponseWriter = HealthCheckResponseWriter.WriteResponse,
    }).AllowAnonymous();

    // Map controllers
    app.MapControllers();

    // Prometheus metrics endpoint (FR-006) — no auth required (SRS §9.3.6)
    // UseMetricServer registers GET /metrics returning Prometheus text exposition format
    app.UseMetricServer("/metrics");

    // Map feature endpoints
    app.MapValidateSingle();
    app.MapValidateBatch();

    // Map cache management endpoints (T8 / FR-003)
    // Flush must be registered before the parameterised {key} route to win routing.
    app.MapFlushCache();
    app.MapInvalidateCache();
    app.MapCacheStats();

    // ==================== Run Application ====================
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

