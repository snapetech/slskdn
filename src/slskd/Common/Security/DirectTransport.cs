// <copyright file="DirectTransport.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace slskd.Common.Security;

/// <summary>
/// Direct TLS transport for clearnet connections to mesh overlay listeners.
/// </summary>
public sealed class DirectTransport : IAnonymityTransport
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan TlsHandshakeTimeout = TimeSpan.FromSeconds(5);

    private readonly ILogger<DirectTransport> _logger;
    private readonly AnonymityTransportStatus _status = new();
    private readonly object _statusLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="DirectTransport"/> class.
    /// </summary>
    public DirectTransport(ILogger<DirectTransport> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public AnonymityTransportType TransportType => AnonymityTransportType.Direct;

    /// <inheritdoc/>
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        lock (_statusLock)
        {
            _status.IsAvailable = true;
            _status.LastError = null;
        }

        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task<Stream> ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        return ConnectAsync(host, port, isolationKey: null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Stream> ConnectAsync(string host, int port, string? isolationKey, CancellationToken cancellationToken = default)
    {
        TcpClient? client = null;
        SslStream? sslStream = null;

        lock (_statusLock)
        {
            _status.TotalConnectionsAttempted++;
        }

        try
        {
            client = new TcpClient();

            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(ConnectTimeout);
            await client.ConnectAsync(host, port, connectCts.Token);

            sslStream = new SslStream(
                client.GetStream(),
                leaveInnerStreamOpen: false,
                (_, certificate, chain, errors) => IsAllowedMeshDirectCertificate(certificate, chain, errors));

            using var tlsCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            tlsCts.CancelAfter(TlsHandshakeTimeout);
            await sslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = "slskdn-overlay",
                EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls13,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
            }, tlsCts.Token);

            lock (_statusLock)
            {
                _status.IsAvailable = true;
                _status.LastError = null;
                _status.LastSuccessfulConnection = DateTimeOffset.UtcNow;
                _status.TotalConnectionsSuccessful++;
                _status.ActiveConnections++;
            }

            _logger.LogDebug("Established direct TLS transport to {Host}:{Port}", host, port);

            var ownedClient = client;
            var ownedSslStream = sslStream;
            client = null;
            sslStream = null;

            return new TrackedStream(ownedSslStream, ownedClient, () =>
            {
                lock (_statusLock)
                {
                    _status.ActiveConnections = Math.Max(0, _status.ActiveConnections - 1);
                }
            });
        }
        catch (Exception ex)
        {
            lock (_statusLock)
            {
                _status.IsAvailable = true;
                _status.LastError = ex.Message;
            }

            _logger.LogDebug(ex, "Direct TLS transport failed to connect to {Host}:{Port}", host, port);
            throw;
        }
        finally
        {
            sslStream?.Dispose();
            client?.Dispose();
        }
    }

    /// <inheritdoc/>
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

    private static bool IsAllowedMeshDirectCertificate(X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
    {
        if (certificate == null)
        {
            return false;
        }

        if (sslPolicyErrors == SslPolicyErrors.None)
        {
            return true;
        }

        if (sslPolicyErrors != SslPolicyErrors.RemoteCertificateChainErrors)
        {
            return false;
        }

        var certificate2 = certificate as X509Certificate2;
        if (certificate2 == null || !string.Equals(certificate2.Subject, certificate2.Issuer, StringComparison.Ordinal))
        {
            return false;
        }

        if (chain == null || chain.ChainStatus.Length == 0)
        {
            return false;
        }

        foreach (var status in chain.ChainStatus)
        {
            if (status.Status != X509ChainStatusFlags.UntrustedRoot &&
                status.Status != X509ChainStatusFlags.PartialChain)
            {
                return false;
            }
        }

        return true;
    }

    private sealed class TrackedStream : Stream
    {
        private readonly Stream _inner;
        private readonly TcpClient _client;
        private readonly Action _onDispose;
        private int _disposed;

        public TrackedStream(Stream inner, TcpClient client, Action onDispose)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _onDispose = onDispose ?? throw new ArgumentNullException(nameof(onDispose));
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }
        public override void Flush() => _inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer) => _inner.Read(buffer);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => _inner.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
        public override void Write(ReadOnlySpan<byte> buffer) => _inner.Write(buffer);
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _inner.WriteAsync(buffer, offset, count, cancellationToken);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => _inner.WriteAsync(buffer, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                try
                {
                    _inner.Dispose();
                }
                finally
                {
                    _client.Dispose();
                    _onDispose();
                }
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                try
                {
                    await _inner.DisposeAsync();
                }
                finally
                {
                    _client.Dispose();
                    _onDispose();
                }
            }

            await base.DisposeAsync();
        }
    }
}
