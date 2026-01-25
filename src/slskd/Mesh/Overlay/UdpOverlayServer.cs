// <copyright file="UdpOverlayServer.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using slskd.Mesh.Transport;
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
    private readonly ConnectionThrottler connectionThrottler;
    private readonly int maxRemotePayload;
    private UdpClient? udp;

    public UdpOverlayServer(
        ILogger<UdpOverlayServer> logger,
        IOptions<OverlayOptions> options,
        IControlDispatcher dispatcher,
        ConnectionThrottler connectionThrottler,
        IOptions<Mesh.MeshOptions>? meshOptions = null)
    {
        logger.LogInformation("[UdpOverlayServer] Constructor called");
        this.logger = logger;
        this.options = options.Value;
        this.dispatcher = dispatcher;
        this.connectionThrottler = connectionThrottler ?? throw new ArgumentNullException(nameof(connectionThrottler));
        maxRemotePayload = meshOptions?.Value?.Security?.GetEffectiveMaxPayloadSize() ?? SecurityUtils.MaxRemotePayloadSize;
        logger.LogInformation("[UdpOverlayServer] Constructor completed");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("[UdpOverlayServer] ExecuteAsync called");
        try
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
                    if (!connectionThrottler.ShouldAllowInboundDatagram(result.RemoteEndPoint?.ToString() ?? "unknown"))
                        continue;
                    if (result.Buffer.Length > maxRemotePayload)
                    {
                        logger.LogWarning("[Overlay] Dropping datagram exceeding MaxRemotePayloadSize ({Size} bytes)", result.Buffer.Length);
                        continue;
                    }
                    if (result.Buffer.Length > options.MaxDatagramBytes)
                    {
                        logger.LogWarning("[Overlay] Dropping oversized datagram size={Size}", result.Buffer.Length);
                        continue;
                    }

                    ControlEnvelope? envelope;
                    try
                    {
                        envelope = PayloadParser.ParseMessagePackSafely<ControlEnvelope>(result.Buffer, maxRemotePayload);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "[Overlay] Failed to decode envelope");
                        continue;
                    }

                    if (envelope == null)
                    {
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
        catch (Exception ex)
        {
            logger.LogError(ex, "[Overlay] FATAL: UDP overlay server failed to start - {Message}", ex.Message);
            throw; // Re-throw to stop host if UDP is critical
        }
    }

    public override void Dispose()
    {
        udp?.Dispose();
        base.Dispose();
    }
}
