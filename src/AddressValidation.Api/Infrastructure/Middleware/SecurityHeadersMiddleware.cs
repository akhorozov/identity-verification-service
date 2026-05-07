namespace AddressValidation.Api.Infrastructure.Middleware;

/// <summary>
/// Middleware to add security headers to responses
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityHeadersMiddleware> _logger;

    public SecurityHeadersMiddleware(RequestDelegate next, ILogger<SecurityHeadersMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var response = context.Response;

        // Prevent MIME type sniffing
        response.Headers["X-Content-Type-Options"] = "nosniff";

        // Prevent clickjacking
        response.Headers["X-Frame-Options"] = "DENY";

        // Enable XSS protection
        response.Headers["X-XSS-Protection"] = "1; mode=block";

        // Referrer policy
        response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Permissions policy
        response.Headers["Permissions-Policy"] = "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";

        // Remove server header
        response.Headers.Remove("Server");
        response.Headers.Remove("X-Powered-By");

        await _next(context);
    }
}
