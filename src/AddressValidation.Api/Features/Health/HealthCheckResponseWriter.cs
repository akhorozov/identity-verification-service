using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AddressValidation.Api.Features.Health;

/// <summary>
/// Writes a structured JSON health response matching the SRS FR-005 schema:
/// <c>{ status, checks: [ { name, status, duration, description } ] }</c>
/// </summary>
public static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Writes the <see cref="HealthReport"/> as structured JSON to the HTTP response.
    /// Sets <c>Content-Type: application/json</c> and the appropriate status code.
    /// </summary>
    public static Task WriteResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var response = new HealthResponse(
            Status: report.Status.ToString(),
            TotalDuration: Math.Round(report.TotalDuration.TotalMilliseconds, 2),
            Checks: report.Entries.Select(e => new HealthCheckEntry(
                Name: e.Key,
                Status: e.Value.Status.ToString(),
                DurationMs: Math.Round(e.Value.Duration.TotalMilliseconds, 2),
                Description: e.Value.Description,
                Exception: e.Value.Exception?.Message
            )).ToList()
        );

        return context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }

    private sealed record HealthResponse(
        string Status,
        double TotalDuration,
        IReadOnlyList<HealthCheckEntry> Checks);

    private sealed record HealthCheckEntry(
        string Name,
        string Status,
        double DurationMs,
        string? Description,
        string? Exception);
}
