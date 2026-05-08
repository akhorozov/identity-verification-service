namespace AddressValidation.Api.Infrastructure.Telemetry;

using System.Diagnostics;
using System.Reflection;

/// <summary>
/// Centralised <see cref="ActivitySource"/> for all custom distributed-tracing
/// spans emitted by the Address Validation API.
/// </summary>
public static class AddressValidationActivitySource
{
    /// <summary>Service name used for OTel resource attribution.</summary>
    public const string ServiceName = "AddressValidation.Api";

    private static readonly AssemblyName AssemblyName = typeof(AddressValidationActivitySource).Assembly.GetName();

    /// <summary>Version derived from the assembly version at startup.</summary>
    public static readonly string ServiceVersion =
        AssemblyName.Version?.ToString() ?? "0.0.0";

    /// <summary>
    /// The shared <see cref="ActivitySource"/> instance.  Register its name
    /// with <c>AddSource(ActivitySource.Name)</c> when configuring the
    /// tracer provider.
    /// </summary>
    public static readonly ActivitySource ActivitySource =
        new(ServiceName, ServiceVersion);

    // ── Span names ────────────────────────────────────────────────────────

    /// <summary>Smarty external-API call span.</summary>
    public const string SmartyValidate = "smarty.validate";

    /// <summary>Cache read (L1 Redis → L2 CosmosDB) span.</summary>
    public const string CacheGet = "cache.get";

    /// <summary>Cache write span.</summary>
    public const string CacheSet = "cache.set";

    /// <summary>Cache invalidation span.</summary>
    public const string CacheInvalidate = "cache.invalidate";

    // ── Attribute keys ────────────────────────────────────────────────────

    /// <summary>Address street value (sanitised — no PII).</summary>
    public const string AttrAddressCity = "address.city";

    /// <summary>Address state (e.g. "CA").</summary>
    public const string AttrAddressState = "address.state";

    /// <summary>Validation provider name.</summary>
    public const string AttrProvider = "validation.provider";

    /// <summary>Cache tier hit (e.g. "redis", "cosmos", "miss").</summary>
    public const string AttrCacheTier = "cache.tier";

    /// <summary>DPV match code returned by the provider.</summary>
    public const string AttrDpvMatchCode = "validation.dpv_match_code";
}
