// <copyright file="UdpHolePuncher.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace slskd.Mesh.Nat;

/// <summary>
/// Advanced UDP hole punching implementation for NAT traversal.
/// Supports different NAT types and implements robust hole punching strategies.
/// </summary>
public interface IUdpHolePuncher
{
    Task<UdpHolePunchResult> TryPunchAsync(
        IPEndPoint localEp,
        IPEndPoint remoteEp,
        CancellationToken ct = default);

    Task<UdpHolePunchResult> TryPunchWithNatAsync(
        IPEndPoint localEp,
        IPEndPoint remoteEp,
        NatType localNatType,
        NatType remoteNatType,
        CancellationToken ct = default);
}

public record UdpHolePunchResult(bool Success, IPEndPoint? LocalEndpoint, TimeSpan Duration);

public class UdpHolePuncher : IUdpHolePuncher
{
    private readonly ILogger<UdpHolePuncher> logger;

    // Hole punching parameters
    private const int MaxAttempts = 5;
    private const int PacketsPerAttempt = 3;
    private const int PacketIntervalMs = 100;
    private const int ReceiveTimeoutMs = 3000;
    private const int SendTimeoutMs = 1000;

    // Payload patterns for different packet types
    private static readonly byte[] PunchPayload = { 0x50, 0x55, 0x4e, 0x43 }; // "PUNC"
    private static readonly byte[] ResponsePayload = { 0x52, 0x45, 0x53, 0x50 }; // "RESP"

    public UdpHolePuncher(ILogger<UdpHolePuncher> logger)
    {
        this.logger = logger;
    }

    /// <summary>
    /// Attempt basic UDP hole punching.
    /// </summary>
    public async Task<UdpHolePunchResult> TryPunchAsync(
        IPEndPoint localEp,
        IPEndPoint remoteEp,
        CancellationToken ct = default)
    {
        return await TryPunchWithNatAsync(localEp, remoteEp, NatType.Unknown, NatType.Unknown, ct);
    }

    /// <summary>
    /// Attempt UDP hole punching with NAT type awareness.
    /// </summary>
    public async Task<UdpHolePunchResult> TryPunchAsync(
        IPEndPoint localEp,
        IPEndPoint remoteEp,
        NatType localNatType,
        NatType remoteNatType,
        CancellationToken ct = default)
    {
        return await TryPunchWithNatAsync(localEp, remoteEp, localNatType, remoteNatType, ct);
    }

    public async Task<UdpHolePunchResult> TryPunchWithNatAsync(
        IPEndPoint localEp,
        IPEndPoint remoteEp,
        NatType localNatType,
        NatType remoteNatType,
        CancellationToken ct = default)
    {
        var startTime = DateTimeOffset.UtcNow;

        using var udp = new UdpClient(localEp);
        udp.Client.ReceiveTimeout = ReceiveTimeoutMs;
        udp.Client.SendTimeout = SendTimeoutMs;

        try
        {
            logger.LogDebug(
                "[HolePunch] Starting punch from {Local} to {Remote} (LocalNAT: {LocalNat}, RemoteNAT: {RemoteNat})",
                localEp, remoteEp, localNatType, remoteNatType);

            // Determine punching strategy based on NAT types
            var strategy = DeterminePunchStrategy(localNatType, remoteNatType);

            bool success = false;

            for (int attempt = 0; attempt < MaxAttempts && !ct.IsCancellationRequested; attempt++)
            {
                logger.LogDebug("[HolePunch] Attempt {Attempt}/{MaxAttempts}", attempt + 1, MaxAttempts);

                // Send punch packets according to strategy
                await SendPunchPacketsAsync(udp, remoteEp, strategy, ct);

                // Try to receive a response
                if (await TryReceiveResponseAsync(udp, remoteEp, ct))
                {
                    success = true;
                    break;
                }

                // Wait before next attempt
                if (attempt < MaxAttempts - 1)
                {
                    await Task.Delay(PacketIntervalMs * 2, ct);
                }
            }

            var duration = DateTimeOffset.UtcNow - startTime;
            var bound = (IPEndPoint)udp.Client.LocalEndPoint!;

            logger.LogInformation(
                "[HolePunch] Completed in {Duration}ms - {Result} (Local: {Local})",
                duration.TotalMilliseconds, success ? "SUCCESS" : "FAILED", bound);

            return new UdpHolePunchResult(success, bound, duration);
        }
        catch (Exception ex)
        {
            var duration = DateTimeOffset.UtcNow - startTime;
            logger.LogWarning(ex, "[HolePunch] Exception during hole punch to {Remote}", remoteEp);
            return new UdpHolePunchResult(false, null, duration);
        }
    }

    /// <summary>
    /// Determine the optimal punching strategy based on NAT types.
    /// </summary>
    private PunchStrategy DeterminePunchStrategy(NatType localNat, NatType remoteNat)
    {
        // For most NAT types, we use a basic strategy
        // More sophisticated strategies could be implemented for specific NAT combinations
        return new PunchStrategy
        {
            PacketCount = PacketsPerAttempt,
            UsePortPrediction = localNat == NatType.Symmetric || remoteNat == NatType.Symmetric,
            DelayBetweenPackets = PacketIntervalMs
        };
    }

    /// <summary>
    /// Send punch packets according to the strategy.
    /// </summary>
    private async Task SendPunchPacketsAsync(UdpClient udp, IPEndPoint remoteEp, PunchStrategy strategy, CancellationToken ct)
    {
        for (int i = 0; i < strategy.PacketCount; i++)
        {
            await udp.SendAsync(PunchPayload, PunchPayload.Length, remoteEp);

            // For symmetric NAT, also try adjacent ports
            if (strategy.UsePortPrediction)
            {
                // Try a few adjacent ports (simple port prediction)
                for (int offset = -2; offset <= 2; offset++)
                {
                    if (offset == 0) continue; // Already sent to main port

                    var predictedEp = new IPEndPoint(remoteEp.Address, remoteEp.Port + offset);
                    try
                    {
                        await udp.SendAsync(PunchPayload, PunchPayload.Length, predictedEp);
                    }
                    catch
                    {
                        // Ignore send failures for predicted ports
                    }
                }
            }

            if (i < strategy.PacketCount - 1)
            {
                await Task.Delay(strategy.DelayBetweenPackets, ct);
            }
        }
    }

    /// <summary>
    /// Try to receive a response from the remote peer.
    /// </summary>
    private async Task<bool> TryReceiveResponseAsync(UdpClient udp, IPEndPoint remoteEp, CancellationToken ct)
    {
        try
        {
            var receiveTask = udp.ReceiveAsync();

            // Wait for response with timeout
            if (await Task.WhenAny(receiveTask, Task.Delay(ReceiveTimeoutMs, ct)) == receiveTask)
            {
                var result = receiveTask.Result;

                // Verify the response is from the expected peer and contains expected payload
                if (result.RemoteEndPoint.Equals(remoteEp) &&
                    result.Buffer.Length >= 4 &&
                    result.Buffer.AsSpan(0, 4).SequenceEqual(ResponsePayload))
                {
                    logger.LogDebug("[HolePunch] Received valid response from {Remote}", result.RemoteEndPoint);

                    // Send acknowledgment
                    await udp.SendAsync(ResponsePayload, ResponsePayload.Length, remoteEp);

                    return true;
                }
                else
                {
                    logger.LogDebug(
                        "[HolePunch] Received unexpected data from {Remote} (expected {Expected})",
                        result.RemoteEndPoint, remoteEp);
                }
            }
            else
            {
                logger.LogDebug("[HolePunch] No response received within timeout");
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "[HolePunch] Error receiving response");
        }

        return false;
    }
}

/// <summary>
/// Strategy for UDP hole punching.
/// </summary>
internal class PunchStrategy
{
    public int PacketCount { get; set; } = 3;
    public bool UsePortPrediction { get; set; } = false;
    public int DelayBetweenPackets { get; set; } = 100;
}
