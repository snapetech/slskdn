namespace slskd.VirtualSoulfind.DisasterMode;

/// <summary>
/// Graceful degradation manager.
/// </summary>
public interface IGracefulDegradationService
{
    /// <summary>
    /// Check if Soulseek should be used for a specific operation.
    /// </summary>
    bool ShouldUseSoulseek(OperationType operationType);
    
    /// <summary>
    /// Check if mesh should be used for a specific operation.
    /// </summary>
    bool ShouldUseMesh(OperationType operationType);
}

/// <summary>
/// Operation type for graceful degradation.
/// </summary>
public enum OperationType
{
    Search,
    Transfer,
    Browse,
    Chat
}

/// <summary>
/// Manages graceful degradation between Soulseek and mesh.
/// </summary>
public class GracefulDegradationService : IGracefulDegradationService
{
    private readonly ILogger<GracefulDegradationService> logger;
    private readonly ISoulseekHealthMonitor healthMonitor;
    private readonly IDisasterModeCoordinator disasterMode;

    public GracefulDegradationService(
        ILogger<GracefulDegradationService> logger,
        ISoulseekHealthMonitor healthMonitor,
        IDisasterModeCoordinator disasterMode)
    {
        this.logger = logger;
        this.healthMonitor = healthMonitor;
        this.disasterMode = disasterMode;
    }

    public bool ShouldUseSoulseek(OperationType operationType)
    {
        // If disaster mode is active, never use Soulseek
        if (disasterMode.IsDisasterModeActive)
        {
            logger.LogDebug("[VSF-DEGRADATION] Disaster mode active, Soulseek disabled for {OpType}",
                operationType);
            return false;
        }

        // Check health status
        var health = healthMonitor.CurrentHealth;

        return health switch
        {
            SoulseekHealth.Healthy => true,
            SoulseekHealth.Degraded => operationType switch
            {
                // Use Soulseek for transfers even when degraded (might still work)
                OperationType.Transfer => true,
                // Avoid Soulseek for searches when degraded (slow)
                OperationType.Search => false,
                _ => true
            },
            SoulseekHealth.Unavailable => false,
            _ => false
        };
    }

    public bool ShouldUseMesh(OperationType operationType)
    {
        // Always allow mesh operations
        return true;
    }
}

/// <summary>
/// Disaster mode configuration.
/// </summary>
public class DisasterModeOptions
{
    /// <summary>
    /// Auto-detect and activate disaster mode.
    /// </summary>
    public bool Auto { get; set; } = true;
    
    /// <summary>
    /// Force disaster mode (for testing).
    /// </summary>
    public bool Force { get; set; } = false;
    
    /// <summary>
    /// Unavailable threshold in minutes before activating.
    /// </summary>
    public int UnavailableThresholdMinutes { get; set; } = 10;
    
    /// <summary>
    /// Enable graceful degradation (partial Soulseek use).
    /// </summary>
    public bool EnableGracefulDegradation { get; set; } = true;
}
