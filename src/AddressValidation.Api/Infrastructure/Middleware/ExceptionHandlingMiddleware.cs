namespace AddressValidation.Api.Infrastructure.Middleware;

/// <summary>
/// Global exception handling middleware
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
            _logger.LogError(exception, "An unhandled exception occurred while processing the request");

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;

            var isDevelopment = _configuration.GetValue<bool>("AddressValidation:EnableDetailedErrors");
            var response = new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                title = "An error occurred processing your request",
                status = StatusCodes.Status500InternalServerError,
                traceId = context.TraceIdentifier,
                correlationId = context.Items.ContainsKey("CorrelationId") ? context.Items["CorrelationId"] : null,
                detail = isDevelopment ? exception.Message : null,
                exception = isDevelopment ? exception.GetType().Name : null
            };

            await context.Response.WriteAsJsonAsync(response);
        }
    }
}
