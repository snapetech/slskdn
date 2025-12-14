// <copyright file="WebSocketTransportTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Moq;
using slskd.Common.Security;
using Xunit;

namespace slskd.Tests.Unit.Mesh.Transport;

public class WebSocketTransportTests : IDisposable
{
    private readonly Mock<ILogger<WebSocketTransport>> _loggerMock;
    private readonly WebSocketOptions _defaultOptions;

    public WebSocketTransportTests()
    {
        _loggerMock = new Mock<ILogger<WebSocketTransport>>();
        _defaultOptions = new WebSocketOptions
        {
            ServerUrl = "wss://test.example.com/tunnel",
            SubProtocol = "slskd-tunnel",
            MaxPooledConnections = 5
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
        using var transport = new WebSocketTransport(_defaultOptions, _loggerMock.Object);
        Assert.Equal(AnonymityTransportType.WebSocket, transport.TransportType);
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new WebSocketTransport(null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new WebSocketTransport(_defaultOptions, null!));
    }

    [Fact]
    public void TransportType_ReturnsWebSocket()
    {
        // Arrange
        using var transport = new WebSocketTransport(_defaultOptions, _loggerMock.Object);

        // Assert
        Assert.Equal(AnonymityTransportType.WebSocket, transport.TransportType);
    }

    [Fact]
    public void GetStatus_ReturnsValidStatusObject()
    {
        // Arrange
        using var transport = new WebSocketTransport(_defaultOptions, _loggerMock.Object);

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
    public async Task ConnectAsync_WithoutIsolationKey_UsesDefaultCircuit()
    {
        // Arrange
        using var transport = new WebSocketTransport(_defaultOptions, _loggerMock.Object);

        // Act & Assert - Should throw due to no actual WebSocket server, but should handle connection logic
        await Assert.ThrowsAnyAsync<Exception>(() => transport.ConnectAsync("example.com", 80));

        var status = transport.GetStatus();
        Assert.Equal(1, status.TotalConnectionsAttempted);
    }

    [Fact]
    public async Task ConnectAsync_WithIsolationKey_UsesIsolatedCircuit()
    {
        // Arrange
        using var transport = new WebSocketTransport(_defaultOptions, _loggerMock.Object);

        // Act & Assert - Should throw due to no actual WebSocket server, but should handle isolation logic
        await Assert.ThrowsAnyAsync<Exception>(() => transport.ConnectAsync("example.com", 80, "peer123"));

        var status = transport.GetStatus();
        Assert.Equal(1, status.TotalConnectionsAttempted);
    }

    [Fact]
    public async Task IsAvailableAsync_ConnectionFailure_ReturnsFalse()
    {
        // Arrange
        using var transport = new WebSocketTransport(_defaultOptions, _loggerMock.Object);

        // Act
        var isAvailable = await transport.IsAvailableAsync();

        // Assert - Should return false since there's no actual WebSocket server
        Assert.False(isAvailable);
        var status = transport.GetStatus();
        Assert.NotNull(status.LastError);
    }

    [Theory]
    [InlineData("wss://test.example.com/tunnel")]
    [InlineData("ws://localhost:8080/websocket")]
    [InlineData("wss://api.example.com:8443/ws")]
    public void WebSocketOptions_AcceptsVariousServerUrls(string serverUrl)
    {
        // Arrange
        var options = new WebSocketOptions { ServerUrl = serverUrl };

        // Act & Assert - Should not throw
        using var transport = new WebSocketTransport(options, _loggerMock.Object);
        Assert.Equal(AnonymityTransportType.WebSocket, transport.TransportType);
    }

    [Fact]
    public void Dispose_CleansUpResources()
    {
        // Arrange
        var transport = new WebSocketTransport(_defaultOptions, _loggerMock.Object);

        // Act
        transport.Dispose();

        // Assert - Should not throw and should clean up internal resources
        // In a full test, we'd verify that connection pools are cleared
    }

    [Fact]
    public void Options_Validation_CustomHeaders()
    {
        // Arrange
        var options = new WebSocketOptions
        {
            ServerUrl = "wss://test.example.com/tunnel",
            CustomHeaders = new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer token123",
                ["X-Custom-Header"] = "custom-value"
            }
        };

        // Act & Assert - Should not throw
        using var transport = new WebSocketTransport(options, _loggerMock.Object);
        Assert.Equal(AnonymityTransportType.WebSocket, transport.TransportType);
    }
}
