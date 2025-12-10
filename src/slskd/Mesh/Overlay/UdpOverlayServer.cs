using System.Net;
using System.Net.Sockets;
using MessagePack;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace slskd.Mesh.Overlay;

/// <summary>
/// UDP overlay server for control envelopes.
/// </summary>
public class UdpOverlayServer : BackgroundService
{
    private readonly ILogger<UdpOverlayServer> logger;
    private readonly OverlayOptions options;
    private readonly IControlDispatcher dispatcher;
    private UdpClient? udp;

    public UdpOverlayServer(
        ILogger<UdpOverlayServer> logger,
        IOptions<OverlayOptions> options,
        IControlDispatcher dispatcher)
    {
        this.logger = logger;
        this.options = options.Value;
        this.dispatcher = dispatcher;
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

                await dispatcher.HandleAsync(envelope, stoppingToken);
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
