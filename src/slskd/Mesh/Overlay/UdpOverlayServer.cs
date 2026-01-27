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
        // Critical: never block host startup (BackgroundService.StartAsync runs until first await)
        // This yields immediately so Kestrel can start binding while UDP initializes
        await Task.Yield();

        logger.LogInformation("[UdpOverlayServer] ExecuteAsync called");
        try
        {
            if (!options.Enable)
            {
                logger.LogInformation("[Overlay] UDP overlay disabled");
                return;
            }

            try
            {
                udp = new UdpClient(new IPEndPoint(IPAddress.Any, options.ListenPort))
                {
                    Client =
                    {
                        ReceiveBufferSize = options.ReceiveBufferBytes,
                        SendBufferSize = options.SendBufferBytes
                    }
                };

                logger.LogInformation("[Overlay] UDP listening on {Port}", options.ListenPort);
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                logger.LogWarning(
                    "[Overlay] UDP overlay port {Port} is already in use. Continuing without UDP overlay server. " +
                    "Mesh will operate in degraded mode: DHT, relay, and hole punching will still function, " +
                    "but direct inbound UDP connections will be unavailable.",
                    options.ListenPort);
                return; // Gracefully exit - mesh can still function via other transports
            }
            catch (SocketException ex)
            {
                logger.LogWarning(
                    ex,
                    "[Overlay] UDP overlay failed to bind to port {Port} (error: {Error}). Continuing without UDP overlay server. " +
                    "Mesh will operate in degraded mode: DHT, relay, and hole punching will still function, " +
                    "but direct inbound UDP connections will be unavailable.",
                    options.ListenPort, ex.SocketErrorCode);
                return; // Gracefully exit - mesh can still function via other transports
            }

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
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Overlay] UDP overlay server error (non-fatal): {Message}", ex.Message);
            // Don't re-throw - allow mesh to continue operating via other transports
        }
    }

    public override void Dispose()
    {
        udp?.Dispose();
        base.Dispose();
    }
}
