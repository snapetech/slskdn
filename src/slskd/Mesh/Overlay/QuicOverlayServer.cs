#pragma warning disable CA2252 // Preview features - QUIC APIs require preview features

namespace slskd.Mesh.Overlay;

using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.Mesh.Security;

/// <summary>
/// QUIC overlay server for control-plane messages (ControlEnvelope).
/// </summary>
public class QuicOverlayServer : BackgroundService
{
    private readonly ILogger<QuicOverlayServer> logger;
    private readonly OverlayOptions options;
    private readonly IControlDispatcher dispatcher;
    private readonly IPeerEndpointRegistry endpointRegistry;
    private readonly IPeerPinCache pinCache;
    private readonly IMeshRateLimiter rateLimiter;
    private readonly ConcurrentDictionary<IPEndPoint, QuicConnection> activeConnections = new();

    public QuicOverlayServer(
        ILogger<QuicOverlayServer> logger,
        IOptions<OverlayOptions> options,
        IControlDispatcher dispatcher,
        IPeerEndpointRegistry endpointRegistry,
        IPeerPinCache pinCache,
        IMeshRateLimiter rateLimiter)
    {
        this.logger = logger;
        this.options = options.Value;
        this.dispatcher = dispatcher;
        this.endpointRegistry = endpointRegistry;
        this.pinCache = pinCache;
        this.rateLimiter = rateLimiter;
    }

    /// <summary>
    /// Gets the count of active QUIC connections (for metrics).
    /// </summary>
    public int GetActiveConnectionCount() => activeConnections.Count;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enable)
        {
            logger.LogInformation("[Overlay-QUIC] Disabled by configuration");
            return;
        }

        if (!QuicListener.IsSupported)
        {
            logger.LogWarning("[Overlay-QUIC] QUIC is not supported on this platform");
            return;
        }

        try
        {
            // Load or create persistent certificate for QUIC/TLS
            var certificate = Security.PersistentCertificate.LoadOrCreate(
                options.TlsCertPath,
                options.TlsCertPassword,
                "CN=mesh-overlay-control",
                validityYears: 5);

            var listenerOptions = new QuicListenerOptions
            {
                ListenEndPoint = new IPEndPoint(IPAddress.Any, options.ListenPort),
                ApplicationProtocols = new List<SslApplicationProtocol> { new SslApplicationProtocol("slskdn-overlay") },
                ConnectionOptionsCallback = (connection, hello, token) =>
                {
                    return new ValueTask<QuicServerConnectionOptions>(new QuicServerConnectionOptions
                    {
                        DefaultStreamErrorCode = 0x01,
                        DefaultCloseErrorCode = 0x01,
                        ServerAuthenticationOptions = new SslServerAuthenticationOptions
                        {
                            ApplicationProtocols = new List<SslApplicationProtocol> { new SslApplicationProtocol("slskdn-overlay") },
                            ServerCertificate = certificate,
                            ClientCertificateRequired = false,
                            RemoteCertificateValidationCallback = (sender, cert, chain, errors) => true // Accept self-signed certs
                        }
                    });
                }
            };

            await using var listener = await QuicListener.ListenAsync(listenerOptions, stoppingToken);
            logger.LogInformation("[Overlay-QUIC] Listening on port {Port}", options.ListenPort);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var connection = await listener.AcceptConnectionAsync(stoppingToken);
                    _ = HandleConnectionAsync(connection, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[Overlay-QUIC] Error accepting connection");
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Overlay-QUIC] Server failed");
        }
    }

    private async Task HandleConnectionAsync(QuicConnection connection, CancellationToken ct)
    {
        var remoteEndPoint = connection.RemoteEndPoint as IPEndPoint;
        if (remoteEndPoint != null)
        {
            activeConnections.TryAdd(remoteEndPoint, connection);
        }

        try
        {
            await using (connection)
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        var stream = await connection.AcceptInboundStreamAsync(ct);
                        _ = HandleStreamAsync(stream, remoteEndPoint, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "[Overlay-QUIC] Error accepting stream from {Endpoint}", remoteEndPoint);
                        break;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Overlay-QUIC] Connection error from {Endpoint}", remoteEndPoint);
        }
        finally
        {
            if (remoteEndPoint != null)
            {
                activeConnections.TryRemove(remoteEndPoint, out _);
            }
        }
    }

    private async Task HandleStreamAsync(QuicStream stream, IPEndPoint? remoteEndPoint, CancellationToken ct)
    {
        try
        {
            await using (stream)
            {
                // Read ControlEnvelope (MessagePack serialized)
                var buffer = new byte[options.MaxDatagramBytes];
                var totalRead = 0;

                while (totalRead < buffer.Length)
                {
                    var read = await stream.ReadAsync(buffer.AsMemory(totalRead), ct);
                    if (read == 0) break;
                    totalRead += read;
                }

                if (totalRead == 0)
                {
                    return;
                }

                // Pre-auth rate limiting by IP
                if (!rateLimiter.AllowPreAuth(remoteEndPoint.Address))
                {
                    logger.LogWarning("[Overlay-QUIC] Pre-auth rate limit exceeded for {Endpoint}", remoteEndPoint);
                    return;
                }

                // Safe deserialization with size validation
                if (!Security.MeshSizeLimits.TryDeserializeControlEnvelope(buffer.AsMemory(0, totalRead).ToArray(), logger, out var envelope))
                {
                    logger.LogWarning("[Overlay-QUIC] Failed to deserialize envelope from {Endpoint}", remoteEndPoint);
                    return;
                }

                logger.LogDebug("[Overlay-QUIC] Received control {Type} from {Endpoint}", envelope!.Type, remoteEndPoint);

                // Resolve peer context
                var peerId = endpointRegistry.GetPeerId(remoteEndPoint);
                if (peerId == null)
                {
                    logger.LogWarning("[Overlay-QUIC] Cannot resolve PeerId for {Endpoint}, rejecting envelope", remoteEndPoint);
                    return;
                }

                // Post-auth rate limiting by PeerId
                if (!rateLimiter.AllowPostAuth(peerId))
                {
                    logger.LogWarning("[Overlay-QUIC] Post-auth rate limit exceeded for {PeerId}", peerId);
                    return;
                }

                var descriptor = pinCache.GetDescriptor(peerId);
                var allowedKeys = descriptor?.ControlSigningKeys
                    ?.Select(k => Convert.FromBase64String(k.PublicKey))
                    .ToList()
                    ?? new List<byte[]>();

                if (allowedKeys.Count == 0)
                {
                    logger.LogWarning("[Overlay-QUIC] No control signing keys for {PeerId}, rejecting envelope", peerId);
                    return;
                }

                var peerContext = new PeerContext
                {
                    PeerId = peerId,
                    RemoteEndPoint = remoteEndPoint,
                    Transport = "quic",
                    AllowedControlSigningKeys = allowedKeys,
                };

                var handled = await dispatcher.HandleAsync(envelope, peerContext, ct);
                if (!handled)
                {
                    logger.LogWarning("[Overlay-QUIC] Failed to handle envelope {Type} from {PeerId}", envelope.Type, peerId);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Overlay-QUIC] Stream error from {Endpoint}", remoteEndPoint);
        }
    }
}
