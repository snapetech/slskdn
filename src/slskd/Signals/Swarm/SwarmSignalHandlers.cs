// <copyright file="SwarmSignalHandlers.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Signals.Swarm;

using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using slskd.Signals;
using slskd.Swarm;
using slskd.Security;

public class StubBitTorrentBackend : IBitTorrentBackend 
{ 
    public bool IsSupported() => false;
    public Task<string> PreparePrivateTorrentAsync(SwarmJob job, string variantId, CancellationToken ct = default) => 
        Task.FromResult(string.Empty);
    public Task<string?> FetchByInfoHashOrMagnetAsync(string backendRef, string destDirectory, CancellationToken ct = default) => 
        Task.FromResult<string?>(null);
}

/// <summary>
/// Signal handlers for Swarm control signals.
/// </summary>
public class SwarmSignalHandlers
{
    private readonly ILogger<SwarmSignalHandlers> logger;
    private readonly ISignalBus signalBus;
    private readonly ISwarmJobStore swarmJobStore;
    private readonly ISecurityPolicyEngine securityPolicyEngine;
    private readonly IBitTorrentBackend bitTorrentBackend;
    private readonly string localPeerId;

    public SwarmSignalHandlers(
        ILogger<SwarmSignalHandlers> logger,
        ISignalBus signalBus,
        ISwarmJobStore swarmJobStore,
        ISecurityPolicyEngine securityPolicyEngine,
        IBitTorrentBackend bitTorrentBackend,
        string localPeerId)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.signalBus = signalBus ?? throw new ArgumentNullException(nameof(signalBus));
        this.swarmJobStore = swarmJobStore ?? throw new ArgumentNullException(nameof(swarmJobStore));
        this.securityPolicyEngine = securityPolicyEngine ?? throw new ArgumentNullException(nameof(securityPolicyEngine));
        this.bitTorrentBackend = bitTorrentBackend ?? throw new ArgumentNullException(nameof(bitTorrentBackend));
        this.localPeerId = localPeerId ?? throw new ArgumentNullException(nameof(localPeerId));
    }

    /// <summary>
    /// Initialize signal handlers by subscribing to relevant signal types.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await signalBus.SubscribeAsync(HandleSignalAsync, cancellationToken);
        logger.LogInformation("Swarm signal handlers initialized");
    }

    private async Task HandleSignalAsync(Signal signal, CancellationToken cancellationToken)
    {
        if (signal == null)
            return;

        // Only handle signals addressed to us
        if (signal.ToPeerId != localPeerId)
            return;

        switch (signal.Type)
        {
            case "Swarm.RequestBtFallback":
                await HandleRequestBtFallbackAsync(signal, cancellationToken);
                break;

            case "Swarm.RequestBtFallbackAck":
                await HandleRequestBtFallbackAckAsync(signal, cancellationToken);
                break;

            case "Swarm.JobCancel":
                await HandleJobCancelAsync(signal, cancellationToken);
                break;

            default:
                // Unknown signal type, ignore
                break;
        }
    }

    /// <summary>
    /// Handle Swarm.RequestBtFallback signal (receiver side).
    /// </summary>
    private async Task HandleRequestBtFallbackAsync(Signal signal, CancellationToken cancellationToken)
    {
        try
        {
            signal.Body.TryGetValue("jobId", out var jobIdObj);
            signal.Body.TryGetValue("variantId", out var variantIdObj);
            signal.Body.TryGetValue("reason", out var reasonObj);
            var jobId = jobIdObj?.ToString();
            var variantId = variantIdObj?.ToString();
            var reason = reasonObj?.ToString();

            if (string.IsNullOrWhiteSpace(jobId) || string.IsNullOrWhiteSpace(variantId))
            {
                await SendBtFallbackAckAsync(signal, accepted: false, reason: "missing-job-id-or-variant-id", cancellationToken, null);
                return;
            }

            // Validate job exists and has this variant
            var job = await swarmJobStore.TryGetJobAsync(jobId, cancellationToken);
            if (job == null || !job.HasVariant(variantId))
            {
                await SendBtFallbackAckAsync(signal, accepted: false, reason: "unknown-job-or-variant", cancellationToken, null);
                return;
            }

            // Evaluate security / trust
            var decision = await securityPolicyEngine.EvaluateAsync(
                SecurityContext.ForBtFallback(
                    fromPeerId: signal.FromPeerId,
                    jobId: jobId,
                    variantId: variantId),
                cancellationToken);

            if (!decision.Allowed)
            {
                await SendBtFallbackAckAsync(signal, accepted: false, reason: "security-denied", cancellationToken);
                return;
            }

            if (!bitTorrentBackend.IsSupported())
            {
                await SendBtFallbackAckAsync(signal, accepted: false, reason: "bt-backend-disabled", cancellationToken, null);
                return;
            }

            // Accept the fallback
            var btFallbackId = await bitTorrentBackend.PreparePrivateTorrentAsync(job, variantId, cancellationToken);

            await SendBtFallbackAckAsync(signal, accepted: true, reason: "ok", cancellationToken, btFallbackId);

            logger.LogInformation("Accepted BT fallback request for job {JobId}, variant {VariantId}", jobId, variantId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling RequestBtFallback signal {SignalId}", signal.SignalId);
            await SendBtFallbackAckAsync(signal, accepted: false, reason: "internal-error", cancellationToken, null);
        }
    }

    /// <summary>
    /// Handle Swarm.RequestBtFallbackAck signal (sender side).
    /// </summary>
    private async Task HandleRequestBtFallbackAckAsync(Signal signal, CancellationToken cancellationToken)
    {
        try
        {
            signal.Body.TryGetValue("jobId", out var jobIdObj);
            signal.Body.TryGetValue("variantId", out var variantIdObj);
            signal.Body.TryGetValue("accepted", out var acceptedObj);
            signal.Body.TryGetValue("reason", out var reasonObj);
            signal.Body.TryGetValue("btFallbackId", out var btFallbackIdObj);
            var jobId = jobIdObj?.ToString();
            var variantId = variantIdObj?.ToString();
            var accepted = acceptedObj is bool acc && acc;
            var reason = reasonObj?.ToString();
            var btFallbackId = btFallbackIdObj?.ToString();

            if (string.IsNullOrWhiteSpace(jobId) || string.IsNullOrWhiteSpace(variantId))
            {
                logger.LogWarning("Received invalid RequestBtFallbackAck signal {SignalId}", signal.SignalId);
                return;
            }

            // TODO: Look up pending fallback request and handle ack
            // For now, just log
            if (accepted)
            {
                logger.LogInformation("BT fallback accepted for job {JobId}, variant {VariantId}, btFallbackId: {BtFallbackId}",
                    jobId, variantId, btFallbackId);
                // TODO: Enable BT fallback in SwarmCore
            }
            else
            {
                logger.LogInformation("BT fallback rejected for job {JobId}, variant {VariantId}, reason: {Reason}",
                    jobId, variantId, reason);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling RequestBtFallbackAck signal {SignalId}", signal.SignalId);
        }
    }

    /// <summary>
    /// Handle Swarm.JobCancel signal.
    /// </summary>
    private async Task HandleJobCancelAsync(Signal signal, CancellationToken cancellationToken)
    {
        try
        {
            signal.Body.TryGetValue("jobId", out var jobIdObj);
            var jobId = jobIdObj?.ToString();

            if (string.IsNullOrWhiteSpace(jobId))
            {
                logger.LogWarning("Received invalid JobCancel signal {SignalId}", signal.SignalId);
                return;
            }

            // TODO: Cancel the job in SwarmCore
            logger.LogInformation("Job cancellation requested for job {JobId} from peer {PeerId}", jobId, signal.FromPeerId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling JobCancel signal {SignalId}", signal.SignalId);
        }
    }

    /// <summary>
    /// Send a BT fallback acknowledgment signal.
    /// </summary>
    private async Task SendBtFallbackAckAsync(
        Signal requestSignal,
        bool accepted,
        string? reason,
        CancellationToken cancellationToken,
        string? btFallbackId = null)
    {
        var ack = new Signal(
            signalId: Guid.NewGuid().ToString("N"),
            fromPeerId: localPeerId,
            toPeerId: requestSignal.FromPeerId,
            sentAt: DateTimeOffset.UtcNow,
            type: "Swarm.RequestBtFallbackAck",
            body: new Dictionary<string, object>
            {
                ["jobId"] = requestSignal.Body["jobId"] ?? string.Empty,
                ["variantId"] = requestSignal.Body["variantId"] ?? string.Empty,
                ["accepted"] = accepted,
                ["reason"] = reason ?? string.Empty,
                ["btFallbackId"] = btFallbackId ?? string.Empty
            },
            ttl: TimeSpan.FromMinutes(5),
            preferredChannels: new[]
            {
                SignalChannel.Mesh,
                SignalChannel.BtExtension
            });

        await signalBus.SendAsync(ack, cancellationToken);
    }
}

/// <summary>
/// Security policy engine stub for testing.
/// </summary>
public class StubSecurityPolicyEngine : ISecurityPolicyEngine
{
    public Task<SecurityDecision> EvaluateAsync(SecurityContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new SecurityDecision { Allowed = true, Reason = "Stub allows all" });
    }
}

/// <summary>
/// Extension methods for SwarmJob to check variant membership.
/// </summary>
public static class SwarmJobExtensions
{
    public static bool HasVariant(this SwarmJob job, string variantId)
    {
        // TODO: Implement actual variant check
        // For now, assume all jobs have variants
        return true;
    }
}

/// <summary>
/// Interface for security policy evaluation.
/// </summary>
public interface ISecurityPolicyEngine
{
    Task<SecurityDecision> EvaluateAsync(SecurityContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Security context for BT fallback evaluation.
/// </summary>
public class SecurityContext
{
    public string FromPeerId { get; set; } = string.Empty;
    public string? JobId { get; set; }
    public string? VariantId { get; set; }

    public static SecurityContext ForBtFallback(string fromPeerId, string jobId, string variantId)
    {
        return new SecurityContext
        {
            FromPeerId = fromPeerId,
            JobId = jobId,
            VariantId = variantId
        };
    }
}

/// <summary>
/// Security policy decision result.
/// </summary>
public class SecurityDecision
{
    public bool Allowed { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// Interface for BitTorrent backend operations.
/// </summary>
public interface IBitTorrentBackend
{
    bool IsSupported();
    Task<string> PreparePrivateTorrentAsync(SwarmJob job, string variantId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Fetches content by infohash or magnet URI. Used by the VirtualSoulfind resolver for
    ///     ContentBackendType.Torrent. Returns the path to the fetched file when complete, or null
    ///     if not supported (e.g. StubBitTorrentBackend) or when the fetch fails.
    /// </summary>
    /// <param name="backendRef">Infohash (40 or 64 hex chars) or magnet URI.</param>
    /// <param name="destDirectory">Directory to write the first/only file into.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Absolute path to the fetched file, or null if not supported or failed.</returns>
    Task<string?> FetchByInfoHashOrMagnetAsync(string backendRef, string destDirectory, CancellationToken ct = default);
}
