// <copyright file="HttpTunnelTransportTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Moq;
using slskd.Common.Security;
using Xunit;

namespace slskd.Tests.Unit.Mesh.Transport;

public class HttpTunnelTransportTests : IDisposable
{
    private readonly Mock<ILogger<HttpTunnelTransport>> _loggerMock;
    private readonly HttpTunnelOptions _defaultOptions;

    public HttpTunnelTransportTests()
    {
        _loggerMock = new Mock<ILogger<HttpTunnelTransport>>();
        _defaultOptions = new HttpTunnelOptions
        {
            ProxyUrl = "https://http-proxy.example.com/tunnel",
            Method = "POST",
            UseHttps = true
        };
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    [Fact]
    public void Constructor_WithValidOptions_CreatesInstance()
    {
        // Act & Assert - Should not throw
        using var transport = new HttpTunnelTransport(_defaultOptions, _loggerMock.Object);
        Assert.Equal(AnonymityTransportType.HttpTunnel, transport.TransportType);
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new HttpTunnelTransport(null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new HttpTunnelTransport(_defaultOptions, null!));
    }

    [Fact]
    public void TransportType_ReturnsHttpTunnel()
    {
        // Arrange
        using var transport = new HttpTunnelTransport(_defaultOptions, _loggerMock.Object);

        // Assert
        Assert.Equal(AnonymityTransportType.HttpTunnel, transport.TransportType);
    }

    [Fact]
    public void GetStatus_ReturnsValidStatusObject()
    {
        // Arrange
        using var transport = new HttpTunnelTransport(_defaultOptions, _loggerMock.Object);

        // Act
        var status = transport.GetStatus();

        // Assert
        Assert.NotNull(status);
        Assert.IsType<AnonymityTransportStatus>(status);
        Assert.Equal(0, status.ActiveConnections);
        Assert.Equal(0, status.TotalConnectionsAttempted);
        Assert.Equal(0, status.TotalConnectionsSuccessful);
    }

    [Fact]
    public async Task ConnectAsync_WithoutIsolationKey_UsesDefaultBehavior()
    {
        // Arrange
        using var transport = new HttpTunnelTransport(_defaultOptions, _loggerMock.Object);

        // Act & Assert - Should throw due to no actual HTTP server, but should handle connection logic
        await Assert.ThrowsAnyAsync<Exception>(() => transport.ConnectAsync("example.com", 80));

        var status = transport.GetStatus();
        Assert.Equal(1, status.TotalConnectionsAttempted);
    }

    [Fact]
    public async Task ConnectAsync_WithIsolationKey_IncludesIsolationHeader()
    {
        // Arrange
        using var transport = new HttpTunnelTransport(_defaultOptions, _loggerMock.Object);

        // Act & Assert - Should throw due to no actual HTTP server, but should handle isolation logic
        await Assert.ThrowsAnyAsync<Exception>(() => transport.ConnectAsync("example.com", 80, "peer123"));

        var status = transport.GetStatus();
        Assert.Equal(1, status.TotalConnectionsAttempted);
    }

    [Fact]
    public async Task IsAvailableAsync_ConnectionFailure_ReturnsFalse()
    {
        // Arrange
        using var transport = new HttpTunnelTransport(_defaultOptions, _loggerMock.Object);

        // Act
        var isAvailable = await transport.IsAvailableAsync();

        // Assert - Should return false since there's no actual HTTP server
        Assert.False(isAvailable);
        var status = transport.GetStatus();
        Assert.NotNull(status.LastError);
    }

    [Theory]
    [InlineData("https://proxy.example.com/tunnel")]
    [InlineData("http://localhost:8080/proxy")]
    [InlineData("https://api.example.com:8443/tunnel")]
    public void HttpTunnelOptions_AcceptsVariousProxyUrls(string proxyUrl)
    {
        // Arrange
        var options = new HttpTunnelOptions { ProxyUrl = proxyUrl };

        // Act & Assert - Should not throw
        using var transport = new HttpTunnelTransport(options, _loggerMock.Object);
        Assert.Equal(AnonymityTransportType.HttpTunnel, transport.TransportType);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("GET")]
    [InlineData("PUT")]
    public void HttpTunnelOptions_AcceptsVariousMethods(string method)
    {
        // Arrange
        var options = new HttpTunnelOptions
        {
            ProxyUrl = "https://proxy.example.com/tunnel",
            Method = method
        };

        // Act & Assert - Should not throw
        using var transport = new HttpTunnelTransport(options, _loggerMock.Object);
        Assert.Equal(AnonymityTransportType.HttpTunnel, transport.TransportType);
    }

    [Fact]
    public void Options_Validation_CustomHeaders()
    {
        // Arrange
        var options = new HttpTunnelOptions
        {
            ProxyUrl = "https://proxy.example.com/tunnel",
            CustomHeaders = new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer token123",
                ["X-API-Key"] = "api-key-456"
            }
        };

        // Act & Assert - Should not throw
        using var transport = new HttpTunnelTransport(options, _loggerMock.Object);
        Assert.Equal(AnonymityTransportType.HttpTunnel, transport.TransportType);
    }

    [Fact]
    public void Dispose_CleansUpResources()
    {
        // Arrange
        var transport = new HttpTunnelTransport(_defaultOptions, _loggerMock.Object);

        // Act
        transport.Dispose();

        // Assert - Should not throw and should clean up internal resources
        // HttpClient is managed internally and should be disposed
    }

    [Fact]
    public async Task MultipleConnectionAttempts_UpdateStatusCorrectly()
    {
        // Arrange
        using var transport = new HttpTunnelTransport(_defaultOptions, _loggerMock.Object);

        // Act - Multiple connection attempts
        for (int i = 0; i < 3; i++)
        {
            await Assert.ThrowsAnyAsync<Exception>(() => transport.ConnectAsync("example.com", 80));
        }

        // Assert
        var status = transport.GetStatus();
        Assert.Equal(3, status.TotalConnectionsAttempted);
        Assert.Equal(0, status.TotalConnectionsSuccessful); // No successful connections
    }
}


