// <copyright file="QuicDataServer.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
#pragma warning disable CA2252 // Preview features - QUIC APIs require preview features
#pragma warning disable CA1416 // Runtime IsSupported guards already gate QUIC-only code paths

namespace slskd.Mesh.Overlay;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
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
    private readonly ConcurrentDictionary<IPEndPoint, string> _pinnedRemoteCertificates = new();
    private readonly ConcurrentDictionary<int, Task> activeConnectionTasks = new();
    private int nextConnectionTaskId;

    public QuicDataServer(
        ILogger<QuicDataServer> logger,
        IOptions<DataOverlayOptions> options,
        ConnectionThrottler connectionThrottler,
        IOptions<MeshOptions>? meshOptions = null)
    {
        logger.LogDebug("[QuicDataServer] Constructor called");
        this.logger = logger;
        this.options = options.Value;
        this.connectionThrottler = connectionThrottler ?? throw new ArgumentNullException(nameof(connectionThrottler));
        var cap = this.options.MaxPayloadBytes;
        if (meshOptions?.Value?.Security != null)
            cap = Math.Min(cap, meshOptions.Value.Security.GetEffectiveMaxPayloadSize());
        maxPayloadBytes = Math.Max(1, cap);
        logger.LogDebug("[QuicDataServer] Constructor completed");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await CloseActiveConnectionsAsync().ConfigureAwait(false);
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        await DrainConnectionTasksAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Critical: never block host startup (BackgroundService.StartAsync runs until first await)
        // This yields immediately so Kestrel can start binding while QUIC initializes
        await Task.Yield();

        logger.LogDebug("[QuicDataServer] ExecuteAsync called");
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
            using var certificate = SelfSignedCertificate.Create("CN=mesh-overlay-quic-data");

            var listenerOptions = new QuicListenerOptions
            {
                ListenEndPoint = new IPEndPoint(IPAddress.Any, options.ListenPort),
                ApplicationProtocols = new List<SslApplicationProtocol> { new SslApplicationProtocol("slskdn-overlay-data") },
                ConnectionOptionsCallback = (connection, hello, token) =>
                {
                    var endpoint = connection.RemoteEndPoint as IPEndPoint;
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
                            RemoteCertificateValidationCallback = (_, certificate, chain, errors) =>
                                ValidatePinnedCertificate(endpoint, certificate, chain, errors)
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
                        try
                        {
                            await connection.CloseAsync(0, stoppingToken);
                        }
                        catch
                        {
                            // Ignore close failures for rejected peers.
                        }

                        await connection.DisposeAsync();
                        continue;
                    }

                    TrackConnectionTask(HandleConnectionAsync(connection, stoppingToken));
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
                    if (r == 0)
                    {
                        break;
                    }

                    if (lineBuf[n] == (byte)'\n')
                    {
                        n++;
                        break;
                    }

                    n += r;
                }

                var line = n > 0 ? System.Text.Encoding.ASCII.GetString(lineBuf.AsSpan(0, n)).TrimEnd() : string.Empty;

                if (line.StartsWith("RELAY_TCP ", StringComparison.Ordinal))
                {
                    var parts = line.Split(' ');
                    if (parts.Length >= 3 && int.TryParse(parts[2], out var port) && port is > 0 and <= ushort.MaxValue)
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
                            try
                            {
                                await stream.WriteAsync(System.Text.Encoding.ASCII.GetBytes("ERR " + ex.Message + "\n"), ct);
                            }
                            catch (Exception responseEx)
                            {
                                logger.LogDebug(responseEx, "[Overlay-QUIC-DATA] Failed to send relay error response to {Endpoint}", remoteEndPoint);
                            }
                        }
                    }
                    else
                    {
                        try
                        {
                            await stream.WriteAsync(System.Text.Encoding.ASCII.GetBytes("ERR bad RELAY_TCP format\n"), ct);
                        }
                        catch (Exception ex)
                        {
                            logger.LogDebug(ex, "[Overlay-QUIC-DATA] Failed to send bad-format response to {Endpoint}", remoteEndPoint);
                        }
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

    private bool ValidatePinnedCertificate(
        IPEndPoint? endpoint,
        X509Certificate? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors)
    {
        if (endpoint is null)
        {
            logger.LogDebug("[Overlay-QUIC-DATA] Rejecting TLS handshake: remote endpoint unavailable");
            return false;
        }

        if (certificate == null)
        {
            logger.LogDebug(
                "[Overlay-QUIC-DATA] Rejecting TLS handshake from {Endpoint}: no certificate provided",
                endpoint);
            return false;
        }

        if (!IsAllowedInsecurePinnedCertificate(certificate, chain, sslPolicyErrors))
        {
            logger.LogDebug(
                "[Overlay-QUIC-DATA] Rejecting TLS handshake from {Endpoint}: TLS policy errors {Errors} are disallowed",
                endpoint,
                sslPolicyErrors);
            return false;
        }

        using var cert2 = certificate as X509Certificate2 ?? new X509Certificate2(certificate);
        var presentedPin = Mesh.Transport.SecurityUtils.ExtractSpkiPin(cert2);
        if (string.IsNullOrWhiteSpace(presentedPin))
        {
            logger.LogWarning(
                "[Overlay-QUIC-DATA] Rejecting TLS handshake from {Endpoint}: failed to extract SPKI pin",
                endpoint);
            return false;
        }

        if (_pinnedRemoteCertificates.TryGetValue(endpoint, out var expectedPin) && expectedPin == presentedPin)
        {
            logger.LogDebug(
                "[Overlay-QUIC-DATA] Accepted pinned certificate for {Endpoint}",
                endpoint);

            return true;
        }

        if (_pinnedRemoteCertificates.TryGetValue(endpoint, out var priorPin))
        {
            logger.LogWarning(
                "[Overlay-QUIC-DATA] Rotating pinned certificate for {Endpoint} due mismatch (old={OldPin}, new={NewPin})",
                endpoint,
                priorPin,
                presentedPin);
        }
        else
        {
            logger.LogInformation(
                "[Overlay-QUIC-DATA] First contact with {Endpoint}; pinning certificate {Pin}",
                endpoint,
                presentedPin);
        }

        _pinnedRemoteCertificates[endpoint] = presentedPin;
        return true;
    }

    private static bool IsAllowedInsecurePinnedCertificate(X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
    {
        if (certificate == null || sslPolicyErrors == SslPolicyErrors.None)
        {
            return sslPolicyErrors == SslPolicyErrors.None;
        }

        if (sslPolicyErrors != SslPolicyErrors.RemoteCertificateChainErrors)
        {
            return false;
        }

        var certificate2 = certificate as X509Certificate2;
        if (certificate2 == null || !string.Equals(certificate2.Subject, certificate2.Issuer, StringComparison.Ordinal))
        {
            return false;
        }

        if (chain == null || chain.ChainStatus.Length == 0)
        {
            return false;
        }

        foreach (var status in chain.ChainStatus)
        {
            if (status.Status != X509ChainStatusFlags.UntrustedRoot &&
                status.Status != X509ChainStatusFlags.PartialChain)
            {
                return false;
            }
        }

        return true;
    }

    private static async Task CopyToAsync(Stream source, Stream target, CancellationToken ct)
    {
        var buf = new byte[8192];
        int r;
        while ((r = await source.ReadAsync(buf, ct)) > 0)
            await target.WriteAsync(buf.AsMemory(0, r), ct);
    }

    private void TrackConnectionTask(Task task)
    {
        var taskId = Interlocked.Increment(ref nextConnectionTaskId);
        activeConnectionTasks.TryAdd(taskId, task);
        _ = task.ContinueWith(
            _ =>
            {
                if (activeConnectionTasks.TryRemove(taskId, out var removedTask))
                {
                    _ = removedTask;
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task CloseActiveConnectionsAsync()
    {
        var connections = activeConnections.Values.Distinct().ToArray();
        foreach (var connection in connections)
        {
            try
            {
                await connection.CloseAsync(0, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "[Overlay-QUIC-DATA] Failed to close active connection during stop");
            }
        }
    }

    private async Task DrainConnectionTasksAsync(CancellationToken cancellationToken)
    {
        var tasks = activeConnectionTasks.Values.ToArray();
        if (tasks.Length == 0)
        {
            return;
        }

        try
        {
            await Task.WhenAll(tasks).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Host shutdown timeout should not surface as a second failure here.
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "[Overlay-QUIC-DATA] Error draining active connection tasks during stop");
        }
    }
}
