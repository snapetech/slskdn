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
using System.Security.Cryptography.X509Certificates;
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
    private readonly ConcurrentDictionary<IPEndPoint, string> _pinnedRemoteCertificates = new();
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
                    RemoteCertificateValidationCallback = (_, certificate, chain, errors) =>
                        ValidatePinnedCertificate(endpoint, certificate, chain, errors)
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

    private bool ValidatePinnedCertificate(
        IPEndPoint endpoint,
        X509Certificate? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors)
    {
        if (certificate == null)
        {
            logger.LogDebug(
                "[Overlay-QUIC] Rejecting TLS handshake for {Endpoint}: no certificate provided",
                endpoint);
            return false;
        }

        if (!IsAllowedInsecurePinnedCertificate(certificate, chain, sslPolicyErrors))
        {
            logger.LogDebug(
                "[Overlay-QUIC] Rejecting TLS handshake from {Endpoint}: TLS policy errors {Errors} are disallowed",
                endpoint,
                sslPolicyErrors);
            return false;
        }

        using var cert2 = certificate as X509Certificate2 ?? new X509Certificate2(certificate);
        var presentedPin = Mesh.Transport.SecurityUtils.ExtractSpkiPin(cert2);
        if (string.IsNullOrWhiteSpace(presentedPin))
        {
            logger.LogWarning(
                "[Overlay-QUIC] Rejecting TLS handshake for {Endpoint}: failed to extract SPKI pin",
                endpoint);
            return false;
        }

        if (_pinnedRemoteCertificates.TryGetValue(endpoint, out var expectedPin) && expectedPin == presentedPin)
        {
            logger.LogDebug(
                "[Overlay-QUIC] Accepted pinned certificate for {Endpoint}",
                endpoint);

            return true;
        }

        if (_pinnedRemoteCertificates.TryGetValue(endpoint, out var priorPin))
        {
            logger.LogWarning(
                "[Overlay-QUIC] Rotating pinned certificate for {Endpoint} due mismatch (old={OldPin}, new={NewPin})",
                endpoint,
                priorPin,
                presentedPin);
        }
        else
        {
            logger.LogInformation(
                "[Overlay-QUIC] First contact with {Endpoint}; pinning certificate {Pin}",
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

            QuicConnection? existing = null;
            if (connections.TryGetValue(endpoint, out existing) && existing != null)
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
        QuicConnection? removed = expectedConnection;

        try
        {
            QuicConnection? cached = null;
            if (connections.TryGetValue(endpoint, out cached) &&
                (expectedConnection == null || ReferenceEquals(cached, expectedConnection)) &&
                connections.TryRemove(endpoint, out _))
            {
                await SafeDisposeConnectionAsync(cached, endpoint, "cached connection removed").ConfigureAwait(false);
                removed = null;
            }

            if (removed is not null)
            {
                await SafeDisposeConnectionAsync(removed, endpoint, "orphaned connection removed").ConfigureAwait(false);
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
