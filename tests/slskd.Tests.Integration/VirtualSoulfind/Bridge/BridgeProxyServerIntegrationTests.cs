// <copyright file="BridgeProxyServerIntegrationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Integration.VirtualSoulfind.Bridge;

using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using slskd.Tests.Integration.Harness;
using slskd.VirtualSoulfind.Bridge;
using slskd.VirtualSoulfind.Bridge.Protocol;
using slskd.VirtualSoulfind.Bridge.Proxy;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// Integration tests for BridgeProxyServer.
/// Tests TCP server functionality, protocol handling, and client connections.
/// </summary>
[Trait("Category", "L2-Bridge")]
public class BridgeProxyServerIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper output;
    private SlskdnTestClient? slskdn;
    private int bridgePort;

    public BridgeProxyServerIntegrationTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    public async Task InitializeAsync()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        
        // Allocate ephemeral port for bridge
        bridgePort = AllocateEphemeralPort();
        
        // Start slskdn with bridge enabled
        // Note: SlskdnTestClient uses TestServer which doesn't support TCP listeners
        // For now, we'll skip these tests and document that they require a full instance
        // In a real scenario, we'd need to start a full slskdn instance with bridge enabled
        output.WriteLine("Note: BridgeProxyServer integration tests require a full slskdn instance.");
        output.WriteLine("These tests are skipped when using TestServer (no TCP listener support).");
    }

    public async Task DisposeAsync()
    {
        if (slskdn != null)
        {
            await slskdn.DisposeAsync();
        }
    }

    [Fact(Skip = "Requires full slskdn instance - TestServer doesn't support TCP listeners")]
    public async Task BridgeProxyServer_Should_Accept_Client_Connection()
    {
        // This test requires a full slskdn instance running with bridge enabled
        // TestServer (used by SlskdnTestClient) doesn't support TCP listeners
        // To test this, start slskdn manually with bridge enabled and connect to it
        
        // Arrange
        using var client = new TcpClient();

        // Act
        await client.ConnectAsync(IPAddress.Loopback, bridgePort);
        var connected = client.Connected;

        // Assert
        Assert.True(connected, "Should be able to connect to bridge proxy server");
        client.Close();
    }

    [Fact(Skip = "Requires full slskdn instance - TestServer doesn't support TCP listeners")]
    public async Task BridgeProxyServer_Should_Handle_Login_Request()
    {
        // Arrange
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, bridgePort);
        var stream = client.GetStream();
        var parser = new SoulseekProtocolParser(
            new XunitLogger<SoulseekProtocolParser>(output));

        // Act - Send login request
        var loginPayload = BuildLoginRequest("testuser", "testpass");
        await parser.WriteMessageAsync(
            stream,
            SoulseekProtocolParser.MessageType.Login,
            loginPayload);

        // Read response
        var response = await parser.ReadMessageAsync(stream);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(SoulseekProtocolParser.MessageType.LoginResponse, response.Type);
        
        // Parse response
        var loginResponse = parser.ParseLoginRequest(response.Payload);
        // Response format: [success: bool] [message: string]
        // For now, just verify we got a response
        Assert.True(response.Payload.Length > 0);
        
        client.Close();
    }

    [Fact(Skip = "Requires full slskdn instance - TestServer doesn't support TCP listeners")]
    public async Task BridgeProxyServer_Should_Handle_Search_Request()
    {
        // Arrange
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, bridgePort);
        var stream = client.GetStream();
        var parser = new SoulseekProtocolParser(
            new XunitLogger<SoulseekProtocolParser>(output));

        // Login first
        var loginPayload = BuildLoginRequest("testuser", "testpass");
        await parser.WriteMessageAsync(
            stream,
            SoulseekProtocolParser.MessageType.Login,
            loginPayload);
        var loginResponse = await parser.ReadMessageAsync(stream);
        Assert.NotNull(loginResponse);

        // Act - Send search request
        var searchPayload = BuildSearchRequest("test query", 12345);
        await parser.WriteMessageAsync(
            stream,
            SoulseekProtocolParser.MessageType.SearchRequest,
            searchPayload);

        // Read response
        var response = await parser.ReadMessageAsync(stream);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(SoulseekProtocolParser.MessageType.SearchResponse, response.Type);
        Assert.True(response.Payload.Length > 0);
        
        client.Close();
    }

    [Fact(Skip = "Requires full slskdn instance - TestServer doesn't support TCP listeners")]
    public async Task BridgeProxyServer_Should_Handle_RoomList_Request()
    {
        // Arrange
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, bridgePort);
        var stream = client.GetStream();
        var parser = new SoulseekProtocolParser(
            new XunitLogger<SoulseekProtocolParser>(output));

        // Login first
        var loginPayload = BuildLoginRequest("testuser", "testpass");
        await parser.WriteMessageAsync(
            stream,
            SoulseekProtocolParser.MessageType.Login,
            loginPayload);
        var loginResponse = await parser.ReadMessageAsync(stream);
        Assert.NotNull(loginResponse);

        // Act - Send room list request
        var roomListPayload = new byte[0]; // Empty payload for room list request
        await parser.WriteMessageAsync(
            stream,
            SoulseekProtocolParser.MessageType.RoomListRequest,
            roomListPayload);

        // Read response
        var response = await parser.ReadMessageAsync(stream);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(SoulseekProtocolParser.MessageType.RoomListResponse, response.Type);
        Assert.True(response.Payload.Length > 0);
        
        client.Close();
    }

    [Fact(Skip = "Requires full slskdn instance - TestServer doesn't support TCP listeners")]
    public async Task BridgeProxyServer_Should_Reject_Invalid_Authentication()
    {
        // This test requires a full slskdn instance with RequireAuth=true and Password configured
        // TestServer (used by SlskdnTestClient) doesn't support TCP listeners
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

    private byte[] BuildLoginRequest(string username, string password)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        var usernameBytes = Encoding.UTF8.GetBytes(username);
        writer.Write(usernameBytes.Length);
        writer.Write(usernameBytes);
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        writer.Write(passwordBytes.Length);
        writer.Write(passwordBytes);
        return stream.ToArray();
    }

    private byte[] BuildSearchRequest(string query, int token)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        var queryBytes = Encoding.UTF8.GetBytes(query);
        writer.Write(queryBytes.Length);
        writer.Write(queryBytes);
        writer.Write(token);
        return stream.ToArray();
    }

    private int AllocateEphemeralPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
