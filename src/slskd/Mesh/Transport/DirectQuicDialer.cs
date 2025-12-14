// <copyright file="DirectQuicDialer.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Threading;

namespace slskd.Mesh.Transport;

/// <summary>
/// Direct QUIC dialer for clearnet connectivity.
/// Wraps the existing QUIC overlay client connection logic.
/// </summary>
public class DirectQuicDialer : ITransportDialer
{
    private readonly ILogger<DirectQuicDialer> _logger;
    private readonly CertificatePinManager _pinManager;
    private readonly DialerStatistics _statistics = new();

    public DirectQuicDialer(ILogger<DirectQuicDialer> logger, CertificatePinManager pinManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pinManager = pinManager ?? throw new ArgumentNullException(nameof(pinManager));
        _statistics.TransportType = TransportType.DirectQuic;
    }

    /// <summary>
    /// Gets the transport type this dialer handles.
    /// </summary>
    public TransportType TransportType => TransportType.DirectQuic;

    /// <summary>
    /// Determines if this dialer can handle the given transport endpoint.
    /// </summary>
    /// <param name="endpoint">The transport endpoint to check.</param>
    /// <returns>True if this dialer can handle DirectQuic endpoints.</returns>
    public bool CanHandle(TransportEndpoint endpoint)
    {
        return endpoint.TransportType == TransportType.DirectQuic && endpoint.IsValid();
    }

    /// <summary>
    /// Attempts to establish a direct QUIC connection to the given endpoint.
    /// </summary>
    /// <param name="endpoint">The transport endpoint to connect to.</param>
    /// <param name="isolationKey">Optional isolation key (ignored for direct connections).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A connected QUIC stream to the peer.</returns>
    public Task<Stream> DialAsync(TransportEndpoint endpoint, string? isolationKey = null, CancellationToken cancellationToken = default)
    {
        // Default to no certificate pins
        return DialWithPinsAsync(endpoint, Array.Empty<string>(), isolationKey, cancellationToken);
    }

    /// <summary>
    /// Attempts to establish a direct QUIC connection with certificate pinning.
    /// </summary>
    /// <param name="endpoint">The transport endpoint to connect to.</param>
    /// <param name="certificatePins">Certificate pins for validation.</param>
    /// <param name="isolationKey">Optional isolation key (ignored for direct connections).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A connected QUIC stream to the peer.</returns>
    public async Task<Stream> DialWithPinsAsync(TransportEndpoint endpoint, IEnumerable<string> certificatePins, string? isolationKey = null, CancellationToken cancellationToken = default)
    {
        return await DialWithPeerValidationAsync(endpoint, "unknown-peer", isolationKey, cancellationToken);
    }

    /// <summary>
    /// Attempts to establish a direct QUIC connection with peer-aware certificate pinning.
    /// </summary>
    /// <param name="endpoint">The transport endpoint to connect to.</param>
    /// <param name="peerId">The peer ID for certificate pin management.</param>
    /// <param name="isolationKey">Optional isolation key (ignored for direct connections).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A connected QUIC stream to the peer.</returns>
    public async Task<Stream> DialWithPeerValidationAsync(TransportEndpoint endpoint, string peerId, string? isolationKey = null, CancellationToken cancellationToken = default)
    {
        if (!CanHandle(endpoint))
        {
            throw new ArgumentException("Endpoint not supported by this dialer", nameof(endpoint));
        }

        Interlocked.Increment(ref _statistics.TotalAttempts);

        var startTime = DateTimeOffset.UtcNow;
        var ipEndpoint = new IPEndPoint(IPAddress.Parse(endpoint.Host), endpoint.Port);

        try
        {
            _logger.LogDebugSafe("Establishing direct QUIC connection to {Endpoint} for peer {PeerId}",
                LoggingUtils.SafeEndpoint(ipEndpoint.ToString()), LoggingUtils.SafePeerId(peerId));

            var connection = await CreateQuicConnectionWithPeerValidationAsync(ipEndpoint, peerId, cancellationToken);
            var stream = await connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, cancellationToken);

            var connectionTime = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            _statistics.AverageConnectionTimeMs = (_statistics.AverageConnectionTimeMs * _statistics.SuccessfulConnections + connectionTime) / (_statistics.SuccessfulConnections + 1);

            Interlocked.Increment(ref _statistics.SuccessfulConnections);
            Interlocked.Increment(ref _statistics.ActiveConnections);

            LoggingUtils.LogConnectionEstablished(_logger, peerId, ipEndpoint.ToString(), TransportType.DirectQuic);
            return new QuicStreamWrapper(stream, connection, () => Interlocked.Decrement(ref _statistics.ActiveConnections));
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _statistics.FailedConnections);
            _statistics.LastError = LoggingUtils.SafeException(ex);
            LoggingUtils.LogConnectionFailed(_logger, peerId, ipEndpoint.ToString(), ex.Message);
            throw;
        }
    }

    private async Task<QuicConnection> CreateQuicConnectionWithPeerValidationAsync(IPEndPoint endpoint, string peerId, CancellationToken cancellationToken)
    {
        var clientOptions = new QuicClientConnectionOptions
        {
            RemoteEndPoint = endpoint,
            DefaultStreamErrorCode = 0x01,
            DefaultCloseErrorCode = 0x01,
            ClientAuthenticationOptions = new SslClientAuthenticationOptions
            {
                ApplicationProtocols = new List<SslApplicationProtocol> { new SslApplicationProtocol("slskdn-overlay") },
                RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                {
                    // First check standard SSL validation
                    if (sslPolicyErrors != SslPolicyErrors.None)
                    {
                        _logger.LogDebugSafe("SSL validation failed for peer {PeerId}: {Errors}",
                            LoggingUtils.SafePeerId(peerId), sslPolicyErrors);
                        return false;
                    }

                    // Then check peer-aware certificate pinning
                    if (certificate != null)
                    {
                        var isValid = _pinManager.ValidateCertificatePin(peerId, certificate);
                        LoggingUtils.LogCertificateValidation(_logger, peerId, certificate, isValid);
                        return isValid;
                    }

                    _logger.LogDebugSafe("No certificate provided for peer {PeerId}", LoggingUtils.SafePeerId(peerId));
                    return false;
                }
            }
        };

        return await QuicConnection.ConnectAsync(clientOptions, cancellationToken);
    }
    /// <summary>
    /// Checks if direct QUIC connectivity is available.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if QUIC is supported on this platform.</returns>
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        var isAvailable = QuicConnection.IsSupported;
        _statistics.IsAvailable = isAvailable;

        if (!isAvailable)
        {
            _statistics.LastError = "QUIC not supported on this platform";
        }

        return Task.FromResult(isAvailable);
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

    private async Task<QuicConnection> CreateQuicConnectionAsync(IPEndPoint endpoint, IEnumerable<string> certificatePins, CancellationToken cancellationToken)
    {
        var clientOptions = new QuicClientConnectionOptions
        {
            RemoteEndPoint = endpoint,
            DefaultStreamErrorCode = 0x01,
            DefaultCloseErrorCode = 0x01,
            ClientAuthenticationOptions = new SslClientAuthenticationOptions
            {
                ApplicationProtocols = new List<SslApplicationProtocol> { new SslApplicationProtocol("slskdn-overlay") },
                RemoteCertificateValidationCallback = SecurityUtils.CreatePinningValidationCallback(certificatePins)
            }
        };

        return await QuicConnection.ConnectAsync(clientOptions, cancellationToken);
    }

    /// <summary>
    /// Wrapper for QUIC streams that manages connection lifecycle.
    /// </summary>
    private class QuicStreamWrapper : Stream
    {
        private readonly QuicStream _stream;
        private readonly QuicConnection _connection;
        private readonly Action _onDispose;
        private bool _disposed;

        public QuicStreamWrapper(QuicStream stream, QuicConnection connection, Action onDispose)
        {
            _stream = stream;
            _connection = connection;
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
                    _connection.Dispose();
                    _onDispose?.Invoke();
                }
            }
            base.Dispose(disposing);
        }
    }
}
