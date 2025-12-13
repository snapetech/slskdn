using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.Mesh.Security;

namespace slskd.Mesh.Overlay;

/// <summary>
/// UDP overlay server for control envelopes.
/// </summary>
public class UdpOverlayServer : BackgroundService
{
    private readonly ILogger<UdpOverlayServer> logger;
    private readonly OverlayOptions options;
    private readonly IControlDispatcher dispatcher;
    private readonly IPeerEndpointRegistry endpointRegistry;
    private readonly IPeerPinCache pinCache;
    private UdpClient? udp;

    public UdpOverlayServer(
        ILogger<UdpOverlayServer> logger,
        IOptions<OverlayOptions> options,
        IControlDispatcher dispatcher,
        IPeerEndpointRegistry endpointRegistry,
        IPeerPinCache pinCache)
    {
        this.logger = logger;
        this.options = options.Value;
        this.dispatcher = dispatcher;
        this.endpointRegistry = endpointRegistry;
        this.pinCache = pinCache;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enable)
        {
            logger.LogInformation("[Overlay] UDP overlay disabled");
            return;
        }

        udp = new UdpClient(new IPEndPoint(IPAddress.Any, options.ListenPort))
        {
            Client =
            {
                ReceiveBufferSize = options.ReceiveBufferBytes,
                SendBufferSize = options.SendBufferBytes
            }
        };

        logger.LogInformation("[Overlay] UDP listening on {Port}", options.ListenPort);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await udp.ReceiveAsync(stoppingToken);
                if (result.Buffer.Length > options.MaxDatagramBytes)
                {
                    logger.LogWarning("[Overlay] Dropping oversized datagram size={Size}", result.Buffer.Length);
                    continue;
                }

                ControlEnvelope envelope;
                try
                {
                    envelope = MessagePackSerializer.Deserialize<ControlEnvelope>(result.Buffer);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[Overlay] Failed to decode envelope");
                    continue;
                }

                // Resolve peer context
                var peerId = endpointRegistry.GetPeerId(result.RemoteEndPoint);
                if (peerId == null)
                {
                    logger.LogWarning("[Overlay-UDP] Cannot resolve PeerId for {Endpoint}, rejecting envelope", result.RemoteEndPoint);
                    continue;
                }

                var descriptor = pinCache.GetDescriptor(peerId);
                var allowedKeys = descriptor?.ControlSigningPublicKeys
                    ?.Select(k => Convert.FromBase64String(k))
                    .ToList()
                    ?? new List<byte[]>();

                if (allowedKeys.Count == 0)
                {
                    logger.LogWarning("[Overlay-UDP] No control signing keys for {PeerId}, rejecting envelope", peerId);
                    continue;
                }

                var peerContext = new PeerContext
                {
                    PeerId = peerId,
                    RemoteEndPoint = result.RemoteEndPoint,
                    Transport = "udp",
                    AllowedControlSigningKeys = allowedKeys,
                };

                await dispatcher.HandleAsync(envelope, peerContext, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // shutdown
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[Overlay] Receive loop error");
            }
        }
    }

    public override void Dispose()
    {
        udp?.Dispose();
        base.Dispose();
    }
}
