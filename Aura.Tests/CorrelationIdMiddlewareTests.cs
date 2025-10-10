using Aura.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Aura.Tests;

/// <summary>
/// Unit tests for CorrelationIdMiddleware
/// Tests correlation ID generation, header propagation, and logging context
/// </summary>
public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task Middleware_Should_GenerateCorrelationId_WhenNotProvided()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var middleware = new CorrelationIdMiddleware(next: (innerContext) => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(context.Response.Headers.ContainsKey("X-Correlation-ID"));
        var correlationId = context.Response.Headers["X-Correlation-ID"].ToString();
        Assert.False(string.IsNullOrEmpty(correlationId));
        Assert.True(Guid.TryParse(correlationId, out _));
    }

    [Fact]
    public async Task Middleware_Should_PreserveCorrelationId_WhenProvided()
    {
        // Arrange
        var expectedCorrelationId = Guid.NewGuid().ToString();
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-ID"] = expectedCorrelationId;
        var middleware = new CorrelationIdMiddleware(next: (innerContext) => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(context.Response.Headers.ContainsKey("X-Correlation-ID"));
        var actualCorrelationId = context.Response.Headers["X-Correlation-ID"].ToString();
        Assert.Equal(expectedCorrelationId, actualCorrelationId);
    }

    [Fact]
    public async Task Middleware_Should_CallNextDelegate()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var nextCalled = false;
        var middleware = new CorrelationIdMiddleware(next: (innerContext) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Middleware_Should_AddCorrelationIdToResponse_BeforeCallingNext()
    {
        // Arrange
        var context = new DefaultHttpContext();
        string? correlationIdInNext = null;
        var middleware = new CorrelationIdMiddleware(next: (innerContext) =>
        {
            // Check if correlation ID is available in response headers
            correlationIdInNext = innerContext.Response.Headers["X-Correlation-ID"].ToString();
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.NotNull(correlationIdInNext);
        Assert.False(string.IsNullOrEmpty(correlationIdInNext));
    }

    [Fact]
    public async Task Middleware_Should_GenerateUniqueCorrelationIds_ForDifferentRequests()
    {
        // Arrange
        var middleware = new CorrelationIdMiddleware(next: (innerContext) => Task.CompletedTask);
        var context1 = new DefaultHttpContext();
        var context2 = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(context1);
        await middleware.InvokeAsync(context2);

        // Assert
        var correlationId1 = context1.Response.Headers["X-Correlation-ID"].ToString();
        var correlationId2 = context2.Response.Headers["X-Correlation-ID"].ToString();
        Assert.NotEqual(correlationId1, correlationId2);
    }
}
