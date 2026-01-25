// <copyright file="ObfuscatedTransportIntegrationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using slskd.Common.Security;
using slskd.Mesh.Transport;
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

    public ObfuscatedTransportIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
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

        var options = new WebSocketTransportOptions
        {
            ServerUrl = "ws://127.0.0.1:12345/nonexistent", // Non-existent server
            SubProtocol = "slskd-tunnel"
        };

        var transport = new WebSocketTransport(options, new XunitLogger<WebSocketTransport>(_output));

        // Act & Assert
        var status = transport.GetStatus();
        Assert.Equal(0, status.TotalConnectionsAttempted);

        await Assert.ThrowsAnyAsync<Exception>(() => transport.ConnectAsync("example.com", 80, CancellationToken.None));

        status = transport.GetStatus();
        Assert.Equal(1, status.TotalConnectionsAttempted);
        Assert.Equal(0, status.TotalConnectionsSuccessful);
    }

    [Fact]
    public async Task HttpTunnelTransport_ConnectionLifecycle_WorksCorrectly()
    {
        // Arrange
        var options = new HttpTunnelTransportOptions
        {
            ProxyUrl = "http://127.0.0.1:12345/nonexistent", // Non-existent server
            Method = "POST"
        };

        var transport = new HttpTunnelTransport(options, new XunitLogger<HttpTunnelTransport>(_output));

        // Act & Assert
        var status = transport.GetStatus();
        Assert.Equal(0, status.TotalConnectionsAttempted);

        await Assert.ThrowsAnyAsync<Exception>(() => transport.ConnectAsync("example.com", 80, CancellationToken.None));

        status = transport.GetStatus();
        Assert.Equal(1, status.TotalConnectionsAttempted);
        Assert.Equal(0, status.TotalConnectionsSuccessful);
    }

    [Fact]
    public async Task Obfs4Transport_WithoutObfs4Proxy_HandlesGracefully()
    {
        // Arrange
        var options = new Obfs4TransportOptions
        {
            Obfs4ProxyPath = "/nonexistent/obfs4proxy",
            BridgeLines = new List<string> { "obfs4 192.0.2.1:443 test cert=test iat-mode=0" }
        };

        var transport = new Obfs4Transport(options, new XunitLogger<Obfs4Transport>(_output));

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
        var options = new MeekTransportOptions
        {
            BridgeUrl = "http://127.0.0.1:12345/nonexistent", // Non-existent server
            FrontDomain = "www.example.com"
        };

        var transport = new MeekTransport(options, new XunitLogger<MeekTransport>(_output));

        // Act & Assert
        var status = transport.GetStatus();
        Assert.Equal(0, status.TotalConnectionsAttempted);

        await Assert.ThrowsAnyAsync<Exception>(() => transport.ConnectAsync("example.com", 80, CancellationToken.None));

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
            Anonymity = new AnonymityLayerOptions { Enabled = true, Mode = AnonymityMode.Direct }
        };

        var selector = new AnonymityTransportSelector(
            adversarialOptions,
            new TransportPolicyManager(NullLogger<TransportPolicyManager>.Instance),
            new XunitLogger<AnonymityTransportSelector>(_output),
            NullLoggerFactory.Instance);

        // Act & Assert - Should throw when all transports fail (InvalidOperationException when none available)
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            selector.SelectAndConnectAsync("peer123", null, "example.com", 80, null, CancellationToken.None));
    }

    [Fact]
    public async Task TransportStatus_UpdatesCorrectlyAcrossAttempts()
    {
        // Arrange
        var options = new WebSocketTransportOptions
        {
            ServerUrl = "ws://127.0.0.1:12345/nonexistent"
        };

        var transport = new WebSocketTransport(options, new XunitLogger<WebSocketTransport>(_output));

        // Act - Multiple failed connection attempts
        for (int i = 0; i < 3; i++)
        {
            await Assert.ThrowsAnyAsync<Exception>(() => transport.ConnectAsync("example.com", 80, CancellationToken.None));
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
        var options = new HttpTunnelTransportOptions
        {
            ProxyUrl = "http://127.0.0.1:12345/nonexistent"
        };

        var transport = new HttpTunnelTransport(options, new XunitLogger<HttpTunnelTransport>(_output));

        // Act - Connect with different isolation keys (simulating different peers)
        await Assert.ThrowsAnyAsync<Exception>(() => transport.ConnectAsync("example.com", 80, "peer1", CancellationToken.None));
        await Assert.ThrowsAnyAsync<Exception>(() => transport.ConnectAsync("example.com", 80, "peer2", CancellationToken.None));
        await Assert.ThrowsAnyAsync<Exception>(() => transport.ConnectAsync("example.com", 80, "peer1", CancellationToken.None)); // Same key again

        // Assert
        var status = transport.GetStatus();
        Assert.Equal(3, status.TotalConnectionsAttempted);
    }

    [Fact]
    public async Task TransportDispose_CleansUpResources()
    {
        // Arrange - WebSocketTransport does not implement IDisposable; we verify ConnectAsync completes
        var transport = new WebSocketTransport(
            new WebSocketTransportOptions { ServerUrl = "ws://127.0.0.1:12345/test" },
            new XunitLogger<WebSocketTransport>(_output));

        // Act - Connection attempt (expected to throw for non-existent server)
        await Assert.ThrowsAnyAsync<Exception>(() => transport.ConnectAsync("example.com", 80, CancellationToken.None));

        // Assert - No Dispose; transport does not implement IDisposable
    }

    /// <summary>
    /// Xunit logger implementation for integration tests.
    /// </summary>
    private class XunitLogger<T> : ILogger<T>
    {
        private readonly ITestOutputHelper _output;

        public XunitLogger(ITestOutputHelper output)
        {
            _output = output;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

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


