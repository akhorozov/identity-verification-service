using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Add configuration sources (including Azure Key Vault if configured)
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
  .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
  .AddEnvironmentVariables();

// Add Azure Key Vault configuration if enabled
if (builder.Configuration.GetValue<bool>("AzureKeyVault:Enabled"))
{
  var keyVaultUri = builder.Configuration["AzureKeyVault:VaultUri"];
  if (!string.IsNullOrEmpty(keyVaultUri))
  {
    builder.Configuration.AddAzureKeyVault(
      new Uri(keyVaultUri),
      new DefaultAzureCredential()
    );
  }
}

// Add Serilog logging
builder.Host.UseSerilog((context, loggerConfig) =>
{
  loggerConfig
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithCorrelationIdHeader("X-Correlation-ID");
});

// Add YARP reverse proxy
builder.Services.AddReverseProxy()
  .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Add CORS
builder.Services.AddCors(options =>
{
  options.AddPolicy("AllowAll", policy =>
  {
    policy.AllowAnyOrigin()
      .AllowAnyMethod()
      .AllowAnyHeader();
  });
});

// Add health checks
builder.Services.AddHealthChecks()
  .AddCheck("gateway", () => HealthCheckResult.Healthy("Gateway is healthy"));

// Add OpenTelemetry
builder.Services.AddOpenTelemetry()
  .WithTracing(tracing => tracing
    .AddAspNetCoreInstrumentation()
    .AddHttpClientInstrumentation()
    .AddConsoleExporter())
  .WithMetrics(metrics => metrics
    .AddAspNetCoreInstrumentation()
    .AddConsoleExporter());

var app = builder.Build();

// Configure middleware
if (app.Environment.IsDevelopment())
{
  app.UseDeveloperExceptionPage();
}

// Add security headers
app.Use(async (context, next) =>
{
  context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
  context.Response.Headers.Append("X-Frame-Options", "DENY");
  context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
  context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
  await next();
});

// Use CORS
app.UseCors("AllowAll");

// Map health checks endpoint
app.MapHealthChecks("/health");

// Map YARP routes
app.MapReverseProxy();

// Redirect root to health endpoint
app.MapGet("/", () => Results.Redirect("/health"));

await app.RunAsync();
