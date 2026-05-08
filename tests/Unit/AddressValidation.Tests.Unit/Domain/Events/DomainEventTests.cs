using AddressValidation.Api.Domain.Events;
using Xunit;

namespace AddressValidation.Tests.Unit.Domain.Events;

/// <summary>
/// Unit tests for T5 domain event types (SRS FR-004).
/// Verifies immutability, EventType discriminators, and PII-safety contract.
/// </summary>
public class DomainEventTests
{
    // ── DomainEvent base ──────────────────────────────────────────────────────

    [Fact]
    public void DomainEvent_EventId_IsAutoAssignedAsNonEmptyGuid()
    {
        var evt = new AddressValidated { AggregateId = "hash", AddressHash = "hash", DpvMatchCode = "Y", ProviderName = "P", CacheSource = "L1" };

        Assert.NotEmpty(evt.EventId);
        Assert.True(Guid.TryParse(evt.EventId, out _));
    }

    [Fact]
    public void DomainEvent_RequestDate_IsUtcAndRecent()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var evt = new AddressValidated { AggregateId = "hash", AddressHash = "hash", DpvMatchCode = "Y", ProviderName = "P", CacheSource = "L1" };
        var after = DateTimeOffset.UtcNow.AddSeconds(1);

        Assert.True(evt.RequestDate >= before && evt.RequestDate <= after);
        Assert.Equal(TimeSpan.Zero, evt.RequestDate.Offset);
    }

    [Fact]
    public void DomainEvent_ServiceVersion_HasDefaultValue()
    {
        var evt = new AddressValidated { AggregateId = "hash", AddressHash = "hash", DpvMatchCode = "Y", ProviderName = "P", CacheSource = "L1" };

        Assert.NotEmpty(evt.ServiceVersion);
    }

    // ── EventType discriminators ──────────────────────────────────────────────

    [Fact]
    public void AddressValidated_EventType_IsCorrect()
        => Assert.Equal("AddressValidated", new AddressValidated { AggregateId = "h", AddressHash = "h", DpvMatchCode = "Y", ProviderName = "P", CacheSource = "L1" }.EventType);

    [Fact]
    public void AddressValidationFailed_EventType_IsCorrect()
        => Assert.Equal("AddressValidationFailed", new AddressValidationFailed { AggregateId = "h", AddressHash = "h", FailureReason = "err" }.EventType);

    [Fact]
    public void CacheEntryCreated_EventType_IsCorrect()
        => Assert.Equal("CacheEntryCreated", new CacheEntryCreated { AggregateId = "h", CacheKey = "k", CacheLayer = "L1" }.EventType);

    [Fact]
    public void CacheEntryRetrieved_EventType_IsCorrect()
        => Assert.Equal("CacheEntryRetrieved", new CacheEntryRetrieved { AggregateId = "h", CacheKey = "k", CacheLayer = "L1" }.EventType);

    [Fact]
    public void CacheEntryInvalidated_EventType_IsCorrect()
        => Assert.Equal("CacheEntryInvalidated", new CacheEntryInvalidated { AggregateId = "h", CacheKey = "k", CacheLayers = [] }.EventType);

    [Fact]
    public void CacheFlushed_EventType_IsCorrect()
        => Assert.Equal("CacheFlushed", new CacheFlushed { AggregateId = "h", CacheLayers = [] }.EventType);

    [Fact]
    public void CircuitBreakerOpened_EventType_IsCorrect()
        => Assert.Equal("CircuitBreakerOpened", new CircuitBreakerOpened { AggregateId = "h", PolicyName = "p" }.EventType);

    [Fact]
    public void CircuitBreakerClosed_EventType_IsCorrect()
        => Assert.Equal("CircuitBreakerClosed", new CircuitBreakerClosed { AggregateId = "h", PolicyName = "p" }.EventType);

    // ── PII-safety contract ───────────────────────────────────────────────────

    [Fact]
    public void AddressValidated_DoesNotContainRawAddress()
    {
        const string rawAddress = "123 Main St Springfield IL 62701";
        var evt = new AddressValidated
        {
            AggregateId = "abc123hash",
            AddressHash = "abc123hash",
            DpvMatchCode = "Y",
            ProviderName = "Smarty",
            CacheSource = "PROVIDER"
        };

        var json = System.Text.Json.JsonSerializer.Serialize(evt);

        Assert.DoesNotContain(rawAddress, json);
        Assert.DoesNotContain("Main St", json);
    }

    [Fact]
    public void AllEventTypes_AggregateId_NeverContainsSpaces()
    {
        // AggregateId should always be a hash (no whitespace), never a raw address string.
        DomainEvent[] events =
        [
            new AddressValidated      { AggregateId = "abc123", AddressHash = "abc123", DpvMatchCode = "Y", ProviderName = "P", CacheSource = "L1" },
            new AddressValidationFailed { AggregateId = "abc123", AddressHash = "abc123", FailureReason = "err" },
            new CacheEntryCreated     { AggregateId = "abc123", CacheKey = "k", CacheLayer = "L1" },
            new CacheEntryRetrieved   { AggregateId = "abc123", CacheKey = "k", CacheLayer = "L1" },
            new CacheEntryInvalidated { AggregateId = "abc123", CacheKey = "k", CacheLayers = [] },
            new CacheFlushed          { AggregateId = "abc123", CacheLayers = [] },
            new CircuitBreakerOpened  { AggregateId = "abc123", PolicyName = "p" },
            new CircuitBreakerClosed  { AggregateId = "abc123", PolicyName = "p" },
        ];

        foreach (var evt in events)
            Assert.DoesNotContain(' ', evt.AggregateId);
    }

    // ── Payload properties ────────────────────────────────────────────────────

    [Fact]
    public void AddressValidated_Stores_ValidationMetadata()
    {
        var evt = new AddressValidated
        {
            AggregateId  = "hash1",
            AddressHash  = "hash1",
            DpvMatchCode = "Y",
            ProviderName = "Smarty",
            CacheSource  = "Redis",
            DurationMs   = 42
        };

        Assert.Equal("Y",      evt.DpvMatchCode);
        Assert.Equal("Smarty", evt.ProviderName);
        Assert.Equal("Redis",  evt.CacheSource);
        Assert.Equal(42,       evt.DurationMs);
    }

    [Fact]
    public void AddressValidationFailed_Stores_FailureDetails()
    {
        var evt = new AddressValidationFailed
        {
            AggregateId    = "hash2",
            AddressHash    = "hash2",
            FailureReason  = "Provider timeout",
            HttpStatusCode = 504,
            ProviderName   = "Smarty"
        };

        Assert.Equal("Provider timeout", evt.FailureReason);
        Assert.Equal(504,                evt.HttpStatusCode);
    }

    [Fact]
    public void CircuitBreakerOpened_Stores_ResilienceMetadata()
    {
        var evt = new CircuitBreakerOpened
        {
            AggregateId         = "hash3",
            PolicyName          = "SmartyPolicy",
            BreakDurationSeconds = 30,
            TriggerReason       = "5 consecutive failures"
        };

        Assert.Equal("SmartyPolicy",           evt.PolicyName);
        Assert.Equal(30,                       evt.BreakDurationSeconds);
        Assert.Equal("5 consecutive failures", evt.TriggerReason);
    }
}
