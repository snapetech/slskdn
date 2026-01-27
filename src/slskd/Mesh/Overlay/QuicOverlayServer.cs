// <copyright file="QuicOverlayServer.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

#pragma warning disable CA2252 // Preview features - QUIC APIs require preview features

namespace slskd.Mesh.Overlay;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.Mesh;
using slskd.Mesh.Transport;

/// <summary>
/// QUIC overlay server for control-plane messages (ControlEnvelope).
/// </summary>
public class QuicOverlayServer : BackgroundService
{
    private readonly ILogger<QuicOverlayServer> logger;
    private readonly OverlayOptions options;
    private readonly IControlDispatcher dispatcher;
    private readonly ConnectionThrottler connectionThrottler;
    private readonly int maxRemotePayload;
    private readonly ConcurrentDictionary<IPEndPoint, QuicConnection> activeConnections = new();

    public QuicOverlayServer(
        ILogger<QuicOverlayServer> logger,
        IOptions<OverlayOptions> options,
        IControlDispatcher dispatcher,
        ConnectionThrottler connectionThrottler,
        IOptions<Mesh.MeshOptions>? meshOptions = null)
    {
        logger.LogInformation("[QuicOverlayServer] Constructor called");
        this.logger = logger;
        this.options = options.Value;
        this.dispatcher = dispatcher;
        this.connectionThrottler = connectionThrottler ?? throw new ArgumentNullException(nameof(connectionThrottler));
        maxRemotePayload = meshOptions?.Value?.Security?.GetEffectiveMaxPayloadSize() ?? SecurityUtils.MaxRemotePayloadSize;
        logger.LogInformation("[QuicOverlayServer] Constructor completed");
    }

    /// <summary>
    /// Gets the count of active QUIC connections (for metrics).
    /// </summary>
    public int GetActiveConnectionCount() => activeConnections.Count;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Critical: never block host startup (BackgroundService.StartAsync runs until first await)
        // This yields immediately so Kestrel can start binding while QUIC initializes
        await Task.Yield();

        logger.LogInformation("[QuicOverlayServer] ExecuteAsync called");
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
            // Generate self-signed certificate for QUIC/TLS
            var certificate = SelfSignedCertificate.Create("CN=mesh-overlay-quic");

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

            QuicListener? listener = null;
            try
            {
                listener = await QuicListener.ListenAsync(listenerOptions, stoppingToken);
                logger.LogInformation("[Overlay-QUIC] Listening on port {Port}", options.ListenPort);
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                logger.LogWarning(
                    "[Overlay-QUIC] QUIC overlay port {Port} is already in use. Continuing without QUIC overlay server. " +
                    "Mesh will operate in degraded mode: DHT, relay, and hole punching will still function, " +
                    "but direct inbound QUIC connections will be unavailable.",
                    options.ListenPort);
                return; // Gracefully exit - mesh can still function via other transports
            }
            catch (SocketException ex)
            {
                logger.LogWarning(
                    ex,
                    "[Overlay-QUIC] QUIC overlay failed to bind to port {Port} (error: {Error}). Continuing without QUIC overlay server. " +
                    "Mesh will operate in degraded mode: DHT, relay, and hole punching will still function, " +
                    "but direct inbound QUIC connections will be unavailable.",
                    options.ListenPort, ex.SocketErrorCode);
                return; // Gracefully exit - mesh can still function via other transports
            }

            if (listener == null)
            {
                return; // Should not happen, but safety check
            }

            await using (listener)

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var connection = await listener.AcceptConnectionAsync(stoppingToken);
                    var ep = connection.RemoteEndPoint as IPEndPoint;
                    if (ep != null && !connectionThrottler.ShouldAllowConnection(ep.ToString(), TransportType.DirectQuic))
                    {
                        try { await connection.CloseAsync(0); } catch { /* ignore */ }
                        await connection.DisposeAsync();
                        continue;
                    }
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
                        if (!connectionThrottler.ShouldAllowInboundStream(remoteEndPoint?.ToString() ?? "unknown"))
                        {
                            await stream.DisposeAsync();
                            continue;
                        }
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
                    if (totalRead > maxRemotePayload)
                    {
                        logger.LogWarning("[Overlay-QUIC] Payload exceeds {Max} bytes, aborting stream", maxRemotePayload);
                        return;
                    }
                }

                if (totalRead == 0)
                {
                    return;
                }

                var envelope = PayloadParser.ParseMessagePackSafely<ControlEnvelope>(buffer.AsSpan(0, totalRead).ToArray(), maxRemotePayload);
                if (envelope == null)
                {
                    return;
                }
                logger.LogDebug("[Overlay-QUIC] Received control {Type} from {Endpoint}", envelope.Type, remoteEndPoint);

                var handled = await dispatcher.HandleAsync(envelope, ct);
                if (!handled)
                {
                    logger.LogWarning("[Overlay-QUIC] Failed to handle envelope {Type} from {Endpoint}", envelope.Type, remoteEndPoint);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Overlay-QUIC] Stream error from {Endpoint}", remoteEndPoint);
        }
    }
}
