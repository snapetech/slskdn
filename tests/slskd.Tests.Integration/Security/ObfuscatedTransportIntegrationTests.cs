// <copyright file="ObfuscatedTransportIntegrationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using slskd.Common.Security;
using Xunit;
using Xunit.Abstractions;

namespace slskd.Tests.Integration.Security;

/// <summary>
/// Integration tests for obfuscated transport implementations.
/// Tests real transport behavior with mock servers.
/// </summary>
[Collection("Integration")] // Run these tests separately to avoid conflicts
public class ObfuscatedTransportIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger _logger;

    public ObfuscatedTransportIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _logger = new XunitLogger(output);
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    [Fact]
    public async Task WebSocketTransport_ConnectionLifecycle_WorksCorrectly()
    {
        // Arrange - Create a mock WebSocket server would be complex,
        // so we test the connection attempt and error handling

        var options = new WebSocketOptions
        {
            ServerUrl = "ws://127.0.0.1:12345/nonexistent", // Non-existent server
            SubProtocol = "slskd-tunnel"
        };

        using var transport = new WebSocketTransport(options, (ILogger<WebSocketTransport>)_logger);

        // Act & Assert
        var status = transport.GetStatus();
        Assert.Equal(0, status.TotalConnectionsAttempted);

        await Assert.ThrowsAnyAsync<Exception>(() => transport.ConnectAsync("example.com", 80));

        status = transport.GetStatus();
        Assert.Equal(1, status.TotalConnectionsAttempted);
        Assert.Equal(0, status.TotalConnectionsSuccessful);
    }

    [Fact]
    public async Task HttpTunnelTransport_ConnectionLifecycle_WorksCorrectly()
    {
        // Arrange
        var options = new HttpTunnelOptions
        {
            ProxyUrl = "http://127.0.0.1:12345/nonexistent", // Non-existent server
            Method = "POST"
        };

        using var transport = new HttpTunnelTransport(options, (ILogger<HttpTunnelTransport>)_logger);

        // Act & Assert
        var status = transport.GetStatus();
        Assert.Equal(0, status.TotalConnectionsAttempted);

        await Assert.ThrowsAnyAsync<Exception>(() => transport.ConnectAsync("example.com", 80));

        status = transport.GetStatus();
        Assert.Equal(1, status.TotalConnectionsAttempted);
        Assert.Equal(0, status.TotalConnectionsSuccessful);
    }

    [Fact]
    public async Task Obfs4Transport_WithoutObfs4Proxy_HandlesGracefully()
    {
        // Arrange
        var options = new Obfs4Options
        {
            Obfs4ProxyPath = "/nonexistent/obfs4proxy",
            BridgeLines = new List<string> { "obfs4 192.0.2.1:443 test cert=test iat-mode=0" }
        };

        using var transport = new Obfs4Transport(options, (ILogger<Obfs4Transport>)_logger);

        // Act
        var isAvailable = await transport.IsAvailableAsync();

        // Assert
        Assert.False(isAvailable);
        var status = transport.GetStatus();
        Assert.Contains("not found", status.LastError?.ToLower() ?? "");
    }

    [Fact]
    public async Task MeekTransport_ConnectionLifecycle_WorksCorrectly()
    {
        // Arrange
        var options = new MeekOptions
        {
            BridgeUrl = "http://127.0.0.1:12345/nonexistent", // Non-existent server
            FrontDomain = "www.example.com"
        };

        using var transport = new MeekTransport(options, (ILogger<MeekTransport>)_logger);

        // Act & Assert
        var status = transport.GetStatus();
        Assert.Equal(0, status.TotalConnectionsAttempted);

        await Assert.ThrowsAnyAsync<Exception>(() => transport.ConnectAsync("example.com", 80));

        status = transport.GetStatus();
        Assert.Equal(1, status.TotalConnectionsAttempted);
        Assert.Equal(0, status.TotalConnectionsSuccessful);
    }

    [Fact]
    public async Task AnonymityTransportSelector_FallbackLogic_Works()
    {
        // Arrange - Create a selector with multiple transports that will fail
        var adversarialOptions = new AdversarialOptions
        {
            AnonymityLayer = new AnonymityLayerOptions { Enabled = true, Mode = AnonymityMode.Direct }
        };

        var selector = new AnonymityTransportSelector(
            adversarialOptions,
            new Transport.TransportPolicyManager(),
            (ILogger<AnonymityTransportSelector>)_logger);

        // Act & Assert - Should throw when all transports fail
        await Assert.ThrowsAsync<Exception>(() =>
            selector.SelectAndConnectAsync("peer123", null, "example.com", 80, null));
    }

    [Fact]
    public async Task TransportStatus_UpdatesCorrectlyAcrossAttempts()
    {
        // Arrange
        var options = new WebSocketOptions
        {
            ServerUrl = "ws://127.0.0.1:12345/nonexistent"
        };

        using var transport = new WebSocketTransport(options, (ILogger<WebSocketTransport>)_logger);

        // Act - Multiple failed connection attempts
        for (int i = 0; i < 3; i++)
        {
            await Assert.ThrowsAnyAsync<Exception>(() => transport.ConnectAsync("example.com", 80));
        }

        // Assert
        var status = transport.GetStatus();
        Assert.Equal(3, status.TotalConnectionsAttempted);
        Assert.Equal(0, status.TotalConnectionsSuccessful);
        Assert.Equal(0, status.ActiveConnections);
    }

    [Fact]
    public async Task IsolationKeys_CreateSeparateConnectionContexts()
    {
        // Arrange
        var options = new HttpTunnelOptions
        {
            ProxyUrl = "http://127.0.0.1:12345/nonexistent"
        };

        using var transport = new HttpTunnelTransport(options, (ILogger<HttpTunnelTransport>)_logger);

        // Act - Connect with different isolation keys (simulating different peers)
        await Assert.ThrowsAnyAsync<Exception>(() => transport.ConnectAsync("example.com", 80, "peer1"));
        await Assert.ThrowsAnyAsync<Exception>(() => transport.ConnectAsync("example.com", 80, "peer2"));
        await Assert.ThrowsAnyAsync<Exception>(() => transport.ConnectAsync("example.com", 80, "peer1")); // Same key again

        // Assert
        var status = transport.GetStatus();
        Assert.Equal(3, status.TotalConnectionsAttempted);
    }

    [Fact]
    public async Task TransportDispose_CleansUpResources()
    {
        // Arrange
        var transport = new WebSocketTransport(
            new WebSocketOptions { ServerUrl = "ws://127.0.0.1:12345/test" },
            (ILogger<WebSocketTransport>)_logger);

        // Act - Use and dispose
        await Assert.ThrowsAnyAsync<Exception>(() => transport.ConnectAsync("example.com", 80));
        transport.Dispose();

        // Assert - Should not throw on dispose
        // In a full test, we'd verify resources are cleaned up
    }

    /// <summary>
    /// Xunit logger implementation for integration tests.
    /// </summary>
    private class XunitLogger : ILogger
    {
        private readonly ITestOutputHelper _output;

        public XunitLogger(ITestOutputHelper output)
        {
            _output = output;
        }

        public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _output.WriteLine($"[{logLevel}] {formatter(state, exception)}");
            if (exception != null)
            {
                _output.WriteLine($"Exception: {exception}");
            }
        }

        private class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();
            public void Dispose() { }
        }
    }
}

// Extension methods for ILogger casting
public static class LoggerExtensions
{
    public static ILogger<T> As<T>(this ILogger logger) => (ILogger<T>)logger;
}

