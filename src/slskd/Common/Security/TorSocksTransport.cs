// <copyright file="TorSocksTransport.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Net.Sockets;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace slskd.Common.Security;

/// <summary>
/// Tor SOCKS5 proxy transport for anonymity.
/// </summary>
public sealed class TorSocksTransport : IAnonymityTransport, IDisposable
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);

    private readonly TorOptions _options;
    private readonly ILogger<TorSocksTransport> _logger;

    private readonly AnonymityTransportStatus _status = new();
    private readonly object _statusLock = new();

    // Circuit isolation: different isolation keys get different Tor circuits
    private readonly ConcurrentDictionary<string, IsolationCircuit> _circuitPool = new();
    private readonly object _circuitLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="TorSocksTransport"/> class.
    /// </summary>
    /// <param name="options">The Tor options.</param>
    /// <param name="logger">The logger.</param>
    public TorSocksTransport(TorOptions options, ILogger<TorSocksTransport> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the transport type.
    /// </summary>
    public AnonymityTransportType TransportType => AnonymityTransportType.Tor;

    /// <summary>
    /// Checks if the Tor SOCKS proxy is available and functional.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if Tor is available, false otherwise.</returns>
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var (socksHost, socksPort) = ParseSocksAddress(_options.SocksAddress);

            // Try to connect to the SOCKS proxy and perform a basic handshake
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

            var connectTask = client.ConnectAsync(socksHost, socksPort);
            await connectTask.WaitAsync(linkedCts.Token);

            if (client.Connected)
            {
                // Perform basic SOCKS5 handshake
                var stream = client.GetStream();
                var handshake = new byte[] { 0x05, 0x01, 0x00 }; // SOCKS5, 1 method, no auth
                await stream.WriteAsync(handshake, 0, handshake.Length, linkedCts.Token);

                var response = new byte[2];
                await ReadExactlyAsync(stream, response, 0, 2, linkedCts.Token);

                if (response[0] == 0x05 && response[1] == 0x00)
                {
                    // SOCKS5 handshake successful
                    lock (_statusLock)
                    {
                        _status.IsAvailable = true;
                        _status.LastError = null;
                        _status.LastSuccessfulConnection = DateTimeOffset.UtcNow;
                    }

                    _logger.LogDebug("Tor SOCKS proxy is available at {Address}", _options.SocksAddress);
                    return true;
                }
            }

            lock (_statusLock)
            {
                _status.IsAvailable = false;
                _status.LastError = "SOCKS5 handshake failed";
            }

            _logger.LogWarning("Tor SOCKS proxy handshake failed at {Address}", _options.SocksAddress);
            return false;
        }
        catch (Exception ex)
        {
            lock (_statusLock)
            {
                _status.IsAvailable = false;
                _status.LastError = "Tor SOCKS proxy unavailable";
            }

            _logger.LogWarning(ex, "Tor SOCKS proxy not available at {Address}", _options.SocksAddress);
            return false;
        }
    }

    /// <summary>
    /// Establishes a connection through the Tor SOCKS proxy.
    /// </summary>
    /// <param name="host">The target host.</param>
    /// <param name="port">The target port.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A stream for the anonymous connection.</returns>
    public async Task<Stream> ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        return await ConnectAsync(host, port, null, cancellationToken);
    }

    /// <summary>
    /// Establishes a connection through the Tor SOCKS proxy with optional stream isolation.
    /// </summary>
    /// <param name="host">The target host.</param>
    /// <param name="port">The target port.</param>
    /// <param name="isolationKey">Optional key for stream isolation (different keys use different Tor circuits).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A stream for the anonymous connection.</returns>
    public async Task<Stream> ConnectAsync(string host, int port, string? isolationKey, CancellationToken cancellationToken = default)
    {
        var circuitKey = isolationKey ?? "default";
        return await ConnectWithCircuitIsolationAsync(host, port, circuitKey, cancellationToken);
    }

    /// <summary>
    /// Establishes a connection with proper circuit isolation.
    /// </summary>
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Circuit connections are transferred to the returned stream on success and disposed on failure.")]
    private async Task<Stream> ConnectWithCircuitIsolationAsync(string host, int port, string circuitKey, CancellationToken cancellationToken = default)
    {
        TcpClient? client = null;
        IsolationCircuit? leasedCircuit = null;
        using var timeoutCts = new CancellationTokenSource(ConnectTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var effectiveToken = linkedCts.Token;

        lock (_statusLock)
        {
            _status.TotalConnectionsAttempted++;
        }

        try
        {
            // Get or create an isolated circuit for this key
            leasedCircuit = await GetOrCreateCircuitAsync(circuitKey, effectiveToken);

            // Get a connection from the circuit's pool
            client = leasedCircuit.GetConnection();
            await client.ConnectAsync(leasedCircuit.SocksHost, leasedCircuit.SocksPort, effectiveToken);

            var stream = client.GetStream();

            // SOCKS5 handshake with authentication if circuit has credentials
            byte[] handshake;
            if (leasedCircuit.Username != null && leasedCircuit.Password != null)
            {
                // Use username/password authentication for stream isolation
                handshake = new byte[] { 0x05, 0x01, 0x02 }; // SOCKS5, 1 method, username/password auth
                await stream.WriteAsync(handshake, 0, handshake.Length, effectiveToken);

                var handshakeResponse = new byte[2];
                await ReadExactlyAsync(stream, handshakeResponse, 0, 2, effectiveToken);

                if (handshakeResponse[0] != 0x05 || handshakeResponse[1] != 0x02)
                {
                    throw new Exception("SOCKS5 authentication not supported by proxy");
                }

                // Send username/password authentication
                await SendSocks5AuthAsync(stream, leasedCircuit.Username, leasedCircuit.Password, effectiveToken);

                var authResponse = new byte[2];
                await ReadExactlyAsync(stream, authResponse, 0, 2, effectiveToken);

                if (authResponse[0] != 0x01 || authResponse[1] != 0x00)
                {
                    throw new Exception("SOCKS5 authentication failed");
                }
            }
            else
            {
                // No authentication (standard SOCKS5)
                handshake = new byte[] { 0x05, 0x01, 0x00 }; // SOCKS5, 1 method, no auth
                await stream.WriteAsync(handshake, 0, handshake.Length, effectiveToken);

                var handshakeResponse = new byte[2];
                await ReadExactlyAsync(stream, handshakeResponse, 0, 2, effectiveToken);

                if (handshakeResponse[0] != 0x05 || handshakeResponse[1] != 0x00)
                {
                    throw new Exception("SOCKS5 handshake failed");
                }
            }

            // SOCKS5 connect request
            var connectRequest = CreateSocks5ConnectRequest(host, port);
            await stream.WriteAsync(connectRequest, 0, connectRequest.Length, effectiveToken);

            await ReadSocks5ConnectResponseAsync(stream, effectiveToken);

            lock (_statusLock)
            {
                _status.TotalConnectionsSuccessful++;
                _status.ActiveConnections++;
                _status.LastSuccessfulConnection = DateTimeOffset.UtcNow;
            }

            _logger.LogDebug("Established Tor connection to {Host}:{Port} via circuit {CircuitKey}", host, port, circuitKey);

            var ownedClient = client;
            client = null;
            var returnCircuit = leasedCircuit;
            leasedCircuit = null;

            // Return a wrapper stream that tracks when the connection is closed
            return new TrackedStream(stream, () =>
            {
                lock (_statusLock)
                {
                    _status.ActiveConnections = Math.Max(0, _status.ActiveConnections - 1);
                }

                returnCircuit.ReleaseConnection(ownedClient);
            });
        }
        catch (OperationCanceledException ex) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            client?.Dispose();

            lock (_statusLock)
            {
                _status.LastError = $"Timed out establishing Tor connection after {ConnectTimeout.TotalSeconds:0} seconds";
            }

            throw new TimeoutException(
                $"Timed out establishing Tor connection to {host}:{port} via circuit {circuitKey}",
                ex);
        }
        catch (Exception ex)
        {
            client?.Dispose();

            lock (_statusLock)
            {
                _status.LastError = "Tor connection failed";
            }

            _logger.LogError(ex, "Failed to establish Tor connection to {Host}:{Port} via circuit {CircuitKey}", host, port, circuitKey);
            throw;
        }
    }

    private static async Task ReadSocks5ConnectResponseAsync(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[4];
        await ReadExactlyAsync(stream, header, 0, header.Length, cancellationToken);

        if (header[0] != 0x05)
        {
            throw new Exception($"Invalid SOCKS5 response version: {header[0]:X2}");
        }

        if (header[1] != 0x00)
        {
            throw new Exception($"SOCKS5 connect failed with response code {header[1]:X2}");
        }

        int bytesToRead;
        var addressType = header[3];
        switch (addressType)
        {
            case 0x01:
                bytesToRead = 4 + 2;
                break;
            case 0x03:
                var length = new byte[1];
                await ReadExactlyAsync(stream, length, 0, 1, cancellationToken);
                bytesToRead = length[0] + 2;
                break;
            case 0x04:
                bytesToRead = 16 + 2;
                break;
            default:
                throw new Exception($"Unsupported SOCKS5 address type: {addressType:X2}");
        }

        var tail = new byte[bytesToRead];
        await ReadExactlyAsync(stream, tail, 0, tail.Length, cancellationToken);
    }

    /// <summary>
    /// Gets the current status of the Tor transport.
    /// </summary>
    public AnonymityTransportStatus GetStatus()
    {
        lock (_statusLock)
        {
            return new AnonymityTransportStatus
            {
                IsAvailable = _status.IsAvailable,
                LastError = _status.LastError,
                LastSuccessfulConnection = _status.LastSuccessfulConnection,
                ActiveConnections = _status.ActiveConnections,
                TotalConnectionsAttempted = _status.TotalConnectionsAttempted,
                TotalConnectionsSuccessful = _status.TotalConnectionsSuccessful,
            };
        }
    }

    /// <summary>
    /// Disposes the transport and cleans up circuit pools.
    /// </summary>
    public void Dispose()
    {
        lock (_circuitLock)
        {
            foreach (var circuit in _circuitPool.Values)
            {
                circuit.Dispose();
            }

            _circuitPool.Clear();
        }
    }

    private static byte[] CreateSocks5ConnectRequest(string host, int port)
    {
        // Determine address type
        byte addressType;
        byte[] addressBytes;

        if (System.Net.IPAddress.TryParse(host, out var ipAddress))
        {
            if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                addressType = 0x01; // IPv4
                addressBytes = ipAddress.GetAddressBytes();
            }
            else
            {
                addressType = 0x04; // IPv6
                addressBytes = ipAddress.GetAddressBytes();
            }
        }
        else
        {
            addressType = 0x03; // Domain name
            var hostBytes = System.Text.Encoding.UTF8.GetBytes(host);
            if (hostBytes.Length is 0 or > 255)
            {
                throw new ArgumentException("SOCKS5 host name must be between 1 and 255 bytes", nameof(host));
            }

            addressBytes = new byte[1 + hostBytes.Length];
            addressBytes[0] = (byte)hostBytes.Length;
            Array.Copy(hostBytes, 0, addressBytes, 1, hostBytes.Length);
        }

        // Create SOCKS5 connect request
        var request = new List<byte>
        {
            0x05, // SOCKS5
            0x01, // Connect command
            0x00, // Reserved
            addressType, // Address type
        };

        request.AddRange(addressBytes);
        request.Add((byte)(port >> 8)); // Port high byte
        request.Add((byte)(port & 0xFF)); // Port low byte

        return request.ToArray();
    }

    private static async Task SendSocks5AuthAsync(Stream stream, string username, string password, CancellationToken cancellationToken)
    {
        var usernameBytes = System.Text.Encoding.UTF8.GetBytes(username);
        var passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);
        if (usernameBytes.Length is 0 or > 255)
        {
            throw new ArgumentException("SOCKS5 username must be between 1 and 255 bytes", nameof(username));
        }

        if (passwordBytes.Length is 0 or > 255)
        {
            throw new ArgumentException("SOCKS5 password must be between 1 and 255 bytes", nameof(password));
        }

        var authRequest = new List<byte>
        {
            0x01, // Version 1
            (byte)usernameBytes.Length,
        };

        authRequest.AddRange(usernameBytes);
        authRequest.Add((byte)passwordBytes.Length);
        authRequest.AddRange(passwordBytes);

        await stream.WriteAsync(authRequest.ToArray(), 0, authRequest.Count, cancellationToken);
    }

    internal static string GenerateIsolationUsername(string isolationKey)
    {
        // Generate a deterministic username based on the isolation key
        // This ensures the same peer always gets the same username (and thus same Tor circuit)
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(isolationKey);
        var hash = sha256.ComputeHash(keyBytes);
        return "tor-" + BitConverter.ToString(hash).Replace("-", string.Empty).Substring(0, 16).ToLower();
    }

    internal static string GenerateIsolationPassword(string isolationKey)
    {
        // Generate a deterministic password based on the isolation key
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(isolationKey + "-password");
        var hash = sha256.ComputeHash(keyBytes);
        return BitConverter.ToString(hash).Replace("-", string.Empty).Substring(0, 16).ToLower();
    }

    private static async Task ReadExactlyAsync(
        Stream stream,
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        var remaining = count;
        while (remaining > 0)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset + (count - remaining), remaining), cancellationToken);
            if (read == 0)
            {
                throw new EndOfStreamException("Unexpected end of stream while reading SOCKS5 response");
            }

            remaining -= read;
        }
    }

    /// <summary>
    /// Gets or creates an isolated circuit for the given key.
    /// </summary>
    private async Task<IsolationCircuit> GetOrCreateCircuitAsync(string circuitKey, CancellationToken cancellationToken)
    {
        // Check if we already have a circuit for this key
        lock (_circuitLock)
        {
            if (_circuitPool.TryGetValue(circuitKey, out var existingCircuit) && existingCircuit.IsHealthy())
            {
                existingCircuit.LastUsed = DateTimeOffset.UtcNow;
                return existingCircuit;
            }
        }

        // Create a new circuit
        var newCircuit = await CreateIsolationCircuitAsync(circuitKey, cancellationToken);

        lock (_circuitLock)
        {
            // Clean up old circuits (keep max 10 active circuits)
            if (_circuitPool.Count >= 10)
            {
                var oldestKey = _circuitPool.OrderBy(kvp => kvp.Value.LastUsed).First().Key;
                _circuitPool.TryRemove(oldestKey, out var oldCircuit);
                oldCircuit?.Dispose();
            }

            _circuitPool[circuitKey] = newCircuit;
        }

        return newCircuit;
    }

    /// <summary>
    /// Creates a new isolated circuit.
    /// </summary>
    private Task<IsolationCircuit> CreateIsolationCircuitAsync(string circuitKey, CancellationToken cancellationToken)
    {
        var (socksHost, socksPort) = ParseSocksAddress(_options.SocksAddress);

        if (_options.IsolateStreams)
        {
            // For stream isolation, we create circuits with different credentials
            // In a full implementation, this would start separate Tor processes or use control port
            var username = GenerateIsolationUsername(circuitKey);
            var password = GenerateIsolationPassword(circuitKey);

            _logger.LogDebug("Creating isolated circuit for key {CircuitKey} with credentials", circuitKey);

            // For now, use the same SOCKS port but with authentication
            // In production, this would use separate Tor instances or control port circuit isolation
            return Task.FromResult(new IsolationCircuit(
                socksHost,
                socksPort,
                username,
                password,
                circuitKey));
        }
        else
        {
            // No isolation - use shared circuit
            return Task.FromResult(new IsolationCircuit(
                socksHost,
                socksPort,
                null,
                null,
                circuitKey));
        }
    }

    private static (string Host, int Port) ParseSocksAddress(string socksAddress)
    {
        if (string.IsNullOrWhiteSpace(socksAddress))
        {
            throw new InvalidOperationException("Tor SOCKS address is not configured");
        }

        if (!TryParseHostAndPort(socksAddress, out var host, out var socksPort))
        {
            throw new InvalidOperationException($"Invalid Tor SOCKS address format: {socksAddress}");
        }

        return (host, socksPort);
    }

    private static bool TryParseHostAndPort(string address, out string host, out int port)
    {
        host = string.Empty;
        port = 0;

        if (string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        string portPart;
        if (address.StartsWith("[", StringComparison.Ordinal))
        {
            var closingBracketIndex = address.IndexOf(']');
            if (closingBracketIndex <= 1 || closingBracketIndex >= address.Length - 2 || address[closingBracketIndex + 1] != ':')
            {
                return false;
            }

            host = address[1..closingBracketIndex];
            portPart = address[(closingBracketIndex + 2)..];
        }
        else
        {
            var separatorIndex = address.LastIndexOf(':');
            if (separatorIndex <= 0 || separatorIndex == address.Length - 1)
            {
                return false;
            }

            host = address[..separatorIndex];
            portPart = address[(separatorIndex + 1)..];
        }

        return int.TryParse(portPart, out port) && port is > 0 and <= ushort.MaxValue;
    }

    /// <summary>
    /// Represents an isolated Tor circuit for stream isolation.
    /// </summary>
    private class IsolationCircuit : IDisposable
    {
        public string SocksHost { get; }
        public int SocksPort { get; }
        public string? Username { get; }
        public string? Password { get; }
        public string CircuitKey { get; }
        public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
        public DateTimeOffset LastUsed { get; set; } = DateTimeOffset.UtcNow;

        private readonly ConcurrentBag<TcpClient> _connectionPool = new();
        private readonly object _poolLock = new();
        private bool _disposed;

        public IsolationCircuit(string socksHost, int socksPort, string? username, string? password, string circuitKey)
        {
            SocksHost = socksHost;
            SocksPort = socksPort;
            Username = username;
            Password = password;
            CircuitKey = circuitKey;
        }

        /// <summary>
        /// Checks if this circuit is still healthy and usable.
        /// </summary>
        public bool IsHealthy()
        {
            // Circuit expires after 30 minutes of inactivity or 2 hours total
            var now = DateTimeOffset.UtcNow;
            return !_disposed &&
                   now - LastUsed < TimeSpan.FromMinutes(30) &&
                   now - CreatedAt < TimeSpan.FromHours(2);
        }

        /// <summary>
        /// Gets a connection from the pool or creates a new one.
        /// </summary>
        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Healthy pooled connections are returned to the caller; unhealthy pooled connections are disposed before replacement.")]
        public TcpClient GetConnection()
        {
            TcpClient? pooledClient = null;

            lock (_poolLock)
            {
                if (_connectionPool.TryTake(out pooledClient))
                {
                    if (IsConnectionHealthy(pooledClient))
                    {
                        return pooledClient;
                    }
                }
            }

            pooledClient?.Dispose();

            // Create new connection
            return new TcpClient();
        }

        /// <summary>
        /// Returns a connection to the pool for reuse.
        /// </summary>
        public void ReleaseConnection(TcpClient client)
        {
            if (_disposed || !IsConnectionHealthy(client))
            {
                client.Dispose();
                return;
            }

            lock (_poolLock)
            {
                // Max 5 connections per circuit.
                if (_connectionPool.Count < 5)
                {
                    _connectionPool.Add(client);
                }
                else
                {
                    client.Dispose();
                }
            }
        }

        private bool IsConnectionHealthy(TcpClient client)
        {
            try
            {
                return client.Connected && client.Client != null && !client.Client.Poll(1, SelectMode.SelectRead);
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            lock (_poolLock)
            {
                foreach (var client in _connectionPool)
                {
                    try
                    {
                        client.Dispose();
                    }
                    catch
                    {
                        // Ignore disposal errors
                    }
                }

                _connectionPool.Clear();
            }
        }
    }

    /// <summary>
    /// Stream wrapper that tracks connection lifecycle.
    /// </summary>
    private class TrackedStream : Stream
    {
        private readonly Stream _innerStream;
        private readonly Action _onDispose;
        private bool _disposed;

        public TrackedStream(Stream innerStream, Action onDispose)
        {
            _innerStream = innerStream;
            _onDispose = onDispose;
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
                try
                {
                    _onDispose();
                }
                catch
                {
                    // noop
                }

                _innerStream.Dispose();
            }

            base.Dispose(disposing);
        }

        // Delegate all other methods to inner stream
        public override bool CanRead => _innerStream.CanRead;
        public override bool CanSeek => _innerStream.CanSeek;
        public override bool CanWrite => _innerStream.CanWrite;
        public override long Length => _innerStream.Length;
        public override long Position { get => _innerStream.Position; set => _innerStream.Position = value; }
        public override void Flush() => _innerStream.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _innerStream.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _innerStream.Seek(offset, origin);
        public override void SetLength(long value) => _innerStream.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _innerStream.Write(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _innerStream.ReadAsync(buffer, offset, count, cancellationToken);
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _innerStream.WriteAsync(buffer, offset, count, cancellationToken);
        public override Task FlushAsync(CancellationToken cancellationToken) =>
            _innerStream.FlushAsync(cancellationToken);
    }
}
