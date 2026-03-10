using Chronith.API.Middleware;
using Chronith.Application.Options;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Chronith.Tests.Unit.API.Middleware;

public sealed class SecurityHeadersMiddlewareTests
{
    private static SecurityHeadersMiddleware CreateMiddleware(CspOptions? cspOptions = null)
    {
        var options = Options.Create(cspOptions ?? new CspOptions());
        return new SecurityHeadersMiddleware(_ => Task.CompletedTask, options);
    }

    [Fact]
    public async Task InvokeAsync_SetsContentSecurityPolicyHeader()
    {
        // Arrange
        var csp = new CspOptions
        {
            DefaultSrc = "'self'",
            ScriptSrc = "'self'",
            StyleSrc = "'self' 'unsafe-inline'",
            ImgSrc = "'self' data: https:",
            ConnectSrc = "'self'",
            FrameAncestors = "'none'",
        };
        var middleware = CreateMiddleware(csp);
        var context = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var cspHeader = context.Response.Headers["Content-Security-Policy"].ToString();
        cspHeader.Should().Contain("default-src 'self'");
        cspHeader.Should().Contain("script-src 'self'");
        cspHeader.Should().Contain("style-src 'self' 'unsafe-inline'");
        cspHeader.Should().Contain("img-src 'self' data: https:");
        cspHeader.Should().Contain("connect-src 'self'");
        cspHeader.Should().Contain("frame-ancestors 'none'");
    }

    [Fact]
    public async Task InvokeAsync_SetsXContentTypeOptionsHeader()
    {
        // Arrange
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers["X-Content-Type-Options"].ToString()
            .Should().Be("nosniff");
    }

    [Fact]
    public async Task InvokeAsync_SetsXFrameOptionsHeader()
    {
        // Arrange
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers["X-Frame-Options"].ToString()
            .Should().Be("DENY");
    }

    [Fact]
    public async Task InvokeAsync_SetsStrictTransportSecurityHeader()
    {
        // Arrange
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers["Strict-Transport-Security"].ToString()
            .Should().Be("max-age=31536000; includeSubDomains");
    }

    [Fact]
    public async Task InvokeAsync_SetsReferrerPolicyHeader()
    {
        // Arrange
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers["Referrer-Policy"].ToString()
            .Should().Be("strict-origin-when-cross-origin");
    }

    [Fact]
    public async Task InvokeAsync_SetsPermissionsPolicyHeader()
    {
        // Arrange
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers["Permissions-Policy"].ToString()
            .Should().Be("camera=(), microphone=(), geolocation=()");
    }

    [Fact]
    public async Task InvokeAsync_CallsNextDelegate()
    {
        // Arrange
        var nextCalled = false;
        var options = Options.Create(new CspOptions());
        var middleware = new SecurityHeadersMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, options);
        var context = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
    }
}
