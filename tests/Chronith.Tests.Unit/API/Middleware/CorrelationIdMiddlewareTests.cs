using Chronith.API.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace Chronith.Tests.Unit.API.Middleware;

public sealed class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenCorrelationIdHeaderPresent_StoresItInItemsAndSetsResponseHeader()
    {
        // Arrange
        var existingCorrelationId = "test-correlation-id-123";
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = existingCorrelationId;

        string? capturedItem = null;
        var middleware = new CorrelationIdMiddleware(ctx =>
        {
            capturedItem = ctx.Items[CorrelationIdMiddleware.ItemKey] as string;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedItem.Should().Be(existingCorrelationId);
        context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString()
            .Should().Be(existingCorrelationId);
    }

    [Fact]
    public async Task InvokeAsync_WhenCorrelationIdHeaderAbsent_GeneratesNewGuidAndSetsOnItemsAndResponseHeader()
    {
        // Arrange
        var context = new DefaultHttpContext();

        string? capturedItem = null;
        var middleware = new CorrelationIdMiddleware(ctx =>
        {
            capturedItem = ctx.Items[CorrelationIdMiddleware.ItemKey] as string;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedItem.Should().NotBeNullOrEmpty();
        Guid.TryParse(capturedItem, out _).Should().BeTrue("generated ID should be a valid GUID");

        var responseHeader = context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        responseHeader.Should().Be(capturedItem);
    }
}
