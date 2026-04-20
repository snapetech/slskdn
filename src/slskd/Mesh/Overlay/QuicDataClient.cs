// <copyright file="QuicDataClient.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

#pragma warning disable CA2252 // Preview features - QUIC APIs require preview features
#pragma warning disable CA1416 // Runtime IsSupported guards already gate QUIC-only code paths

namespace slskd.Mesh.Overlay;

using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// QUIC data-plane client for bulk payload transfers.
/// </summary>
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
[SupportedOSPlatform("windows")]
public class QuicDataClient : IOverlayDataPlane, IAsyncDisposable
{
    private readonly ILogger<QuicDataClient> logger;
    private readonly DataOverlayOptions options;
    private readonly ConcurrentDictionary<IPEndPoint, QuicConnection> connections = new();
    private readonly ConcurrentDictionary<IPEndPoint, SemaphoreSlim> connectionLocks = new();
    private readonly ConcurrentDictionary<IPEndPoint, string> _pinnedRemoteCertificates = new();
    private int disposed;

    public QuicDataClient(ILogger<QuicDataClient> logger, IOptions<DataOverlayOptions> options)
    {
        this.logger = logger;
        this.options = options.Value;
    }

    public async Task<bool> SendAsync(byte[] payload, IPEndPoint endpoint, CancellationToken ct = default)
    {
        if (!options.Enable)
        {
            return false;
        }

        if (!QuicConnection.IsSupported)
        {
            logger.LogDebug("[Overlay-QUIC-DATA] QUIC not supported, skipping send to {Endpoint}", endpoint);
            return false;
        }

        if (payload.Length > options.MaxPayloadBytes)
        {
            logger.LogWarning("[Overlay-QUIC-DATA] Payload too large: {Size} bytes (max {Max})", payload.Length, options.MaxPayloadBytes);
            return false;
        }

        try
        {
            var connection = await GetOrCreateConnectionAsync(endpoint, ct).ConfigureAwait(false);
            if (connection == null)
            {
                return false;
            }

            await using var stream = await connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, ct).ConfigureAwait(false);
            await stream.WriteAsync(payload, ct).ConfigureAwait(false);
            logger.LogDebug("[Overlay-QUIC-DATA] Sent {Size} bytes to {Endpoint}", payload.Length, endpoint);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Overlay-QUIC-DATA] Failed to send to {Endpoint}", endpoint);
            await RemoveConnectionAsync(endpoint).ConfigureAwait(false);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<Stream?> OpenBidirectionalStreamAsync(IPEndPoint endpoint, CancellationToken ct = default)
    {
        if (!options.Enable)
            return null;
        if (!QuicConnection.IsSupported)
        {
            logger.LogDebug("[Overlay-QUIC-DATA] QUIC not supported, cannot open stream to {Endpoint}", endpoint);
            return null;
        }

        try
        {
            var connection = await GetOrCreateConnectionAsync(endpoint, ct).ConfigureAwait(false);
            if (connection == null)
            {
                return null;
            }

            var stream = await connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, ct).ConfigureAwait(false);
            logger.LogDebug("[Overlay-QUIC-DATA] Opened bidirectional stream to {Endpoint}", endpoint);
            return stream;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Overlay-QUIC-DATA] Failed to open stream to {Endpoint}", endpoint);
            await RemoveConnectionAsync(endpoint).ConfigureAwait(false);
            return null;
        }
    }

    private async Task<QuicConnection?> CreateConnectionAsync(IPEndPoint endpoint, CancellationToken ct)
    {
        try
        {
            var clientOptions = new QuicClientConnectionOptions
            {
                RemoteEndPoint = endpoint,
                DefaultStreamErrorCode = 0x02,
                DefaultCloseErrorCode = 0x02,
                ClientAuthenticationOptions = new SslClientAuthenticationOptions
                {
                    ApplicationProtocols = new List<SslApplicationProtocol> { new SslApplicationProtocol("slskdn-overlay-data") },
                    RemoteCertificateValidationCallback = (_, certificate, chain, errors) =>
                        ValidatePinnedCertificate(endpoint, certificate, chain, errors)
                }
            };

            var connection = await QuicConnection.ConnectAsync(clientOptions, ct);
            logger.LogDebug("[Overlay-QUIC-DATA] Connected to {Endpoint}", endpoint);
            return connection;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Overlay-QUIC-DATA] Failed to connect to {Endpoint}", endpoint);
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
                "[Overlay-QUIC-DATA] Rejecting TLS handshake for {Endpoint}: no certificate provided",
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
                "[Overlay-QUIC-DATA] Rejecting TLS handshake for {Endpoint}: failed to extract SPKI pin",
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

            if (removed != null)
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
            logger.LogDebug(ex, "[Overlay-QUIC-DATA] Failed to dispose connection to {Endpoint} ({Reason})", endpoint, reason);
        }
    }
}
