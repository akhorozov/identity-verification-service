namespace AddressValidation.Tests.Unit.Infrastructure.Middleware;

using System.Net;
using System.Text.Json;
using AddressValidation.Api.Infrastructure.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

public class ApiVersionRequiredMiddlewareTests
{
    private static DefaultHttpContext MakeContext(string path, string? apiVersion = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        if (apiVersion is not null)
            context.Request.Headers["Api-Version"] = apiVersion;
        return context;
    }

    [Theory]
    [InlineData("/api/addresses/validate")]
    [InlineData("/api/addresses/validate/batch")]
    [InlineData("/api/cache/flush")]
    public async Task InvokeAsync_ApiPath_MissingHeader_Returns400(string path)
    {
        var middleware = new ApiVersionRequiredMiddleware(_ => Task.CompletedTask);
        var context = MakeContext(path);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
    }

    [Theory]
    [InlineData("/api/addresses/validate", "1.0")]
    [InlineData("/api/addresses/validate/batch", "2.0")]
    public async Task InvokeAsync_ApiPath_WithHeader_PassesThrough(string path, string version)
    {
        var called = false;
        var middleware = new ApiVersionRequiredMiddleware(_ => { called = true; return Task.CompletedTask; });
        var context = MakeContext(path, version);

        await middleware.InvokeAsync(context);

        Assert.True(called);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    [InlineData("/metrics")]
    [InlineData("/swagger/index.html")]
    public async Task InvokeAsync_ExcludedPath_NoHeader_PassesThrough(string path)
    {
        var called = false;
        var middleware = new ApiVersionRequiredMiddleware(_ => { called = true; return Task.CompletedTask; });
        var context = MakeContext(path);

        await middleware.InvokeAsync(context);

        Assert.True(called);
    }

    [Fact]
    public async Task InvokeAsync_ApiPath_EmptyHeader_Returns400()
    {
        var middleware = new ApiVersionRequiredMiddleware(_ => Task.CompletedTask);
        var context = MakeContext("/api/addresses/validate", "   ");

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ApiPath_MissingHeader_ResponseBodyContainsProblemDetails()
    {
        var middleware = new ApiVersionRequiredMiddleware(_ => Task.CompletedTask);
        var context = MakeContext("/api/addresses/validate");

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var problem = JsonSerializer.Deserialize<ProblemDetails>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Contains("Api-Version", problem.Detail ?? "");
    }

    [Fact]
    public async Task Constructor_NullNext_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ApiVersionRequiredMiddleware(null!));
    }
}
