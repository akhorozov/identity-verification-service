namespace AddressValidation.Api.Infrastructure.Authentication;

/// <summary>
/// Names of supported API key roles and their associated authorization policy names.
/// </summary>
public static class ApiKeyAuthorizationPolicy
{
    /// <summary>Policy name for endpoints accessible by any authenticated API key.</summary>
    public const string ReadOnly = "ApiKeyReadOnly";

    /// <summary>Policy name for endpoints that require the <c>admin</c> role.</summary>
    public const string Admin = "ApiKeyAdmin";
}
