// <copyright file="TorSocksDialer.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace slskd.Mesh.Transport;

/// <summary>
/// Tor SOCKS5 dialer for connecting through Tor to onion services.
/// </summary>
public class TorSocksDialer : ITransportDialer
{
    private readonly TorTransportOptions _options;
    private readonly ILogger<TorSocksDialer> _logger;
    private readonly DialerStatistics _statistics = new();

    public TorSocksDialer(TorTransportOptions options, ILogger<TorSocksDialer> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _statistics.TransportType = TransportType.TorOnionQuic;
    }

    /// <summary>
    /// Gets the transport type this dialer handles.
    /// </summary>
    public TransportType TransportType => TransportType.TorOnionQuic;

    /// <summary>
    /// Determines if this dialer can handle the given transport endpoint.
    /// </summary>
    /// <param name="endpoint">The transport endpoint to check.</param>
    /// <returns>True if this dialer can handle Tor onion endpoints.</returns>
    public bool CanHandle(TransportEndpoint endpoint)
    {
        return endpoint.TransportType == TransportType.TorOnionQuic && endpoint.IsValid() && _options.Enabled;
    }

    /// <summary>
    /// Attempts to establish a connection through Tor SOCKS5 proxy to the onion endpoint.
    /// </summary>
    /// <param name="endpoint">The transport endpoint to connect to.</param>
    /// <param name="isolationKey">Optional isolation key for stream isolation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A connected stream through Tor to the onion service.</returns>
    public Task<Stream> DialAsync(TransportEndpoint endpoint, string? isolationKey = null, CancellationToken cancellationToken = default)
    {
        // Tor connections don't use certificate pinning (TCP proxy)
        return DialWithPinsAsync(endpoint, Array.Empty<string>(), isolationKey, cancellationToken);
    }

    /// <summary>
    /// Attempts to establish a connection through Tor with certificate pinning (no-op for Tor).
    /// </summary>
    /// <param name="endpoint">The transport endpoint to connect to.</param>
    /// <param name="certificatePins">Certificate pins (ignored for Tor).</param>
    /// <param name="isolationKey">Optional isolation key for stream isolation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A connected stream through Tor to the onion service.</returns>
    public async Task<Stream> DialWithPinsAsync(TransportEndpoint endpoint, IEnumerable<string> certificatePins, string? isolationKey = null, CancellationToken cancellationToken = default)
    {
        return await DialWithPeerValidationAsync(endpoint, "unknown-peer", isolationKey, cancellationToken);
    }

    public async Task<Stream> DialWithPeerValidationAsync(TransportEndpoint endpoint, string peerId, string? isolationKey = null, CancellationToken cancellationToken = default)
    {
        if (!CanHandle(endpoint))
        {
            throw new ArgumentException("Endpoint not supported by this dialer", nameof(endpoint));
        }

        // Certificate pins are ignored for Tor (TCP proxy)
        _statistics.TotalAttempts++;

        var startTime = DateTimeOffset.UtcNow;

        // Generate isolation key if stream isolation is enabled
        var effectiveIsolationKey = isolationKey;
        if (_options.EnableStreamIsolation && string.IsNullOrEmpty(effectiveIsolationKey))
        {
            // Use endpoint as isolation key for stream isolation
            effectiveIsolationKey = $"{endpoint.TransportType}:{endpoint.Host}:{endpoint.Port}";
        }

        try
        {
            _logger.LogDebugSafe("Establishing Tor connection to {Endpoint} via SOCKS5 proxy (isolated: {Isolation})",
                LoggingUtils.SafeTransportEndpoint(endpoint),
                string.IsNullOrEmpty(effectiveIsolationKey) ? "no" : "yes");

            var stream = await ConnectViaSocks5Async(endpoint.Host, endpoint.Port, effectiveIsolationKey, cancellationToken);

            var connectionTime = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            _statistics.AverageConnectionTimeMs = (_statistics.AverageConnectionTimeMs * _statistics.SuccessfulConnections + connectionTime) / (_statistics.SuccessfulConnections + 1);

            _statistics.SuccessfulConnections++;
            _statistics.ActiveConnections++;

            LoggingUtils.LogConnectionEstablished(_logger, "unknown-peer", $"{endpoint.Host}:{endpoint.Port}", endpoint.TransportType);
            return new TorStreamWrapper(stream, () => _statistics.ActiveConnections--);
        }
        catch (Exception ex)
        {
            _statistics.FailedConnections++;
            _statistics.LastError = LoggingUtils.SafeException(ex);
            LoggingUtils.LogConnectionFailed(_logger, "unknown-peer", $"{endpoint.Host}:{endpoint.Port}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Checks if the Tor SOCKS5 proxy is available.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the Tor proxy is accessible.</returns>
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _statistics.IsAvailable = false;
            _statistics.LastError = "Tor transport is disabled";
            return false;
        }

        try
        {
            // Test basic connectivity to the SOCKS proxy
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(_options.SocksHost, _options.SocksPort);
            var timeoutTask = Task.Delay(_options.ConnectionTimeout, cancellationToken);

            var completedTask = await Task.WhenAny(connectTask, timeoutTask);
            if (completedTask == timeoutTask)
            {
                throw new TimeoutException("Connection to Tor SOCKS proxy timed out");
            }

            await connectTask; // Ensure any exceptions are propagated

            _statistics.IsAvailable = true;
            _statistics.LastError = null;
            return true;
        }
        catch (Exception ex)
        {
            _statistics.IsAvailable = false;
            _statistics.LastError = ex.Message;
            _logger.LogWarning(ex, "Tor SOCKS proxy not available at {Host}:{Port}", _options.SocksHost, _options.SocksPort);
            return false;
        }
    }

    /// <summary>
    /// Gets statistics about this dialer's usage.
    /// </summary>
    /// <returns>Dialer statistics.</returns>
    public DialerStatistics GetStatistics()
    {
        return new DialerStatistics
        {
            TransportType = _statistics.TransportType,
            TotalAttempts = _statistics.TotalAttempts,
            SuccessfulConnections = _statistics.SuccessfulConnections,
            FailedConnections = _statistics.FailedConnections,
            ActiveConnections = _statistics.ActiveConnections,
            AverageConnectionTimeMs = _statistics.AverageConnectionTimeMs,
            IsAvailable = _statistics.IsAvailable,
            LastError = _statistics.LastError
        };
    }

    private async Task<Stream> ConnectViaSocks5Async(string host, int port, string? isolationKey, CancellationToken cancellationToken)
    {
        var tcpClient = new TcpClient();
        try
        {
            // Connect to SOCKS proxy
            await tcpClient.ConnectAsync(_options.SocksHost, _options.SocksPort, cancellationToken);

            var stream = tcpClient.GetStream();
            tcpClient = null; // Prevent disposal

            // Perform SOCKS5 handshake
            await PerformSocks5HandshakeAsync(stream, host, port, isolationKey, cancellationToken);

            return stream;
        }
        catch
        {
            tcpClient?.Dispose();
            throw;
        }
    }

    private async Task PerformSocks5HandshakeAsync(Stream stream, string host, int port, string? isolationKey, CancellationToken cancellationToken)
    {
        // Validate hostname to prevent DNS leaks
        if (!IsValidOnionHostname(host))
        {
            throw new Exception($"Invalid hostname for Tor connection: {host}. Only .onion addresses are allowed.");
        }

        // SOCKS5 greeting - request authentication methods based on isolation key
        byte[] greeting;
        if (!string.IsNullOrEmpty(isolationKey))
        {
            // Request both no-auth and username/password methods
            greeting = new byte[] { 0x05, 0x02, 0x00, 0x02 }; // Version 5, 2 methods: no auth (0x00), username/password (0x02)
        }
        else
        {
            // No authentication needed
            greeting = new byte[] { 0x05, 0x01, 0x00 }; // Version 5, 1 method, no auth
        }

        await stream.WriteAsync(greeting, 0, greeting.Length, cancellationToken);

        // Read server response
        var response = new byte[2];
        await ReadExactlyAsync(stream, response, 0, 2, cancellationToken);

        if (response[0] != 0x05)
        {
            throw new Exception("Invalid SOCKS5 response version");
        }

        var selectedMethod = response[1];
        if (selectedMethod == 0xFF)
        {
            throw new Exception("No acceptable SOCKS5 authentication method");
        }

        // Perform authentication if username/password was selected
        if (selectedMethod == 0x02 && !string.IsNullOrEmpty(isolationKey))
        {
            await PerformUsernamePasswordAuthAsync(stream, isolationKey, cancellationToken);
        }
        else if (selectedMethod != 0x00)
        {
            throw new Exception($"Unsupported SOCKS5 authentication method: {selectedMethod:X2}");
        }

        // SOCKS5 connect request
        // For onion addresses, we use domain name format
        var hostBytes = System.Text.Encoding.UTF8.GetBytes(host);
        var request = new byte[7 + hostBytes.Length];
        request[0] = 0x05; // Version
        request[1] = 0x01; // Connect command
        request[2] = 0x00; // Reserved
        request[3] = 0x03; // Domain name address type
        request[4] = (byte)hostBytes.Length; // Domain name length
        Array.Copy(hostBytes, 0, request, 5, hostBytes.Length); // Domain name
        var portBytes = BitConverter.GetBytes((ushort)port);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(portBytes);
        }
        request[5 + hostBytes.Length] = portBytes[0]; // Port high byte
        request[6 + hostBytes.Length] = portBytes[1]; // Port low byte

        await stream.WriteAsync(request, 0, request.Length, cancellationToken);

        // Read connect response
        var connectResponse = new byte[10];
        await ReadExactlyAsync(stream, connectResponse, 0, 10, cancellationToken);

        if (connectResponse[0] != 0x05 || connectResponse[1] != 0x00)
        {
            throw new Exception($"SOCKS5 connect failed with error code {connectResponse[1]}");
        }

        _logger.LogDebug("SOCKS5 handshake completed for {Host}:{Port}{Isolation}", host, port,
            string.IsNullOrEmpty(isolationKey) ? "" : $" (isolated: {isolationKey})");
    }

    private static bool IsValidOnionHostname(string hostname)
    {
        // Only allow .onion addresses to prevent DNS leaks
        // .onion addresses are 56 characters long (v2) or 62 characters (v3) before .onion
        if (string.IsNullOrEmpty(hostname) || !hostname.EndsWith(".onion", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var onionPart = hostname.Substring(0, hostname.Length - 6); // Remove .onion

        // Check for valid onion address format (base32 encoded)
        if (onionPart.Length != 16 && onionPart.Length != 56) // v2 (16) or v3 (56) addresses
        {
            return false;
        }

        // Basic base32 validation (a-z, 2-7)
        return onionPart.All(c => (c >= 'a' && c <= 'z') || (c >= '2' && c <= '7'));
    }

    private async Task PerformUsernamePasswordAuthAsync(Stream stream, string isolationKey, CancellationToken cancellationToken)
    {
        // Generate deterministic username/password from isolation key
        // This ensures the same peer always gets the same credentials (important for circuit reuse)
        var (username, password) = GenerateCredentialsFromIsolationKey(isolationKey);

        // SOCKS5 username/password authentication (RFC 1929)
        var usernameBytes = System.Text.Encoding.UTF8.GetBytes(username);
        var passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);

        if (usernameBytes.Length > 255 || passwordBytes.Length > 255)
        {
            throw new Exception("Username or password too long for SOCKS5 authentication");
        }

        var authRequest = new byte[3 + usernameBytes.Length + passwordBytes.Length];
        authRequest[0] = 0x01; // Version
        authRequest[1] = (byte)usernameBytes.Length; // Username length
        Array.Copy(usernameBytes, 0, authRequest, 2, usernameBytes.Length); // Username
        authRequest[2 + usernameBytes.Length] = (byte)passwordBytes.Length; // Password length
        Array.Copy(passwordBytes, 0, authRequest, 3 + usernameBytes.Length, passwordBytes.Length); // Password

        await stream.WriteAsync(authRequest, 0, authRequest.Length, cancellationToken);

        // Read authentication response
        var authResponse = new byte[2];
        await ReadExactlyAsync(stream, authResponse, 0, 2, cancellationToken);

        if (authResponse[0] != 0x01 || authResponse[1] != 0x00)
        {
            throw new Exception("SOCKS5 username/password authentication failed");
        }

        _logger.LogDebug("SOCKS5 authentication successful for isolation key {IsolationKey}", isolationKey);
    }

    private (string Username, string Password) GenerateCredentialsFromIsolationKey(string isolationKey)
    {
        // Generate deterministic credentials from the isolation key
        // Use SHA256 to create a hash, then encode parts as username/password
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(isolationKey));

        // Username: first 16 bytes as base64 (22 chars)
        var username = Convert.ToBase64String(hash.AsSpan(0, 16)).TrimEnd('=');

        // Password: next 16 bytes as base64 (22 chars)
        var password = Convert.ToBase64String(hash.AsSpan(16, 16)).TrimEnd('=');

        return (username, password);
    }

    private async Task ReadExactlyAsync(Stream stream, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var totalRead = 0;
        while (totalRead < count)
        {
            var read = await stream.ReadAsync(buffer, offset + totalRead, count - totalRead, cancellationToken);
            if (read == 0)
            {
                throw new EndOfStreamException("Unexpected end of stream during SOCKS5 handshake");
            }
            totalRead += read;
        }
    }

    /// <summary>
    /// Wrapper for Tor streams that manages connection lifecycle.
    /// </summary>
    private class TorStreamWrapper : Stream
    {
        private readonly Stream _stream;
        private readonly Action _onDispose;
        private bool _disposed;

        public TorStreamWrapper(Stream stream, Action onDispose)
        {
            _stream = stream;
            _onDispose = onDispose;
        }

        public override bool CanRead => _stream.CanRead;
        public override bool CanSeek => _stream.CanSeek;
        public override bool CanWrite => _stream.CanWrite;
        public override long Length => _stream.Length;
        public override long Position
        {
            get => _stream.Position;
            set => _stream.Position = value;
        }

        public override void Flush() => _stream.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _stream.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _stream.Seek(offset, origin);
        public override void SetLength(long value) => _stream.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _stream.Write(buffer, offset, count);

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => await _stream.ReadAsync(buffer, offset, count, cancellationToken);

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => await _stream.WriteAsync(buffer, offset, count, cancellationToken);

        public override async Task FlushAsync(CancellationToken cancellationToken)
            => await _stream.FlushAsync(cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
                if (disposing)
                {
                    _stream.Dispose();
                    _onDispose?.Invoke();
                }
            }
            base.Dispose(disposing);
        }
    }
}
