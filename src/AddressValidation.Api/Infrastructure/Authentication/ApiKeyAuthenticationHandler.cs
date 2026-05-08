using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace AddressValidation.Api.Infrastructure.Authentication;

/// <summary>
/// Options for API key authentication.
/// </summary>
public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>Name of the HTTP header that carries the API key.</summary>
    public string HeaderName { get; set; } = "X-Api-Key";
}

/// <summary>
/// Authentication handler that validates API keys from the <c>Security:ApiKeys</c>
/// configuration section. Each key entry has a <c>Key</c> and an optional <c>Role</c>
/// (defaults to <c>readonly</c>; use <c>admin</c> for privileged access).
/// </summary>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly IConfiguration _configuration;

    /// <summary>The authentication scheme name registered for this handler.</summary>
    public const string SchemeName = "ApiKey";

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _configuration = configuration;
    }

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var headerName = Options.HeaderName;

        if (!Request.Headers.TryGetValue(headerName, out var apiKeyValues))
        {
            return Task.FromResult(AuthenticateResult.Fail($"Missing '{headerName}' header."));
        }

        var providedKey = apiKeyValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(providedKey))
        {
            return Task.FromResult(AuthenticateResult.Fail($"Empty '{headerName}' header."));
        }

        var apiKeySection = _configuration.GetSection("Security:ApiKeys");
        var keyEntries = apiKeySection.GetChildren().ToList();

        foreach (var entry in keyEntries)
        {
            var configuredKey = entry["Key"];
            if (string.IsNullOrWhiteSpace(configuredKey) || configuredKey != providedKey)
                continue;

            var role = entry["Role"] ?? "readonly";
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, entry["Name"] ?? "api-client"),
                new Claim(ClaimTypes.Role, role),
            };

            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
    }
}
