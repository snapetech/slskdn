// <copyright file="RelayOnlyTransport.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Security;

using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using slskd.Mesh.Overlay;

/// <summary>
/// Relay-only transport that routes all connections through trusted relay nodes via the data overlay.
/// Never reveals the user's IP address to the destination peer.
/// </summary>
public class RelayOnlyTransport : IAnonymityTransport
{
    private readonly RelayOnlyOptions _options;
    private readonly IOverlayDataPlane _overlay;
    private readonly ILogger<RelayOnlyTransport> _logger;

    private readonly AnonymityTransportStatus _status = new();
    private readonly object _statusLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="RelayOnlyTransport"/> class.
    /// </summary>
    /// <param name="options">The relay-only options.</param>
    /// <param name="overlay">The data-plane overlay for opening streams to relay peers.</param>
    /// <param name="logger">The logger.</param>
    public RelayOnlyTransport(RelayOnlyOptions options, IOverlayDataPlane overlay, ILogger<RelayOnlyTransport> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if ((_options.RelayPeerDataEndpoints == null || _options.RelayPeerDataEndpoints.Count == 0) &&
            (_options.TrustedRelayPeers == null || _options.TrustedRelayPeers.Count == 0))
        {
            _logger.LogWarning("RelayOnlyTransport: No RelayPeerDataEndpoints or TrustedRelayPeers configured. Relay-only will not function.");
        }
    }

    /// <summary>
    /// Gets the transport type.
    /// </summary>
    public AnonymityTransportType TransportType => AnonymityTransportType.RelayOnly;

    /// <summary>
    /// Checks if relay-only transport is available (has RelayPeerDataEndpoints or TrustedRelayPeers as host:port).
    /// </summary>
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        var list = GetRelayEndpointList();
        var isAvailable = list.Count > 0;

        lock (_statusLock)
        {
            _status.IsAvailable = isAvailable;
            _status.LastError = isAvailable ? null : "No RelayPeerDataEndpoints or TrustedRelayPeers (host:port) configured";
        }

        return Task.FromResult(isAvailable);
    }

    private System.Collections.Generic.List<string> GetRelayEndpointList()
    {
        if (_options.RelayPeerDataEndpoints != null && _options.RelayPeerDataEndpoints.Count > 0)
            return _options.RelayPeerDataEndpoints;
        if (_options.TrustedRelayPeers != null)
            return _options.TrustedRelayPeers.Where(s => !string.IsNullOrEmpty(s) && s.Contains(':', StringComparison.Ordinal)).ToList();
        return new System.Collections.Generic.List<string>();
    }

    /// <summary>
    /// Establishes a connection through trusted relay nodes.
    /// </summary>
    /// <param name="host">The target host.</param>
    /// <param name="port">The target port.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A stream for the relayed connection.</returns>
    public async Task<Stream> ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        return await ConnectAsync(host, port, null, cancellationToken);
    }

    /// <summary>
    /// Establishes a connection through trusted relay nodes via the data overlay (RELAY_TCP protocol).
    /// </summary>
    /// <param name="host">The target host for the relay to connect to.</param>
    /// <param name="port">The target port.</param>
    /// <param name="isolationKey">Unused; reserved for future stream isolation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A stream that tunnels to the target host:port via the relay.</returns>
    public async Task<Stream> ConnectAsync(string host, int port, string? isolationKey, CancellationToken cancellationToken = default)
    {
        lock (_statusLock)
        {
            _status.TotalConnectionsAttempted++;
        }

        try
        {
            var list = GetRelayEndpointList();
            if (list.Count == 0)
                throw new InvalidOperationException("No RelayPeerDataEndpoints or TrustedRelayPeers (host:port) configured for relay-only transport.");

            var relayEndpoint = SelectRelayPeer(list);
            var endpoint = await ParseEndpointAsync(relayEndpoint, cancellationToken);

            _logger.LogDebug("Relay connection to {Host}:{Port} via relay {Relay}", host, port, relayEndpoint);

            var stream = await _overlay.OpenBidirectionalStreamAsync(endpoint, cancellationToken);
            if (stream == null)
                throw new InvalidOperationException("Failed to open overlay stream to relay " + relayEndpoint + ". Is the data overlay enabled?");

            var cmd = Encoding.ASCII.GetBytes("RELAY_TCP " + host + " " + port + "\n");
            await stream.WriteAsync(cmd, cancellationToken);

            var lineBuf = new byte[128];
            var n = 0;
            while (n < lineBuf.Length)
            {
                var r = await stream.ReadAsync(lineBuf.AsMemory(n, 1), cancellationToken);
                if (r == 0)
                {
                    break;
                }

                if (lineBuf[n] == (byte)'\n')
                {
                    n++;
                    break;
                }

                n += r;
            }

            var line = n > 0 ? Encoding.ASCII.GetString(lineBuf.AsSpan(0, n)).TrimEnd() : string.Empty;
            if (!line.StartsWith("OK", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Relay refused: " + (string.IsNullOrEmpty(line) ? "no response" : line));
            }

            lock (_statusLock)
            {
                _status.TotalConnectionsSuccessful++;
                _status.ActiveConnections++;
                _status.LastSuccessfulConnection = DateTimeOffset.UtcNow;
            }

            return new TrackedStream(stream, () =>
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
                _status.LastError = "Relay connection failed";
            }

            _logger.LogError(ex, "Failed to establish relay connection to {Host}:{Port}", host, port);
            throw;
        }
    }

    private static async Task<IPEndPoint> ParseEndpointAsync(string hostPort, CancellationToken ct)
    {
        if (!TryParseHostAndPort(hostPort, out var host, out var port))
        {
            throw new ArgumentException("Relay endpoint must be host:port: " + hostPort);
        }

        IPAddress ip;
        if (IPAddress.TryParse(host, out var a))
        {
            ip = a;
        }
        else
        {
            var he = await Dns.GetHostEntryAsync(host, ct);
            ip = he.AddressList.FirstOrDefault() ?? throw new InvalidOperationException("Could not resolve relay host: " + host);
        }

        return new IPEndPoint(ip, port);
    }

    private static bool TryParseHostAndPort(string hostPort, out string host, out int port)
    {
        host = string.Empty;
        port = 0;

        if (string.IsNullOrWhiteSpace(hostPort))
        {
            return false;
        }

        string portPart;
        if (hostPort.StartsWith("[", StringComparison.Ordinal))
        {
            var closingBracketIndex = hostPort.IndexOf(']');
            if (closingBracketIndex <= 1 || closingBracketIndex >= hostPort.Length - 2 || hostPort[closingBracketIndex + 1] != ':')
            {
                return false;
            }

            host = hostPort[1..closingBracketIndex];
            portPart = hostPort[(closingBracketIndex + 2)..];
        }
        else
        {
            var separatorIndex = hostPort.LastIndexOf(':');
            if (separatorIndex <= 0 || separatorIndex == hostPort.Length - 1)
            {
                return false;
            }

            host = hostPort[..separatorIndex];
            portPart = hostPort[(separatorIndex + 1)..];
        }

        return int.TryParse(portPart, out port) && port is > 0 and <= ushort.MaxValue;
    }

    /// <summary>
    /// Gets the current status of the relay-only transport.
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

    private static string SelectRelayPeer(System.Collections.Generic.List<string> list)
    {
        return list[Random.Shared.Next(list.Count)];
    }

    private sealed class TrackedStream : Stream
    {
        private readonly Stream _inner;
        private readonly Action _onDispose;
        private bool _disposed;

        public TrackedStream(Stream inner, Action onDispose)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _onDispose = onDispose ?? throw new ArgumentNullException(nameof(onDispose));
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override void Flush() => _inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer) => _inner.Read(buffer);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => _inner.ReadAsync(buffer, cancellationToken);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
        public override void Write(ReadOnlySpan<byte> buffer) => _inner.Write(buffer);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => _inner.WriteAsync(buffer, cancellationToken);
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _inner.WriteAsync(buffer, offset, count, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                if (disposing)
                {
                    _inner.Dispose();
                }
            }
            finally
            {
                try
                {
                    _onDispose();
                }
                catch
                {
                    // noop
                }

                base.Dispose(disposing);
            }
        }

        public override async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                await _inner.DisposeAsync();
            }
            finally
            {
                try
                {
                    _onDispose();
                }
                catch
                {
                    // noop
                }

                await base.DisposeAsync();
            }
        }
    }
}
