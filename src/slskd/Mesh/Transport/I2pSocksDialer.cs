// <copyright file="I2pSocksDialer.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace slskd.Mesh.Transport;

/// <summary>
/// I2P SOCKS5 dialer for connecting through I2P to I2P destinations.
/// </summary>
public class I2pSocksDialer : ITransportDialer
{
    private readonly I2PTransportOptions _options;
    private readonly ILogger<I2pSocksDialer> _logger;
    private readonly DialerStatistics _statistics = new();

    public I2pSocksDialer(I2PTransportOptions options, ILogger<I2pSocksDialer> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _statistics.TransportType = TransportType.I2PQuic;
    }

    /// <summary>
    /// Gets the transport type this dialer handles.
    /// </summary>
    public TransportType TransportType => TransportType.I2PQuic;

    /// <summary>
    /// Determines if this dialer can handle the given transport endpoint.
    /// </summary>
    /// <param name="endpoint">The transport endpoint to check.</param>
    /// <returns>True if this dialer can handle I2P endpoints.</returns>
    public bool CanHandle(TransportEndpoint endpoint)
    {
        return endpoint.TransportType == TransportType.I2PQuic && endpoint.IsValid() && _options.Enabled;
    }

    /// <summary>
    /// Attempts to establish a connection through I2P SOCKS5 proxy to the I2P destination.
    /// </summary>
    /// <param name="endpoint">The transport endpoint to connect to.</param>
    /// <param name="isolationKey">Optional isolation key for stream isolation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A connected stream through I2P to the destination.</returns>
    public Task<Stream> DialAsync(TransportEndpoint endpoint, string? isolationKey = null, CancellationToken cancellationToken = default)
    {
        // I2P connections don't use certificate pinning (TCP proxy)
        return DialWithPinsAsync(endpoint, Array.Empty<string>(), isolationKey, cancellationToken);
    }

    /// <summary>
    /// Attempts to establish a connection through I2P with certificate pinning (no-op for I2P).
    /// </summary>
    /// <param name="endpoint">The transport endpoint to connect to.</param>
    /// <param name="certificatePins">Certificate pins (ignored for I2P).</param>
    /// <param name="isolationKey">Optional isolation key for stream isolation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A connected stream through I2P to the destination.</returns>
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

        // Certificate pins are ignored for I2P (TCP proxy)
        _statistics.TotalAttempts++;

        var startTime = DateTimeOffset.UtcNow;

        try
        {
            _logger.LogDebug("Establishing I2P connection to destination {Host}:{Port} via SOCKS5 {ProxyHost}:{ProxyPort}",
                endpoint.Host, endpoint.Port, _options.SocksHost, _options.SocksPort);

            var stream = await ConnectViaSocks5Async(endpoint.Host, endpoint.Port, isolationKey, cancellationToken);

            var connectionTime = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            _statistics.AverageConnectionTimeMs = (_statistics.AverageConnectionTimeMs * _statistics.SuccessfulConnections + connectionTime) / (_statistics.SuccessfulConnections + 1);

            _statistics.SuccessfulConnections++;
            _statistics.ActiveConnections++;

            _logger.LogDebug("I2P connection established to {Host}:{Port}", endpoint.Host, endpoint.Port);
            return new I2pStreamWrapper(stream, () => _statistics.ActiveConnections--);
        }
        catch (Exception ex)
        {
            _statistics.FailedConnections++;
            _statistics.LastError = ex.Message;
            _logger.LogWarning(ex, "Failed to establish I2P connection to {Host}:{Port}", endpoint.Host, endpoint.Port);
            throw;
        }
    }

    /// <summary>
    /// Checks if the I2P SOCKS5 proxy is available.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the I2P proxy is accessible.</returns>
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _statistics.IsAvailable = false;
            _statistics.LastError = "I2P transport is disabled";
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
                throw new TimeoutException("Connection to I2P SOCKS proxy timed out");
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
            _logger.LogWarning(ex, "I2P SOCKS proxy not available at {Host}:{Port}", _options.SocksHost, _options.SocksPort);
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

    private async Task<Stream> ConnectViaSocks5Async(string destination, int port, string? isolationKey, CancellationToken cancellationToken)
    {
        var tcpClient = new TcpClient();
        try
        {
            // Connect to SOCKS proxy
            await tcpClient.ConnectAsync(_options.SocksHost, _options.SocksPort, cancellationToken);

            var stream = tcpClient.GetStream();
            tcpClient = null; // Prevent disposal

            // Perform SOCKS5 handshake
            await PerformSocks5HandshakeAsync(stream, destination, port, isolationKey, cancellationToken);

            return stream;
        }
        catch
        {
            tcpClient?.Dispose();
            throw;
        }
    }

    private async Task PerformSocks5HandshakeAsync(Stream stream, string destination, int port, string? isolationKey, CancellationToken cancellationToken)
    {
        // Validate hostname to prevent DNS leaks
        if (!IsValidI2pHostname(destination))
        {
            throw new Exception($"Invalid hostname for I2P connection: {destination}. Only .i2p addresses are allowed.");
        }

        // SOCKS5 greeting (no authentication)
        var greeting = new byte[] { 0x05, 0x01, 0x00 }; // Version 5, 1 method, no auth
        await stream.WriteAsync(greeting, 0, greeting.Length, cancellationToken);

        // Read server response
        var response = new byte[2];
        await ReadExactlyAsync(stream, response, 0, 2, cancellationToken);

        if (response[0] != 0x05 || response[1] != 0x00)
        {
            throw new Exception("SOCKS5 authentication negotiation failed");
        }

        // SOCKS5 connect request
        // For I2P destinations, we treat them as domain names
        var destBytes = System.Text.Encoding.UTF8.GetBytes(destination);
        if (destBytes.Length > 255)
        {
            throw new ArgumentException("I2P destination is too long", nameof(destination));
        }

        var request = new byte[7 + destBytes.Length];
        request[0] = 0x05; // Version
        request[1] = 0x01; // Connect command
        request[2] = 0x00; // Reserved
        request[3] = 0x03; // Domain name address type
        request[4] = (byte)destBytes.Length; // Domain name length
        Array.Copy(destBytes, 0, request, 5, destBytes.Length); // Domain name
        var portBytes = BitConverter.GetBytes((ushort)port);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(portBytes);
        }
        request[5 + destBytes.Length] = portBytes[0]; // Port high byte
        request[6 + destBytes.Length] = portBytes[1]; // Port low byte

        await stream.WriteAsync(request, 0, request.Length, cancellationToken);

        // Read connect response
        var connectResponse = new byte[10];
        await ReadExactlyAsync(stream, connectResponse, 0, 10, cancellationToken);

        if (connectResponse[0] != 0x05 || connectResponse[1] != 0x00)
        {
            throw new Exception($"SOCKS5 connect failed with error code {connectResponse[1]}");
        }

        _logger.LogDebug("SOCKS5 handshake completed for I2P destination {Destination}:{Port}", destination, port);
    }

    private static bool IsValidI2pHostname(string hostname)
    {
        // Only allow .i2p addresses to prevent DNS leaks
        // I2P destinations can be base64-encoded or various formats, but typically end with .i2p
        if (string.IsNullOrEmpty(hostname) || !hostname.EndsWith(".i2p", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var i2pPart = hostname.Substring(0, hostname.Length - 4); // Remove .i2p

        // I2P destinations can be:
        // - Base64-encoded (long strings)
        // - Short local names
        // - Hashes
        // For security, we'll be conservative and only allow reasonable lengths
        if (i2pPart.Length < 1 || i2pPart.Length > 200)
        {
            return false;
        }

        // Allow alphanumeric, dots, hyphens, and underscores (common in I2P names)
        return i2pPart.All(c =>
            char.IsLetterOrDigit(c) ||
            c == '.' ||
            c == '-' ||
            c == '_');
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
    /// Wrapper for I2P streams that manages connection lifecycle.
    /// </summary>
    private class I2pStreamWrapper : Stream
    {
        private readonly Stream _stream;
        private readonly Action _onDispose;
        private bool _disposed;

        public I2pStreamWrapper(Stream stream, Action onDispose)
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
