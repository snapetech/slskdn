// <copyright file="QuicDataServer.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

#pragma warning disable CA2252 // Preview features - QUIC APIs require preview features

namespace slskd.Mesh.Overlay;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// QUIC data-plane server for bulk payload transfers.
/// </summary>
public class QuicDataServer : BackgroundService
{
    private readonly ILogger<QuicDataServer> logger;
    private readonly DataOverlayOptions options;
    private readonly ConcurrentDictionary<IPEndPoint, QuicConnection> activeConnections = new();

    public QuicDataServer(
        ILogger<QuicDataServer> logger,
        IOptions<DataOverlayOptions> options)
    {
        logger.LogInformation("[QuicDataServer] Constructor called");
        this.logger = logger;
        this.options = options.Value;
        logger.LogInformation("[QuicDataServer] Constructor completed");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("[QuicDataServer] ExecuteAsync called");
        if (!options.Enable)
        {
            logger.LogInformation("[Overlay-QUIC-DATA] Disabled by configuration");
            return;
        }

        if (!QuicListener.IsSupported)
        {
            logger.LogWarning("[Overlay-QUIC-DATA] QUIC is not supported on this platform");
            return;
        }

        try
        {
            // Load or create persistent certificate for QUIC/TLS data plane
            var certificate = Security.PersistentCertificate.LoadOrCreate(
                options.TlsCertPath,
                options.TlsCertPassword,
                "CN=mesh-overlay-data",
                validityYears: 5);

            var listenerOptions = new QuicListenerOptions
            {
                ListenEndPoint = new IPEndPoint(IPAddress.Any, options.ListenPort),
                ApplicationProtocols = new List<SslApplicationProtocol> { new SslApplicationProtocol("slskdn-overlay-data") },
                ConnectionOptionsCallback = (connection, hello, token) =>
                {
                    return new ValueTask<QuicServerConnectionOptions>(new QuicServerConnectionOptions
                    {
                        DefaultStreamErrorCode = 0x02,
                        DefaultCloseErrorCode = 0x02,
                        MaxInboundBidirectionalStreams = options.MaxConcurrentStreams,
                        MaxInboundUnidirectionalStreams = options.MaxConcurrentStreams,
                        ServerAuthenticationOptions = new SslServerAuthenticationOptions
                        {
                            ApplicationProtocols = new List<SslApplicationProtocol> { new SslApplicationProtocol("slskdn-overlay-data") },
                            ServerCertificate = certificate,
                            ClientCertificateRequired = false,
                            RemoteCertificateValidationCallback = (sender, cert, chain, errors) => true // Accept self-signed certs
                        }
                    });
                }
            };

            await using var listener = await QuicListener.ListenAsync(listenerOptions, stoppingToken);
            logger.LogInformation("[Overlay-QUIC-DATA] Listening on port {Port}", options.ListenPort);

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
                    logger.LogError(ex, "[Overlay-QUIC-DATA] Error accepting connection");
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Overlay-QUIC-DATA] Server failed");
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
                        logger.LogWarning(ex, "[Overlay-QUIC-DATA] Error accepting stream from {Endpoint}", remoteEndPoint);
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
            logger.LogWarning(ex, "[Overlay-QUIC-DATA] Connection error from {Endpoint}", remoteEndPoint);
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
                // Read payload data
                var buffer = new byte[options.MaxPayloadBytes];
                var totalRead = 0;

                while (totalRead < buffer.Length)
                {
                    var read = await stream.ReadAsync(buffer.AsMemory(totalRead), ct);
                    if (read == 0) break;
                    totalRead += read;
                }

                if (totalRead > 0)
                {
                    logger.LogDebug("[Overlay-QUIC-DATA] Received {Size} bytes from {Endpoint}", totalRead, remoteEndPoint);
                    // TODO: Process payload (e.g., deliver to mesh message handler)
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Overlay-QUIC-DATA] Stream error from {Endpoint}", remoteEndPoint);
        }
    }
}
