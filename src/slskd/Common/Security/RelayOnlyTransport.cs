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
                if (r == 0) break;
                if (lineBuf[n] == (byte)'\n') { n++; break; }
                n += r;
            }
            var line = n > 0 ? Encoding.ASCII.GetString(lineBuf.AsSpan(0, n)).TrimEnd() : "";
            if (!line.StartsWith("OK", StringComparison.Ordinal))
                throw new InvalidOperationException("Relay refused: " + (string.IsNullOrEmpty(line) ? "no response" : line));

            lock (_statusLock)
            {
                _status.TotalConnectionsSuccessful++;
                _status.ActiveConnections++;
                _status.LastSuccessfulConnection = DateTimeOffset.UtcNow;
            }

            return stream;
        }
        catch (Exception ex)
        {
            lock (_statusLock)
            {
                _status.LastError = ex.Message;
            }
            _logger.LogError(ex, "Failed to establish relay connection to {Host}:{Port}", host, port);
            throw;
        }
    }

    private static async Task<IPEndPoint> ParseEndpointAsync(string hostPort, CancellationToken ct)
    {
        var idx = hostPort.LastIndexOf(':');
        if (idx < 0)
            throw new ArgumentException("Relay endpoint must be host:port: " + hostPort);
        var host = hostPort[..idx];
        var port = int.Parse(hostPort[(idx + 1)..]);

        IPAddress ip;
        if (IPAddress.TryParse(host, out var a))
            ip = a;
        else
        {
            var he = await Dns.GetHostEntryAsync(host, ct);
            ip = he.AddressList.FirstOrDefault() ?? throw new InvalidOperationException("Could not resolve relay host: " + host);
        }
        return new IPEndPoint(ip, port);
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
        return list[new Random().Next(list.Count)];
    }
}
