namespace AddressValidation.Tests.Unit.Infrastructure.Middleware;

using AddressValidation.Api.Infrastructure.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

public class SunsetHeaderMiddlewareTests
{
    private static IConfiguration MakeConfig(string? version = null, string? sunsetDate = null)
    {
        var data = new Dictionary<string, string?>();
        if (version is not null && sunsetDate is not null)
            data[$"Security:ApiSunset:{version}"] = sunsetDate;
        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
    }

    private static DefaultHttpContext MakeContext(string path, string? apiVersion = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        if (apiVersion is not null)
            context.Request.Headers["Api-Version"] = apiVersion;
        return context;
    }

    [Fact]
    public async Task InvokeAsync_WithSunsetConfig_AddsSunsetHeader()
    {
        var config = MakeConfig("1.0", "2026-12-31T00:00:00Z");
        var middleware = new SunsetHeaderMiddleware(_ => Task.CompletedTask, config);
        var context = MakeContext("/api/addresses/validate", "1.0");

        await middleware.InvokeAsync(context);

        Assert.True(context.Response.Headers.ContainsKey("Sunset"));
        Assert.Equal("true", context.Response.Headers["Deprecation"].ToString());
    }

    [Fact]
    public async Task InvokeAsync_NoSunsetConfig_DoesNotAddSunsetHeader()
    {
        var config = MakeConfig();
        var middleware = new SunsetHeaderMiddleware(_ => Task.CompletedTask, config);
        var context = MakeContext("/api/addresses/validate", "1.0");

        await middleware.InvokeAsync(context);

        Assert.False(context.Response.Headers.ContainsKey("Sunset"));
    }

    [Fact]
    public async Task InvokeAsync_NonApiPath_DoesNotAddSunsetHeader()
    {
        var config = MakeConfig("1.0", "2026-12-31T00:00:00Z");
        var middleware = new SunsetHeaderMiddleware(_ => Task.CompletedTask, config);
        var context = MakeContext("/health/live");

        await middleware.InvokeAsync(context);

        Assert.False(context.Response.Headers.ContainsKey("Sunset"));
    }

    [Fact]
    public async Task InvokeAsync_InvalidSunsetDate_DoesNotAddSunsetHeader()
    {
        var config = MakeConfig("1.0", "not-a-date");
        var middleware = new SunsetHeaderMiddleware(_ => Task.CompletedTask, config);
        var context = MakeContext("/api/addresses/validate", "1.0");

        await middleware.InvokeAsync(context);

        Assert.False(context.Response.Headers.ContainsKey("Sunset"));
    }

    [Fact]
    public void Constructor_NullNext_Throws()
    {
        var config = MakeConfig();
        Assert.Throws<ArgumentNullException>(() => new SunsetHeaderMiddleware(null!, config));
    }

    [Fact]
    public void Constructor_NullConfig_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new SunsetHeaderMiddleware(_ => Task.CompletedTask, null!));
    }
}
