using Microsoft.AspNetCore.Mvc;

namespace AddressValidation.Api.Infrastructure.Middleware;

/// <summary>
/// Global exception handling middleware. Returns RFC 7807 ProblemDetails
/// with <c>application/problem+json</c> content type on all unhandled exceptions.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IConfiguration _configuration;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);

            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;

            var isDevelopment = _configuration.GetValue<bool>("AddressValidation:EnableDetailedErrors");
            var correlationId = context.Items.TryGetValue("CorrelationId", out var cid) ? cid?.ToString() : null;

            var problem = new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                Title = "An unexpected error occurred.",
                Status = StatusCodes.Status500InternalServerError,
                Detail = isDevelopment ? exception.Message : null,
                Instance = context.Request.Path,
            };
            problem.Extensions["traceId"] = context.TraceIdentifier;
            if (correlationId is not null)
                problem.Extensions["correlationId"] = correlationId;
            if (isDevelopment)
                problem.Extensions["exceptionType"] = exception.GetType().Name;

            await context.Response.WriteAsJsonAsync(problem);
        }
    }
}
