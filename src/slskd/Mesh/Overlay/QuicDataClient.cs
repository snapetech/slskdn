#pragma warning disable CA2252 // Preview features - QUIC APIs require preview features

namespace slskd.Mesh.Overlay;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// QUIC data-plane client for bulk payload transfers.
/// </summary>
public class QuicDataClient : IOverlayDataPlane
{
    private readonly ILogger<QuicDataClient> logger;
    private readonly DataOverlayOptions options;
    private readonly ConcurrentDictionary<IPEndPoint, QuicConnection> connections = new();

    public QuicDataClient(ILogger<QuicDataClient> logger, IOptions<DataOverlayOptions> options)
    {
        this.logger = logger;
        this.options = options.Value;
    }

    public async Task<bool> SendAsync(byte[] payload, IPEndPoint endpoint, CancellationToken ct = default)
    {
        if (!options.Enable)
        {
            return false;
        }

        if (!QuicConnection.IsSupported)
        {
            logger.LogDebug("[Overlay-QUIC-DATA] QUIC not supported, skipping send to {Endpoint}", endpoint);
            return false;
        }

        if (payload.Length > options.MaxPayloadBytes)
        {
            logger.LogWarning("[Overlay-QUIC-DATA] Payload too large: {Size} bytes (max {Max})", payload.Length, options.MaxPayloadBytes);
            return false;
        }

        try
        {
            // Get or create connection
            if (!connections.TryGetValue(endpoint, out var connection) || connection == null)
            {
                connection = await CreateConnectionAsync(endpoint, ct);
                if (connection == null)
                {
                    return false;
                }
                connections.TryAdd(endpoint, connection);
            }

            // Open stream and send payload
            await using var stream = await connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, ct);
            await stream.WriteAsync(payload, ct);
            // Stream will be closed when disposed

            logger.LogDebug("[Overlay-QUIC-DATA] Sent {Size} bytes to {Endpoint}", payload.Length, endpoint);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Overlay-QUIC-DATA] Failed to send to {Endpoint}", endpoint);
            connections.TryRemove(endpoint, out _);
            return false;
        }
    }

    private async Task<QuicConnection?> CreateConnectionAsync(IPEndPoint endpoint, CancellationToken ct)
    {
        try
        {
            var clientOptions = new QuicClientConnectionOptions
            {
                RemoteEndPoint = endpoint,
                DefaultStreamErrorCode = 0x02,
                DefaultCloseErrorCode = 0x02,
                ClientAuthenticationOptions = new SslClientAuthenticationOptions
                {
                    ApplicationProtocols = new List<SslApplicationProtocol> { new SslApplicationProtocol("slskdn-overlay-data") },
                    RemoteCertificateValidationCallback = (sender, certificate, chain, errors) => true // Accept self-signed certs
                }
            };

            var connection = await QuicConnection.ConnectAsync(clientOptions, ct);
            logger.LogDebug("[Overlay-QUIC-DATA] Connected to {Endpoint}", endpoint);
            return connection;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Overlay-QUIC-DATA] Failed to connect to {Endpoint}", endpoint);
            return null;
        }
    }
}
