// <copyright file="QuicDataServer.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

#pragma warning disable CA2252 // Preview features - QUIC APIs require preview features

namespace slskd.Mesh.Overlay;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.Mesh;
using slskd.Mesh.Transport;

/// <summary>
/// QUIC data-plane server for bulk payload transfers.
/// </summary>
public class QuicDataServer : BackgroundService
{
    private readonly ILogger<QuicDataServer> logger;
    private readonly DataOverlayOptions options;
    private readonly ConnectionThrottler connectionThrottler;
    private readonly int maxPayloadBytes;
    private readonly ConcurrentDictionary<IPEndPoint, QuicConnection> activeConnections = new();

    public QuicDataServer(
        ILogger<QuicDataServer> logger,
        IOptions<DataOverlayOptions> options,
        ConnectionThrottler connectionThrottler,
        IOptions<MeshOptions>? meshOptions = null)
    {
        logger.LogInformation("[QuicDataServer] Constructor called");
        this.logger = logger;
        this.options = options.Value;
        this.connectionThrottler = connectionThrottler ?? throw new ArgumentNullException(nameof(connectionThrottler));
        var cap = this.options.MaxPayloadBytes;
        if (meshOptions?.Value?.Security != null)
            cap = Math.Min(cap, meshOptions.Value.Security.GetEffectiveMaxPayloadSize());
        maxPayloadBytes = Math.Max(1, cap);
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
            // Generate self-signed certificate for QUIC/TLS
            var certificate = SelfSignedCertificate.Create("CN=mesh-overlay-quic-data");

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
                // Read first line (up to 256 bytes) to detect RELAY_TCP
                var lineBuf = new byte[256];
                var n = 0;
                while (n < lineBuf.Length)
                {
                    var r = await stream.ReadAsync(lineBuf.AsMemory(n, 1), ct);
                    if (r == 0) break;
                    if (lineBuf[n] == (byte)'\n') { n++; break; }
                    n += r;
                }
                var line = n > 0 ? System.Text.Encoding.ASCII.GetString(lineBuf.AsSpan(0, n)).TrimEnd() : "";

                if (line.StartsWith("RELAY_TCP ", StringComparison.Ordinal))
                {
                    var parts = line.Split(' ');
                    if (parts.Length >= 3 && int.TryParse(parts[2], out var port))
                    {
                        var host = parts[1];
                        try
                        {
                            using var tcp = new System.Net.Sockets.TcpClient();
                            await tcp.ConnectAsync(host, port, ct);
                            var tcpStream = tcp.GetStream();
                            await stream.WriteAsync(System.Text.Encoding.ASCII.GetBytes("OK\n"), ct);

                            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                            var toTcp = CopyToAsync(stream, tcpStream, cts.Token);
                            var toStream = CopyToAsync(tcpStream, stream, cts.Token);
                            await Task.WhenAny(toTcp, toStream);
                            cts.Cancel();
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "[Overlay-QUIC-DATA] RELAY_TCP to {Host}:{Port} failed", host, port);
                            try { await stream.WriteAsync(System.Text.Encoding.ASCII.GetBytes("ERR " + ex.Message + "\n"), ct); } catch { /* best effort */ }
                        }
                    }
                    else
                    {
                        try { await stream.WriteAsync(System.Text.Encoding.ASCII.GetBytes("ERR bad RELAY_TCP format\n"), ct); } catch { }
                    }
                    return;
                }

                // Non-relay: read payload (existing behavior)
                var buffer = new byte[maxPayloadBytes];
                var totalRead = n;
                if (n > 0)
                    Array.Copy(lineBuf, 0, buffer, 0, n);
                while (totalRead < buffer.Length)
                {
                    var read = await stream.ReadAsync(buffer.AsMemory(totalRead), ct);
                    if (read == 0) break;
                    totalRead += read;
                }
                if (totalRead > 0)
                {
                    logger.LogDebug("[Overlay-QUIC-DATA] Received {Size} bytes from {Endpoint}", totalRead, remoteEndPoint);
                    // Payload delivery: deferred until IOverlayDataPayloadHandler (or similar) is designed and wired.
                    // See memory-bank/triage-todo-fixme.md. For now we log and retain buffer for future use.
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Overlay-QUIC-DATA] Stream error from {Endpoint}", remoteEndPoint);
        }
    }

    private static async Task CopyToAsync(Stream source, Stream target, CancellationToken ct)
    {
        var buf = new byte[8192];
        int r;
        while ((r = await source.ReadAsync(buf, ct)) > 0)
            await target.WriteAsync(buf.AsMemory(0, r), ct);
    }
}
