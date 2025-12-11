namespace slskd.VirtualSoulfind.Integration;

using slskd.VirtualSoulfind.DisasterMode;

/// <summary>
/// Integrates disaster mode with rescue mode.
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
/// Disaster mode integration with rescue mode.
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
        // In disaster mode, all transfers are "rescue" (mesh-only)
        if (disasterMode.IsDisasterModeActive)
        {
            logger.LogDebug("[VSF-INTEGRATION] Rescue mode active (disaster mode)");
            return true;
        }

        // Also use rescue mode if Soulseek is degraded
        if (healthMonitor.CurrentHealth == SoulseekHealth.Degraded)
        {
            logger.LogDebug("[VSF-INTEGRATION] Rescue mode active (degraded health)");
            return true;
        }

        return false;
    }

    public string? GetRescueModeReason()
    {
        if (disasterMode.IsDisasterModeActive)
        {
            return "Disaster mode active - Soulseek unavailable";
        }

        if (healthMonitor.CurrentHealth == SoulseekHealth.Degraded)
        {
            return "Soulseek connection degraded";
        }

        return null;
    }
}

