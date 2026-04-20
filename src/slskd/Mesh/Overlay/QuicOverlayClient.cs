// <copyright file="QuicOverlayClient.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

#pragma warning disable CA2252 // Preview features - QUIC APIs require preview features
#pragma warning disable CA1416 // Runtime IsSupported guards already gate QUIC-only code paths

namespace slskd.Mesh.Overlay;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.Mesh.Privacy;

/// <summary>
/// QUIC overlay client for control-plane messages (ControlEnvelope).
/// </summary>
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
[SupportedOSPlatform("windows")]
public class QuicOverlayClient : IOverlayClient, IAsyncDisposable
{
    private readonly ILogger<QuicOverlayClient> logger;
    private readonly OverlayOptions options;
    private readonly IPrivacyLayer? privacyLayer;
    private readonly ConcurrentDictionary<IPEndPoint, QuicConnection> connections = new();
    private readonly ConcurrentDictionary<IPEndPoint, SemaphoreSlim> connectionLocks = new();
    private int disposed;

    public QuicOverlayClient(
        ILogger<QuicOverlayClient> logger,
        IOptions<OverlayOptions> options,
        IControlSigner signer,
        IPrivacyLayer? privacyLayer = null)
    {
        this.logger = logger;
        this.options = options.Value;
        this.privacyLayer = privacyLayer;
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
            // Apply outbound privacy transforms if enabled
            if (privacyLayer != null && privacyLayer.IsEnabled && envelope.Payload != null && envelope.Payload.Length > 0)
            {
                envelope.Payload = await privacyLayer.ProcessOutboundMessageAsync(envelope.Payload, ct);
                if (envelope.Payload.Length == 0)
                {
                    // Message was queued for batching, not ready to send yet
                    logger.LogTrace("[Overlay-QUIC] Message queued for batching, not sending immediately");
                    return true; // Not an error, just delayed
                }

                logger.LogTrace("[Overlay-QUIC] Applied outbound privacy transforms to envelope {MessageId}", envelope.MessageId);
            }

            // Check for pending batches to send
            if (privacyLayer != null)
            {
                var batches = privacyLayer.GetPendingBatches();
                if (batches != null && batches.Count > 0)
                {
                    // Send batched messages first
                    foreach (var batchPayload in batches)
                    {
                        if (!await SendPayloadAsync(batchPayload, endpoint, ct))
                        {
                            return false;
                        }
                    }

                    logger.LogDebug("[Overlay-QUIC] Sent {Count} batched messages to {Endpoint}", batches.Count, endpoint);
                }
            }

            // Serialize envelope
            var payload = MessagePackSerializer.Serialize(envelope, cancellationToken: CancellationToken.None);
            if (payload.Length > options.MaxDatagramBytes)
            {
                logger.LogWarning("[Overlay-QUIC] Refusing to send oversized envelope size={Size}", payload.Length);
                return false;
            }

            // Send the main payload
            var success = await SendPayloadAsync(payload, endpoint, ct);

            if (success && privacyLayer != null)
            {
                // Apply timing delay if enabled
                var delay = privacyLayer.GetOutboundDelay();
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, ct);
                    logger.LogTrace("[Overlay-QUIC] Applied timing delay {Delay}ms", delay.TotalMilliseconds);
                }

                // Record the outbound message for privacy layer tracking
                privacyLayer.RecordOutboundMessage();
            }

            return success;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Overlay-QUIC] Failed to send to {Endpoint}", endpoint);
            connections.TryRemove(endpoint, out _);
            return false;
        }
    }

    private async Task<bool> SendPayloadAsync(byte[] payload, IPEndPoint endpoint, CancellationToken ct)
    {
        var connection = await GetOrCreateConnectionAsync(endpoint, ct).ConfigureAwait(false);
        if (connection == null)
        {
            return false;
        }

        try
        {
            await using var stream = await connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, ct).ConfigureAwait(false);
            await stream.WriteAsync(payload, ct).ConfigureAwait(false);
            return true;
        }
        catch
        {
            await RemoveConnectionAsync(endpoint, connection).ConfigureAwait(false);
            throw;
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
                    RemoteCertificateValidationCallback = (sender, certificate, chain, errors) => true // Accept self-signed certs
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

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        var endpoints = connections.Keys.ToArray();
        foreach (var endpoint in endpoints)
        {
            await RemoveConnectionAsync(endpoint).ConfigureAwait(false);
        }

        foreach (var gate in connectionLocks.Values)
        {
            gate.Dispose();
        }

        connectionLocks.Clear();
    }

    private async Task<QuicConnection?> GetOrCreateConnectionAsync(IPEndPoint endpoint, CancellationToken ct)
    {
        if (Volatile.Read(ref disposed) != 0)
        {
            return null;
        }

        var gate = connectionLocks.GetOrAdd(endpoint, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            if (Volatile.Read(ref disposed) != 0)
            {
                return null;
            }

            if (connections.TryGetValue(endpoint, out var existing) && existing != null)
            {
                return existing;
            }

            var created = await CreateConnectionAsync(endpoint, ct).ConfigureAwait(false);
            if (created == null)
            {
                return null;
            }

            if (connections.TryAdd(endpoint, created))
            {
                return created;
            }

            await SafeDisposeConnectionAsync(created, endpoint, "duplicate cached connection").ConfigureAwait(false);
            return connections.TryGetValue(endpoint, out existing) ? existing : null;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task RemoveConnectionAsync(IPEndPoint endpoint, QuicConnection? expectedConnection = null)
    {
        var gate = connectionLocks.GetOrAdd(endpoint, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);

        try
        {
            if (connections.TryGetValue(endpoint, out var cached) &&
                (expectedConnection == null || ReferenceEquals(cached, expectedConnection)) &&
                connections.TryRemove(endpoint, out var removed))
            {
                await SafeDisposeConnectionAsync(removed, endpoint, "cached connection removed").ConfigureAwait(false);
            }
            else if (expectedConnection != null)
            {
                await SafeDisposeConnectionAsync(expectedConnection, endpoint, "orphaned connection removed").ConfigureAwait(false);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task SafeDisposeConnectionAsync(QuicConnection connection, IPEndPoint endpoint, string reason)
    {
        try
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "[Overlay-QUIC] Failed to dispose connection to {Endpoint} ({Reason})", endpoint, reason);
        }
    }
}
