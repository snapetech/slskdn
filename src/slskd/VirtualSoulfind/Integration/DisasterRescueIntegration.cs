// <copyright file="DisasterRescueIntegration.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.VirtualSoulfind.Integration;

using slskd.VirtualSoulfind.DisasterMode;

/// <summary>
/// Integrates the legacy fallback state with rescue mode.
/// </summary>
public interface IDisasterRescueIntegration
{
    /// <summary>
    /// Check if a transfer should use rescue mode (mesh-only).
    /// </summary>
    bool ShouldUseRescueMode();

    /// <summary>
    /// Get rescue mode reason.
    /// </summary>
    string? GetRescueModeReason();
}

/// <summary>
/// Legacy fallback integration with rescue mode.
/// </summary>
public class DisasterRescueIntegration : IDisasterRescueIntegration
{
    private readonly ILogger<DisasterRescueIntegration> logger;
    private readonly IDisasterModeCoordinator disasterMode;
    private readonly ISoulseekHealthMonitor healthMonitor;

    public DisasterRescueIntegration(
        ILogger<DisasterRescueIntegration> logger,
        IDisasterModeCoordinator disasterMode,
        ISoulseekHealthMonitor healthMonitor)
    {
        this.logger = logger;
        this.disasterMode = disasterMode;
        this.healthMonitor = healthMonitor;
    }

    public bool ShouldUseRescueMode()
    {
        // When the legacy fallback is active, all transfers are "rescue" (mesh-only).
        if (disasterMode.IsDisasterModeActive)
        {
            logger.LogDebug("[VSF-INTEGRATION] Rescue mode active (legacy fallback)");
            return true;
        }

        // Also use rescue mode whenever Soulseek is not fully healthy
        if (healthMonitor.CurrentHealth != SoulseekHealth.Healthy)
        {
            logger.LogDebug("[VSF-INTEGRATION] Rescue mode active ({Health})", healthMonitor.CurrentHealth);
            return true;
        }

        return false;
    }

    public string? GetRescueModeReason()
    {
        if (disasterMode.IsDisasterModeActive)
        {
            return "Legacy fallback active - Soulseek unavailable";
        }

        if (healthMonitor.CurrentHealth == SoulseekHealth.Degraded)
        {
            return "Soulseek connection degraded";
        }

        if (healthMonitor.CurrentHealth == SoulseekHealth.Unavailable)
        {
            return "Soulseek unavailable";
        }

        return null;
    }
}
