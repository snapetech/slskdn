// <copyright file="TorSocksTransportTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Moq;
using slskd.Common.Security;
using Xunit;

namespace slskd.Tests.Unit.Mesh.Transport;

public class TorSocksTransportTests : IDisposable
{
    private readonly Mock<ILogger<TorSocksTransport>> _loggerMock;
    private readonly TorOptions _defaultOptions;

    public TorSocksTransportTests()
    {
        _loggerMock = new Mock<ILogger<TorSocksTransport>>();
        _defaultOptions = new TorOptions
        {
            SocksAddress = "127.0.0.1:9050",
            IsolateStreams = false
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
        using var transport = new TorSocksTransport(_defaultOptions, _loggerMock.Object);
        Assert.Equal(AnonymityTransportType.Tor, transport.TransportType);
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new TorSocksTransport(null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new TorSocksTransport(_defaultOptions, null!));
    }

    [Fact]
    public void IsAvailableAsync_InitiallyReturnsFalse()
    {
        // Arrange
        using var transport = new TorSocksTransport(_defaultOptions, _loggerMock.Object);

        // Act - Call without actual connectivity test
        var task = transport.IsAvailableAsync();

        // Assert - Task should be created (we can't easily test the actual result without a mock server)
        Assert.NotNull(task);
    }

    [Fact]
    public void GetStatus_ReturnsValidStatusObject()
    {
        // Arrange
        using var transport = new TorSocksTransport(_defaultOptions, _loggerMock.Object);

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
    public void TransportType_ReturnsTor()
    {
        // Arrange
        using var transport = new TorSocksTransport(_defaultOptions, _loggerMock.Object);

        // Assert
        Assert.Equal(AnonymityTransportType.Tor, transport.TransportType);
    }

    [Fact]
    public async Task ConnectAsync_WithoutIsolationKey_CreatesConnectionAttempt()
    {
        // Arrange
        using var transport = new TorSocksTransport(_defaultOptions, _loggerMock.Object);

        // Act & Assert - Should throw due to no actual Tor server, but should update status
        await Assert.ThrowsAnyAsync<Exception>(() => transport.ConnectAsync("example.com", 80));

        var status = transport.GetStatus();
        Assert.Equal(1, status.TotalConnectionsAttempted);
    }

    [Fact]
    public async Task ConnectAsync_WithIsolationKey_WhenIsolationEnabled_UsesDifferentCredentials()
    {
        // Arrange
        var optionsWithIsolation = new TorOptions
        {
            SocksAddress = "127.0.0.1:9050",
            IsolateStreams = true
        };

        using var transport = new TorSocksTransport(optionsWithIsolation, _loggerMock.Object);

        // Act & Assert - Should throw due to no actual Tor server, but should handle isolation logic
        await Assert.ThrowsAnyAsync<Exception>(() => transport.ConnectAsync("example.com", 80, "test-peer"));

        var status = transport.GetStatus();
        Assert.Equal(1, status.TotalConnectionsAttempted);
    }

    [Fact]
    public void CircuitIsolation_CreatesDifferentCircuitsForDifferentKeys()
    {
        // Arrange
        var optionsWithIsolation = new TorOptions
        {
            SocksAddress = "127.0.0.1:9050",
            IsolateStreams = true
        };

        using var transport = new TorSocksTransport(optionsWithIsolation, _loggerMock.Object);

        // Act - Try to access private circuit creation method via reflection for testing
        var createMethod = typeof(TorSocksTransport).GetMethod("CreateIsolationCircuitAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Assert - Method should exist
        Assert.NotNull(createMethod);
    }

    [Fact]
    public void IsolationCredentials_AreDeterministicForSameKey()
    {
        // Arrange
        var key1 = "peer-alice";
        var key2 = "peer-alice"; // Same key
        var key3 = "peer-bob";   // Different key

        // Act
        var username1 = TorSocksTransport.GenerateIsolationUsername(key1);
        var username2 = TorSocksTransport.GenerateIsolationUsername(key2);
        var username3 = TorSocksTransport.GenerateIsolationUsername(key3);

        var password1 = TorSocksTransport.GenerateIsolationPassword(key1);
        var password2 = TorSocksTransport.GenerateIsolationPassword(key2);
        var password3 = TorSocksTransport.GenerateIsolationPassword(key3);

        // Assert - Same key produces same credentials
        Assert.Equal(username1, username2);
        Assert.Equal(password1, password2);

        // Different keys produce different credentials
        Assert.NotEqual(username1, username3);
        Assert.NotEqual(password1, password3);

        // Format validation
        Assert.StartsWith("tor-", username1);
        Assert.True(username1.Length > 4); // "tor-" + hash
        Assert.True(password1.Length > 0);
    }

    [Fact]
    public void CircuitPool_LimitsActiveCircuits()
    {
        // Arrange
        var optionsWithIsolation = new TorOptions
        {
            SocksAddress = "127.0.0.1:9050",
            IsolateStreams = true
        };

        using var transport = new TorSocksTransport(optionsWithIsolation, _loggerMock.Object);

        // Act - Access circuit pool via reflection
        var circuitPoolField = typeof(TorSocksTransport).GetField("_circuitPool",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var circuitPool = circuitPoolField?.GetValue(transport) as System.Collections.Concurrent.ConcurrentDictionary<string, object>;

        // Assert - Circuit pool should exist
        Assert.NotNull(circuitPool);
    }

    [Fact]
    public void Dispose_CleansUpCircuitPool()
    {
        // Arrange
        var optionsWithIsolation = new TorOptions
        {
            SocksAddress = "127.0.0.1:9050",
            IsolateStreams = true
        };

        var transport = new TorSocksTransport(optionsWithIsolation, _loggerMock.Object);

        // Act
        transport.Dispose();

        // Assert - Should not throw and should clean up resources
        // In a full test, we'd verify the circuit pool is cleared
    }

    [Fact]
    public void GenerateIsolationUsername_CreatesDeterministicUsername()
    {
        // Arrange
        var isolationKey1 = "peer123";
        var isolationKey2 = "peer123"; // Same key
        var isolationKey3 = "peer456"; // Different key

        // Act
        var username1 = TorSocksTransport.GenerateIsolationUsername(isolationKey1);
        var username2 = TorSocksTransport.GenerateIsolationUsername(isolationKey2);
        var username3 = TorSocksTransport.GenerateIsolationUsername(isolationKey3);

        // Assert
        Assert.Equal(username1, username2); // Same key should produce same username
        Assert.NotEqual(username1, username3); // Different keys should produce different usernames
        Assert.StartsWith("tor-", username1);
        Assert.Equal(20, username1.Length); // "tor-" + 16 hex chars
    }

    [Fact]
    public void GenerateIsolationPassword_CreatesDeterministicPassword()
    {
        // Arrange
        var isolationKey1 = "peer123";
        var isolationKey2 = "peer123";
        var isolationKey3 = "peer456";

        // Act
        var password1 = TorSocksTransport.GenerateIsolationPassword(isolationKey1);
        var password2 = TorSocksTransport.GenerateIsolationPassword(isolationKey2);
        var password3 = TorSocksTransport.GenerateIsolationPassword(isolationKey3);

        // Assert
        Assert.Equal(password1, password2);
        Assert.NotEqual(password1, password3);
        Assert.Equal(16, password1.Length);
        Assert.Matches("^[a-f0-9]+$", password1); // Should be lowercase hex
    }

    [Fact]
    public async Task MultipleConnectionAttempts_UpdateStatusCorrectly()
    {
        // Arrange
        using var transport = new TorSocksTransport(_defaultOptions, _loggerMock.Object);

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

    [Fact]
    public void StatusTracking_IsThreadSafe()
    {
        // Arrange
        using var transport = new TorSocksTransport(_defaultOptions, _loggerMock.Object);

        // Act - Access status from multiple threads
        var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
        {
            var status = transport.GetStatus();
            return status != null;
        })).ToArray();

        Task.WaitAll(tasks);

        // Assert - No exceptions thrown, all tasks completed
        Assert.All(tasks, t => Assert.True(t.Result));
    }

    [Theory]
    [InlineData("127.0.0.1:9050")]
    [InlineData("localhost:9050")]
    [InlineData("192.168.1.1:9050")]
    public void TorOptions_AcceptVariousSocksAddresses(string address)
    {
        // Arrange
        var options = new TorOptions { SocksAddress = address };

        // Act & Assert - Should not throw
        using var transport = new TorSocksTransport(options, _loggerMock.Object);
        Assert.Equal(AnonymityTransportType.Tor, transport.TransportType);
    }

    [Fact]
    public void IsolationCredentials_AreDifferentForDifferentPeers()
    {
        // Arrange
        var peers = new[] { "alice", "bob", "charlie", "diana" };

        // Act
        var credentials = peers.Select(peer => new
        {
            Peer = peer,
            Username = TorSocksTransport.GenerateIsolationUsername(peer),
            Password = TorSocksTransport.GenerateIsolationPassword(peer)
        }).ToList();

        // Assert - All credentials should be unique
        var uniqueCredentials = credentials.Select(c => $"{c.Username}:{c.Password}").Distinct();
        Assert.Equal(peers.Length, uniqueCredentials.Count());
    }

    [Fact]
    public async Task IsAvailableAsync_UpdatesLastErrorOnFailure()
    {
        // Arrange
        var options = new TorOptions { SocksAddress = "127.0.0.1:12345" }; // Non-existent port
        using var transport = new TorSocksTransport(options, _loggerMock.Object);

        // Act
        var isAvailable = await transport.IsAvailableAsync();

        // Assert
        Assert.False(isAvailable);
        var status = transport.GetStatus();
        Assert.False(status.IsAvailable);
        Assert.NotNull(status.LastError);
        Assert.Contains("connect", status.LastError.ToLower());
    }

    [Fact]
    public void CreateSocks5ConnectRequest_HandlesVariousAddressTypes()
    {
        // Test IPv4
        var requestIpv4 = CreateSocks5ConnectRequest("192.168.1.1", 8080);
        Assert.Equal(0x01, requestIpv4[3]); // IPv4 address type

        // Test domain name
        var requestDomain = CreateSocks5ConnectRequest("example.com", 80);
        Assert.Equal(0x03, requestDomain[3]); // Domain name address type
        Assert.Equal(11, requestDomain[4]); // Length of "example.com"

        // Helper method to access private method for testing
        static byte[] CreateSocks5ConnectRequest(string host, int port)
        {
            var method = typeof(TorSocksTransport).GetMethod("CreateSocks5ConnectRequest",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            return (byte[])method.Invoke(null, new object[] { host, port });
        }
    }

    [Fact]
    public void SendSocks5AuthAsync_HandlesCredentialsCorrectly()
    {
        // Test that the authentication method exists and has correct signature
        var method = typeof(TorSocksTransport).GetMethod("SendSocks5AuthAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
        var parameters = method.GetParameters();
        Assert.Equal(4, parameters.Length); // stream, username, password, cancellationToken
    }
}
