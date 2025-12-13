#pragma warning disable CA2252 // Preview features - QUIC APIs require preview features

namespace slskd.Mesh.Overlay;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
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
    private readonly Security.IPeerPinCache peerPinCache;
    private readonly Security.ITofuPinStore tofuPinStore;
    private readonly ConcurrentDictionary<IPEndPoint, QuicConnection> connections = new();

    public QuicOverlayClient(
        ILogger<QuicOverlayClient> logger,
        IOptions<OverlayOptions> options,
        IControlSigner signer,
        Security.IPeerPinCache peerPinCache,
        Security.ITofuPinStore tofuPinStore)
    {
        this.logger = logger;
        this.options = options.Value;
        this.peerPinCache = peerPinCache;
        this.tofuPinStore = tofuPinStore;
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
                    RemoteCertificateValidationCallback = (sender, certificate, chain, errors) =>
                    {
                        // SPKI pinning validation
                        if (certificate == null)
                        {
                            logger.LogWarning("[Overlay-QUIC] No certificate presented by {Endpoint}", endpoint);
                            return false;
                        }

                        var cert = new X509Certificate2(certificate);

                        // Check certificate time validity
                        if (cert.NotBefore > DateTime.UtcNow || cert.NotAfter < DateTime.UtcNow)
                        {
                            logger.LogWarning("[Overlay-QUIC] Certificate time invalid for {Endpoint}", endpoint);
                            return false;
                        }

                        var actualSpki = Security.CertificatePins.ComputeSpkiSha256Base64(cert);

                        // Try descriptor-based pinning first
                        var expectedSpki = peerPinCache.GetExpectedControlSpkiAsync(endpoint, ct).GetAwaiter().GetResult();

                        if (expectedSpki != null)
                        {
                            // Descriptor available - strict validation
                            if (actualSpki != expectedSpki)
                            {
                                logger.LogError("[Overlay-QUIC] SPKI mismatch for {Endpoint}: expected {Expected}, got {Actual}",
                                    endpoint, expectedSpki, actualSpki);
                                return false;
                            }

                            logger.LogDebug("[Overlay-QUIC] Descriptor-based pinning validated for {Endpoint}", endpoint);
                            return true;
                        }

                        // Fallback to TOFU
                        var (existingPin, recordedAt) = tofuPinStore.GetPin(endpoint, Security.TofuPinType.Control);

                        if (existingPin != null)
                        {
                            // We've seen this endpoint before - enforce pin
                            if (actualSpki != existingPin)
                            {
                                logger.LogError("[Overlay-QUIC] TOFU pin violation for {Endpoint}: recorded {Recorded} on {Date}, got {Actual}",
                                    endpoint, existingPin, recordedAt, actualSpki);
                                return false;
                            }

                            logger.LogDebug("[Overlay-QUIC] TOFU pin validated for {Endpoint}", endpoint);
                            return true;
                        }

                        // First connection - record pin and accept
                        tofuPinStore.RecordPin(endpoint, actualSpki, Security.TofuPinType.Control);
                        logger.LogWarning("[Overlay-QUIC] No descriptor for {Endpoint}, recording TOFU pin: {Pin}",
                            endpoint, actualSpki);
                        return true;
                    }
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
