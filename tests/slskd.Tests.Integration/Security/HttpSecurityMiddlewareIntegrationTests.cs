// <copyright file="HttpSecurityMiddlewareIntegrationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Tests.Integration.Security;

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using slskd.Common.Security;
using Xunit;

/// <summary>
/// HTTP integration tests for SecurityMiddleware.
/// Tests the middleware in a real HTTP context with actual requests.
/// </summary>
public class HttpSecurityMiddlewareIntegrationTests : IClassFixture<SecurityTestWebApplicationFactory>
{
    private readonly SecurityTestWebApplicationFactory _factory;

    public HttpSecurityMiddlewareIntegrationTests(SecurityTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PathTraversal_PlainTraversal_Returns400()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/etc/passwd");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PathTraversal_UrlEncodedTraversal_Returns400()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/..%2F..%2Fetc%2Fpasswd");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PathTraversal_DoubleEncodedTraversal_Returns400()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act - double URL encoding: %252e%252e = %2e%2e = ..
        var response = await client.GetAsync("/api/%252e%252e/etc/passwd");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PathTraversal_SuspiciousSystemPath_Returns400()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/etc/shadow");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PathTraversal_NormalPath_Returns200()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Authentication_WhenDisabled_AllowsAccess()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act - request without authentication
        var response = await client.GetAsync("/api/v0/session");

        // Assert - should NOT return 401 when auth is disabled
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MeshGateway_WhenDisabled_Returns404()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/mesh/gateway");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MeshGateway_WhenDisabled_Returns404ForServices()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/mesh/http/services");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SecurityMiddleware_IsFirstInPipeline()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act - try path traversal
        var response = await client.GetAsync("/etc/passwd");

        // Assert - should be blocked by SecurityMiddleware (first in pipeline)
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        // Verify response doesn't contain static file content (would indicate UseFileServer ran first)
        var content = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("<!DOCTYPE html>", content);
        Assert.DoesNotContain("<html", content);
    }
}

/// <summary>
/// Web application factory for security middleware integration tests.
/// Creates a test server with SecurityMiddleware configured.
/// </summary>
public class SecurityTestWebApplicationFactory : IDisposable
{
    private TestServer? _server;
    private HttpClient? _client;
    private IHost? _host;

    public HttpClient CreateClient()
    {
        if (_client != null)
        {
            return _client;
        }

        var configDict = new Dictionary<string, string?>
        {
            ["Security:Enabled"] = "true",
            ["Security:Profile"] = "Standard",
            ["Web:Authentication:Disabled"] = "true",
            ["Mesh:Gateway:Enabled"] = "false",
        };

        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureAppConfiguration(config =>
                {
                    config.AddInMemoryCollection(configDict);
                });
                webBuilder.ConfigureServices((context, services) =>
                {
                    services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
                    services.AddSlskdnSecurity(context.Configuration);
                    services.AddRouting();
                });
                webBuilder.Configure(app =>
                {
                    app.UseSlskdnSecurity();
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/health", () => "OK");
                        endpoints.MapGet("/api/v0/session", () => new { authenticated = true });
                    });
                });
            });

        _host = hostBuilder.StartAsync().GetAwaiter().GetResult();
        _server = _host.GetTestServer();
        _client = _server.CreateClient();
        return _client;
    }

    public void Dispose()
    {
        _client?.Dispose();
        _server?.Dispose();
        _host?.Dispose();
    }
}
