#pragma warning disable CA2252 // Preview features - QUIC APIs require preview features

namespace slskd.Mesh.Overlay;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// QUIC overlay client for control-plane messages (ControlEnvelope).
/// </summary>
public class QuicOverlayClient : IOverlayClient
{
    private readonly ILogger<QuicOverlayClient> logger;
    private readonly OverlayOptions options;
    private readonly ConcurrentDictionary<IPEndPoint, QuicConnection> connections = new();

    public QuicOverlayClient(
        ILogger<QuicOverlayClient> logger,
        IOptions<OverlayOptions> options,
        IControlSigner signer)
    {
        this.logger = logger;
        this.options = options.Value;
    }

    /// <summary>
    /// Gets the count of active client connections (for metrics).
    /// </summary>
    public int GetActiveConnectionCount() => connections.Count;

    public async Task<bool> SendAsync(ControlEnvelope envelope, IPEndPoint endpoint, CancellationToken ct = default)
    {
        if (!options.Enable)
        {
            return false;
        }

        if (!QuicConnection.IsSupported)
        {
            logger.LogDebug("[Overlay-QUIC] QUIC not supported, skipping send to {Endpoint}", endpoint);
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

            // Serialize envelope
            var payload = MessagePackSerializer.Serialize(envelope);
            if (payload.Length > options.MaxDatagramBytes)
            {
                logger.LogWarning("[Overlay-QUIC] Refusing to send oversized envelope size={Size}", payload.Length);
                return false;
            }

            // Open stream and send
            await using var stream = await connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, ct);
            await stream.WriteAsync(payload, ct);
            // Stream will be closed when disposed

            logger.LogDebug("[Overlay-QUIC] Sent control {Type} to {Endpoint}", envelope.Type, endpoint);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Overlay-QUIC] Failed to send to {Endpoint}", endpoint);
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
                DefaultStreamErrorCode = 0x01,
                DefaultCloseErrorCode = 0x01,
                ClientAuthenticationOptions = new SslClientAuthenticationOptions
                {
                    ApplicationProtocols = new List<SslApplicationProtocol> { new SslApplicationProtocol("slskdn-overlay") },
                    RemoteCertificateValidationCallback = (sender, certificate, chain, errors) => true // Accept self-signed certs
                }
            };

            var connection = await QuicConnection.ConnectAsync(clientOptions, ct);
            logger.LogDebug("[Overlay-QUIC] Connected to {Endpoint}", endpoint);
            return connection;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Overlay-QUIC] Failed to connect to {Endpoint}", endpoint);
            return null;
        }
    }
}
