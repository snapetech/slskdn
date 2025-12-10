namespace slskd.VirtualSoulfind.DisasterMode;

using Microsoft.Extensions.Options;

/// <summary>
/// Interface for disaster mode recovery.
/// </summary>
public interface IDisasterModeRecovery
{
    /// <summary>
    /// Attempt to recover from disaster mode.
    /// </summary>
    Task AttemptRecoveryAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Check if recovery should be attempted.
    /// </summary>
    bool ShouldAttemptRecovery();
}

/// <summary>
/// Disaster mode recovery logic.
/// </summary>
public class DisasterModeRecovery : IDisasterModeRecovery
{
    private readonly ILogger<DisasterModeRecovery> logger;
    private readonly ISoulseekHealthMonitor healthMonitor;
    private readonly IDisasterModeCoordinator disasterMode;
    private readonly ISoulseekClient soulseek;
    private readonly IOptionsMonitor<Options> optionsMonitor;
    private DateTimeOffset? lastRecoveryAttempt;

    public DisasterModeRecovery(
        ILogger<DisasterModeRecovery> logger,
        ISoulseekHealthMonitor healthMonitor,
        IDisasterModeCoordinator disasterMode,
        ISoulseekClient soulseek,
        IOptionsMonitor<Options> optionsMonitor)
    {
        this.logger = logger;
        this.healthMonitor = healthMonitor;
        this.disasterMode = disasterMode;
        this.soulseek = soulseek;
        this.optionsMonitor = optionsMonitor;
    }

    public bool ShouldAttemptRecovery()
    {
        if (!disasterMode.IsDisasterModeActive)
        {
            return false;
        }

        // Don't attempt recovery too frequently (minimum 5 minutes between attempts)
        if (lastRecoveryAttempt.HasValue)
        {
            var elapsed = DateTimeOffset.UtcNow - lastRecoveryAttempt.Value;
            if (elapsed < TimeSpan.FromMinutes(5))
            {
                return false;
            }
        }

        return true;
    }

    public async Task AttemptRecoveryAsync(CancellationToken ct)
    {
        if (!ShouldAttemptRecovery())
        {
            return;
        }

        logger.LogInformation("[VSF-RECOVERY] Attempting disaster mode recovery");

        lastRecoveryAttempt = DateTimeOffset.UtcNow;

        try
        {
            // Try to reconnect to Soulseek
            if (soulseek.State != SoulseekClientStates.Connected &&
                soulseek.State != SoulseekClientStates.LoggedIn)
            {
                logger.LogDebug("[VSF-RECOVERY] Attempting Soulseek reconnection");

                await soulseek.ConnectAsync(cancellationToken: ct);

                logger.LogInformation("[VSF-RECOVERY] Successfully reconnected to Soulseek");

                // Wait a bit to verify stability
                await Task.Delay(TimeSpan.FromSeconds(30), ct);

                // Check if still healthy
                if (healthMonitor.CurrentHealth == SoulseekHealth.Healthy)
                {
                    logger.LogInformation("[VSF-RECOVERY] Soulseek stable, deactivating disaster mode");
                    await disasterMode.DeactivateDisasterModeAsync(ct);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[VSF-RECOVERY] Recovery attempt failed: {Message}", ex.Message);
        }
    }
}
