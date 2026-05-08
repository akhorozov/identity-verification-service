using Serilog.Core;
using Serilog.Events;

namespace AddressValidation.Api.Infrastructure.Logging;

/// <summary>
/// Serilog destructuring policy that sanitizes PII fields before they
/// reach any log sink (issue #102 / NFR-018).
/// Replaces the values of known PII properties with a masked placeholder
/// so that raw addresses, names, and similar data never appear in logs.
/// </summary>
public sealed class PiiSanitizingDestructuringPolicy : IDestructuringPolicy
{
    private static readonly HashSet<string> PiiPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "street",
        "street1",
        "street2",
        "address",
        "address1",
        "address2",
        "city",
        "state",
        "zipcode",
        "zip",
        "zip_code",
        "plus4",
        "deliveryline1",
        "deliveryline2",
        "lastline",
        "fulladdress",
        "rawaddress",
        "inputaddress",
        "validatedaddress",
    };

    /// <inheritdoc />
    public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory, out LogEventPropertyValue result)
    {
        result = null!;
        return false;
    }
}

/// <summary>
/// Serilog enricher that masks PII property values on structured log events.
/// Applied at the enricher level so that it catches both directly-logged
/// properties and those added by <see cref="PiiSanitizingDestructuringPolicy"/>.
/// </summary>
public sealed class PiiSanitizingEnricher : ILogEventEnricher
{
    private static readonly HashSet<string> PiiPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "street",
        "street1",
        "street2",
        "address",
        "address1",
        "address2",
        "city",
        "state",
        "zipcode",
        "zip",
        "zip_code",
        "plus4",
        "deliveryline1",
        "deliveryline2",
        "lastline",
        "fulladdress",
        "rawaddress",
        "inputaddress",
        "validatedaddress",
    };

    private const string Mask = "***";

    /// <inheritdoc />
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        // Properties dictionary is case-sensitive; iterate and check each key
        var keysToMask = logEvent.Properties.Keys
            .Where(k => PiiPropertyNames.Contains(k))
            .ToList();

        foreach (var key in keysToMask)
        {
            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(key, Mask));
        }
    }
}
