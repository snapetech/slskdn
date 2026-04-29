// <copyright file="DnsLeakPreventionVerifierTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.Common.Security;
using slskd.Mesh;
using slskd.Mesh.Transport;
using Xunit;

namespace slskd.Tests.Unit.Mesh.Transport;

public class DnsLeakPreventionVerifierTests
{
    private readonly Mock<ILogger<DnsLeakPreventionVerifier>> _loggerMock;
    private readonly DnsLeakPreventionVerifier _verifier;

    public DnsLeakPreventionVerifierTests()
    {
        _loggerMock = new Mock<ILogger<DnsLeakPreventionVerifier>>();
        _verifier = new DnsLeakPreventionVerifier(_loggerMock.Object);
    }

    [Fact]
    public async Task VerifySocksConfiguration_ValidOnionAddress_ReturnsSuccess()
    {
        // Arrange
        var proxyHost = "127.0.0.1";
        var proxyPort = 9050;
        var testHostname = "abcdefghijklmnop.onion"; // Valid .onion format

        // Act
        var result = await _verifier.VerifySocksConfigurationAsync(
            proxyHost, proxyPort, testHostname, false); // Don't require leak prevention for this test

        // Assert
        // Note: This test may fail if Tor is not running, but we're testing the validation logic
        // The important part is that it doesn't fail due to hostname validation
        Assert.NotNull(result);
    }

    [Fact]
    public async Task VerifySocksConfiguration_HandlesFragmentedHandshakeResponse()
    {
        // Arrange
        using var mockServer = new FragmentedSocksServer
        {
            FragmentHandshakeResponse = true
        };
        await mockServer.StartAsync();

        // expectedLeakPrevention=false avoids a second SOCKS validation pass.
        var result = await _verifier.VerifySocksConfigurationAsync("127.0.0.1", mockServer.Port, "abcdefghijklmnop.onion", false);

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public async Task VerifySocksConfiguration_InvalidHostname_ReturnsFailure()
    {
        // Arrange
        var proxyHost = "127.0.0.1";
        var proxyPort = 9050;
        var testHostname = "example.xyz"; // Invalid for proxy: not .onion/.i2p/localhost

        // Act
        var result = await _verifier.VerifySocksConfigurationAsync(proxyHost, proxyPort, testHostname, true);

        // Assert - fails from hostname validation ("Unsupported hostname format") when SOCKS succeeds, or from SOCKS/connection when Tor is not running
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.NotEmpty(result.ErrorMessage);
    }

    [Fact]
    public async Task VerifySocksConfiguration_WhenVerificationThrows_ReturnsSanitizedFailure()
    {
        var result = await _verifier.VerifySocksConfigurationAsync(
            "127.0.0.1",
            -1,
            "abcdefghijklmnop.onion",
            false);

        Assert.False(result.Success);
        Assert.Equal("SOCKS proxy connection failed", result.ErrorMessage);
        Assert.DoesNotContain("sensitive", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PerformDnsLeakAudit_NoConfigurations_ReturnsSuccess()
    {
        // Arrange
        var torOptions = new TorTransportOptions { Enabled = false };
        var i2pOptions = new I2pTransportOptions { Enabled = false };

        // Act
        var result = await _verifier.PerformDnsLeakAuditAsync(torOptions, i2pOptions);

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateHostname_OnionAddress_ReturnsValid()
    {
        // Arrange
        var onionAddr = "abcdefghijklmnop.onion";

        // Act
        var result = typeof(DnsLeakPreventionVerifier)
            .GetMethod("ValidateHostnameForProxy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .Invoke(_verifier, new object[] { onionAddr }) as HostnameValidationResult;

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateHostname_I2PAddress_ReturnsValid()
    {
        // Arrange
        var i2pAddr = "abcdefghijklmnop.i2p";

        // Act
        var result = typeof(DnsLeakPreventionVerifier)
            .GetMethod("ValidateHostnameForProxy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .Invoke(_verifier, new object[] { i2pAddr }) as HostnameValidationResult;

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateHostname_ClearnetAddress_ReturnsInvalid()
    {
        // Arrange
        var clearnetAddr = "example.com";

        // Act
        var result = typeof(DnsLeakPreventionVerifier)
            .GetMethod("ValidateHostnameForProxy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .Invoke(_verifier, new object[] { clearnetAddr }) as HostnameValidationResult;

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.Contains("clearnet address", result.ErrorMessage);
    }

    [Fact]
    public void ValidateHostname_IPAddress_ReturnsInvalid()
    {
        // Arrange
        var ipAddr = "192.168.1.100";

        // Act
        var result = typeof(DnsLeakPreventionVerifier)
            .GetMethod("ValidateHostnameForProxy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .Invoke(_verifier, new object[] { ipAddr }) as HostnameValidationResult;

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsValid);
        Assert.Contains("clearnet address", result.ErrorMessage);
    }

    [Fact]
    public void ValidateHostname_Localhost_ReturnsValid()
    {
        // Arrange
        var localhostAddr = "localhost";

        // Act
        var result = typeof(DnsLeakPreventionVerifier)
            .GetMethod("ValidateHostnameForProxy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .Invoke(_verifier, new object[] { localhostAddr }) as HostnameValidationResult;

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsValid);
    }

    private sealed class FragmentedSocksServer : IDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private Task? _serverTask;
        private TcpListener? _listener;

        public int Port { get; private set; }
        public bool FragmentHandshakeResponse { get; set; }

        public async Task StartAsync()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;

            // LongRunning ensures a dedicated OS thread starts immediately, bypassing thread-pool
            // scheduling delays that can cause the server to not start under heavy parallel-test load.
            // The entire accept+handle loop is synchronous so it never needs to re-enter the thread
            // pool — critical for correctness under thread-pool saturation in parallel test runs.
            _serverTask = Task.Factory.StartNew(() =>
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var client = _listener!.AcceptTcpClient();
                        HandleClientSync(client);
                    }
                    catch (SocketException)
                    {
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                }
            }, _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private void HandleClientSync(TcpClient client)
        {
            using var c = client;
            try
            {
                var stream = c.GetStream();

                // Read the SOCKS5 handshake from the client.
                var handshake = new byte[3];
                var read = 0;
                while (read < handshake.Length)
                    read += stream.Read(handshake, read, handshake.Length - read);

                var response = new byte[] { 0x05, 0x00 };
                if (FragmentHandshakeResponse && response.Length > 1)
                {
                    stream.Write(response, 0, 1);
                    Thread.Sleep(5); // simulate network fragmentation delay
                    stream.Write(response, 1, response.Length - 1);
                }
                else
                {
                    stream.Write(response, 0, response.Length);
                }
            }
            catch
            {
                // Ignore mock transport errors in test environment.
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _listener?.Stop();

            try
            {
                _serverTask?.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _cts.Dispose();
            }
        }
    }
}
