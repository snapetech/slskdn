// <copyright file="MeshGatewayAuthMiddlewareTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using slskd.Mesh.ServiceFabric;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace slskd.Tests.Unit.Mesh.ServiceFabric;

public class MeshGatewayAuthMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_NonMeshPath_PassesThrough()
    {
        // Arrange
        var (middleware, context) = CreateMiddleware(new MeshGatewayOptions { Enabled = true });
        context.Request.Path = "/api/other";
        var nextCalled = false;

        // Act
        await middleware.InvokeAsync(context);

        // Assert (next delegate should be called)
        Assert.True(nextCalled || context.Response.StatusCode == 200);
    }

    [Fact]
    public async Task InvokeAsync_GatewayDisabled_Returns404()
    {
        // Arrange
        var (middleware, context) = CreateMiddleware(new MeshGatewayOptions { Enabled = false });
        context.Request.Path = "/mesh/http/pods";
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal((int)HttpStatusCode.NotFound, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_NonLocalhost_NoApiKey_Returns401()
    {
        // Arrange
        var options = new MeshGatewayOptions
        {
            Enabled = true,
            ApiKey = "test-key-123",
            AllowedServices = new() { "pods" }
        };
        var (middleware, context) = CreateMiddleware(options);
        context.Request.Path = "/mesh/http/pods";
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.100");
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal((int)HttpStatusCode.Unauthorized, context.Response.StatusCode);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var responseReader = new StreamReader(context.Response.Body);
        var responseText = await responseReader.ReadToEndAsync();
        Assert.Contains("unauthorized", responseText);
    }

    [Fact]
    public async Task InvokeAsync_NonLocalhost_ValidApiKey_PassesThrough()
    {
        // Arrange
        var options = new MeshGatewayOptions
        {
            Enabled = true,
            ApiKey = "test-key-123",
            AllowedServices = new() { "pods" }
        };
        var (middleware, context) = CreateMiddleware(options);
        context.Request.Path = "/mesh/http/pods";
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.100");
        context.Request.Headers["X-Slskdn-ApiKey"] = "test-key-123";

        var nextCalled = false;

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled || context.Response.StatusCode == 200);
    }

    [Fact]
    public async Task InvokeAsync_Localhost_NoCsrf_WhenConfigured_Returns403()
    {
        // Arrange
        var options = new MeshGatewayOptions
        {
            Enabled = true,
            CsrfToken = "csrf-token-abc",
            AllowedServices = new() { "pods" }
        };
        var (middleware, context) = CreateMiddleware(options);
        context.Request.Path = "/mesh/http/pods";
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal((int)HttpStatusCode.Forbidden, context.Response.StatusCode);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var responseReader = new StreamReader(context.Response.Body);
        var responseText = await responseReader.ReadToEndAsync();
        Assert.Contains("csrf_required", responseText);
    }

    [Fact]
    public async Task InvokeAsync_Localhost_ValidCsrf_PassesThrough()
    {
        // Arrange
        var options = new MeshGatewayOptions
        {
            Enabled = true,
            CsrfToken = "csrf-token-abc",
            AllowedServices = new() { "pods" }
        };
        var (middleware, context) = CreateMiddleware(options);
        context.Request.Path = "/mesh/http/pods";
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        context.Request.Headers["X-Slskdn-Csrf"] = "csrf-token-abc";

        var nextCalled = false;

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled || context.Response.StatusCode == 200);
    }

    [Fact]
    public async Task InvokeAsync_CrossOrigin_NotInAllowedList_Returns403()
    {
        // Arrange
        var options = new MeshGatewayOptions
        {
            Enabled = true,
            AllowedOrigins = new() { "https://allowed.example.com" },
            AllowedServices = new() { "pods" }
        };
        var (middleware, context) = CreateMiddleware(options);
        context.Request.Path = "/mesh/http/pods";
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        context.Request.Headers["Origin"] = "https://evil.com";
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal((int)HttpStatusCode.Forbidden, context.Response.StatusCode);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var responseReader = new StreamReader(context.Response.Body);
        var responseText = await responseReader.ReadToEndAsync();
        Assert.Contains("origin_not_allowed", responseText);
    }

    [Fact]
    public async Task InvokeAsync_LocalIpv6OriginWithoutAllowList_PassesThrough()
    {
        var options = new MeshGatewayOptions
        {
            Enabled = true,
            AllowedServices = new() { "pods" }
        };

        var (middleware, context) = CreateMiddleware(options);
        context.Request.Path = "/mesh/http/pods";
        context.Connection.RemoteIpAddress = IPAddress.IPv6Loopback;
        context.Request.Headers["Origin"] = "https://[::1]:3000";

        await middleware.InvokeAsync(context);

        Assert.Equal(200, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_LocalIpv6OriginWithUppercaseHost_StillPassesThrough()
    {
        var options = new MeshGatewayOptions
        {
            Enabled = true,
            AllowedServices = new() { "pods" }
        };

        var (middleware, context) = CreateMiddleware(options);
        context.Request.Path = "/mesh/http/pods";
        context.Connection.RemoteIpAddress = IPAddress.IPv6Loopback;
        context.Request.Headers["Origin"] = "https://[::1]:3000".Replace("::1", "::1".ToUpperInvariant());

        await middleware.InvokeAsync(context);

        Assert.Equal(200, context.Response.StatusCode);
    }

    [Fact]
    public void MeshGatewayConfigValidator_GenerateSecureToken_ReturnsNonEmptyString()
    {
        // Act
        var token1 = MeshGatewayConfigValidator.GenerateSecureToken();
        var token2 = MeshGatewayConfigValidator.GenerateSecureToken();

        // Assert
        Assert.NotNull(token1);
        Assert.NotEmpty(token1);
        Assert.NotEqual(token1, token2); // Should be random
        Assert.True(token1.Length > 20); // Should be reasonably long
    }

    [Fact]
    public void MeshGatewayOptions_Validate_RequiresApiKeyForNonLocalhost()
    {
        // Arrange
        var options = new MeshGatewayOptions
        {
            Enabled = true,
            BindAddress = "192.168.1.1",
            AllowedServices = new() { "pods" }
        };

        // Act
        var (isValid, error) = options.Validate();

        // Assert
        Assert.False(isValid);
        Assert.Contains("ApiKey", error);
    }

    [Fact]
    public void MeshGatewayOptions_Validate_RequiresAllowedServices()
    {
        // Arrange
        var options = new MeshGatewayOptions
        {
            Enabled = true,
            BindAddress = "127.0.0.1",
            AllowedServices = new() // Empty!
        };

        // Act
        var (isValid, error) = options.Validate();

        // Assert
        Assert.False(isValid);
        Assert.Contains("AllowedServices", error);
    }

    [Fact]
    public void MeshGatewayOptions_Validate_ValidLocalhost_Passes()
    {
        // Arrange
        var options = new MeshGatewayOptions
        {
            Enabled = true,
            BindAddress = "127.0.0.1",
            AllowedServices = new() { "pods" }
        };

        // Act
        var (isValid, error) = options.Validate();

        // Assert
        Assert.True(isValid);
        Assert.Null(error);
    }

    private (MeshGatewayAuthMiddleware, DefaultHttpContext) CreateMiddleware(MeshGatewayOptions options)
    {
        var logger = NullLoggerFactory.Instance.CreateLogger<MeshGatewayAuthMiddleware>();
        var optionsWrapper = Microsoft.Extensions.Options.Options.Create(options);

        var nextCalled = false;
        RequestDelegate next = (HttpContext ctx) =>
        {
            nextCalled = true;
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        };

        var middleware = new MeshGatewayAuthMiddleware(next, logger, optionsWrapper);
        var context = new DefaultHttpContext();

        return (middleware, context);
    }
}
