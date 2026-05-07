namespace AddressValidation.Api.Infrastructure.Middleware;

/// <summary>
/// Middleware to add correlation ID to requests and responses
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;
    private readonly string _correlationIdHeaderName;

    public CorrelationIdMiddleware(
        RequestDelegate next,
        ILogger<CorrelationIdMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _correlationIdHeaderName = configuration.GetValue<string>("AddressValidation:CorrelationIdHeaderName") ?? "X-Correlation-ID";
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers
            .FirstOrDefault(h => h.Key == _correlationIdHeaderName)
            .Value
            .FirstOrDefault() ?? Guid.NewGuid().ToString();

        context.Items["CorrelationId"] = correlationId;

        context.Response.Headers.Append(_correlationIdHeaderName, correlationId);

        await _next(context);
    }
}
