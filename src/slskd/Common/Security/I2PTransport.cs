// <copyright file="I2PTransport.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Net.Sockets;

namespace slskd.Common.Security;

/// <summary>
/// I2P SAM (Simple Anonymous Messaging) transport for anonymity.
/// </summary>
public class I2PTransport : IAnonymityTransport
{
    private readonly I2POptions _options;
    private readonly ILogger<I2PTransport> _logger;

    private readonly AnonymityTransportStatus _status = new();
    private readonly object _statusLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="I2PTransport"/> class.
    /// </summary>
    /// <param name="options">The I2P options.</param>
    /// <param name="logger">The logger.</param>
    public I2PTransport(I2POptions options, ILogger<I2PTransport> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the transport type.
    /// </summary>
    public AnonymityTransportType TransportType => AnonymityTransportType.I2P;

    /// <summary>
    /// Checks if the I2P SAM bridge is available and functional.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if I2P is available, false otherwise.</returns>
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);

            // Connect to SAM bridge
            var parts = _options.SamAddress.Split(':');
            var host = parts[0];
            var port = int.Parse(parts[1]);

            await client.ConnectAsync(host, port, linkedCts.Token);

            if (client.Connected)
            {
                var stream = client.GetStream();
                var writer = new StreamWriter(stream);
                var reader = new StreamReader(stream);

                // Send HELLO command to test SAM bridge
                await writer.WriteLineAsync("HELLO VERSION MIN=3.1 MAX=3.1");
                await writer.FlushAsync();

                var response = await reader.ReadLineAsync();
                if (response != null && response.Contains("RESULT=OK"))
                {
                    lock (_statusLock)
                    {
                        _status.IsAvailable = true;
                        _status.LastError = null;
                        _status.LastSuccessfulConnection = DateTimeOffset.UtcNow;
                    }
                    _logger.LogDebug("I2P SAM bridge is available at {Address}", _options.SamAddress);
                    return true;
                }
                else
                {
                    lock (_statusLock)
                    {
                        _status.IsAvailable = false;
                        _status.LastError = $"SAM bridge responded: {response}";
                    }
                    _logger.LogWarning("I2P SAM bridge rejected HELLO command: {Response}", response);
                    return false;
                }
            }

            lock (_statusLock)
            {
                _status.IsAvailable = false;
                _status.LastError = "Failed to connect to SAM bridge";
            }
            _logger.LogWarning("I2P SAM bridge not available at {Address}", _options.SamAddress);
            return false;
        }
        catch (Exception ex)
        {
            lock (_statusLock)
            {
                _status.IsAvailable = false;
                _status.LastError = ex.Message;
            }
            _logger.LogWarning(ex, "I2P SAM bridge not available at {Address}", _options.SamAddress);
            return false;
        }
    }

    /// <summary>
    /// Establishes a connection through I2P.
    /// </summary>
    /// <param name="host">The destination host (ignored for I2P).</param>
    /// <param name="port">The destination port (ignored for I2P).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A stream for the anonymous connection.</returns>
    public async Task<Stream> ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        return await ConnectAsync(host, port, null, cancellationToken);
    }

    /// <summary>
    /// Establishes a connection through I2P with stream isolation.
    /// </summary>
    /// <param name="host">I2P destination: base64-encoded destination key or .b32.i2p address. Must not be clearnet host:port.</param>
    /// <param name="port">Unused for I2P SAM STREAM CONNECT; reserved for API compatibility.</param>
    /// <param name="isolationKey">Optional key for stream isolation (SAM session ID suffix when we add named sessions).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A stream for the anonymous I2P connection.</returns>
    public async Task<Stream> ConnectAsync(string host, int port, string? isolationKey, CancellationToken cancellationToken = default)
    {
        lock (_statusLock)
        {
            _status.TotalConnectionsAttempted++;
        }

        TcpClient? client = null;
        try
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException("I2P destination (host) must be a base64 or .b32.i2p address.", nameof(host));

            client = new TcpClient();

            var parts = _options.SamAddress.Split(':');
            var samHost = parts[0];
            var samPort = int.Parse(parts[1]);

            await client.ConnectAsync(samHost, samPort, cancellationToken);
            var stream = client.GetStream();
            var writer = new StreamWriter(stream);
            var reader = new StreamReader(stream);

            // SAM v3.1 HELLO (one-shot; no named session)
            await writer.WriteLineAsync("HELLO VERSION MIN=3.1 MAX=3.1");
            await writer.FlushAsync();
            var helloResponse = await reader.ReadLineAsync();
            if (helloResponse == null || !helloResponse.Contains("RESULT=OK"))
                throw new InvalidOperationException($"SAM HELLO failed: {helloResponse}");

            // STREAM CONNECT: DESTINATION is I2P dest (base64 or .b32.i2p). Port is not used by SAM.
            var sessionId = Guid.NewGuid().ToString("N")[..8];
            var connectCmd = $"STREAM CONNECT ID={sessionId} DESTINATION={host.Trim()} SILENT=false";
            await writer.WriteLineAsync(connectCmd);
            await writer.FlushAsync();

            var connectResponse = await reader.ReadLineAsync();
            if (connectResponse == null)
                throw new InvalidOperationException("SAM STREAM CONNECT produced no response.");

            if (!connectResponse.Contains("RESULT=OK"))
            {
                var msg = connectResponse.Contains("MESSAGE=")
                    ? connectResponse
                    : $"STREAM CONNECT failed: {connectResponse}";
                throw new InvalidOperationException(msg);
            }

            lock (_statusLock)
            {
                _status.TotalConnectionsSuccessful++;
                _status.ActiveConnections++;
                _status.LastSuccessfulConnection = DateTimeOffset.UtcNow;
            }

            var c = client;
            client = null;
            return new TrackedStream(stream, () =>
            {
                lock (_statusLock)
                {
                    _status.ActiveConnections = Math.Max(0, _status.ActiveConnections - 1);
                }
                c.Dispose();
            });
        }
        catch (Exception ex)
        {
            lock (_statusLock)
            {
                _status.LastError = ex.Message;
            }
            _logger.LogError(ex, "Failed to establish I2P connection to destination {Host}", host);
            client?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Gets the current status of the I2P transport.
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
                _onDispose();
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
