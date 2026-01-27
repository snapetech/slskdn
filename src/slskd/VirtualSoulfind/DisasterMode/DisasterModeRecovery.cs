// <copyright file="DisasterModeRecovery.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.VirtualSoulfind.DisasterMode;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading;
using System.Threading.Tasks;

public interface IDisasterModeRecovery
{
    Task AttemptRecoveryAsync(CancellationToken ct = default);
    bool ShouldAttemptRecovery();
}

/// <summary>
/// Recovery logic for disaster mode (re-enable Soulseek when healthy).
/// Phase 6D: T-830 - Real implementation.
/// </summary>
public class DisasterModeRecovery : IDisasterModeRecovery
{
    private readonly ILogger<DisasterModeRecovery> logger;
    private readonly ISoulseekHealthMonitor healthMonitor;
    private readonly IDisasterModeCoordinator disasterMode;
    private readonly IOptionsMonitor<slskd.Options> optionsMonitor;
    private DateTimeOffset? lastRecoveryAttempt;
    private int consecutiveHealthyChecks;

    public DisasterModeRecovery(
        ILogger<DisasterModeRecovery> logger,
        ISoulseekHealthMonitor healthMonitor,
        IDisasterModeCoordinator disasterMode,
        IOptionsMonitor<slskd.Options> optionsMonitor)
    {
        this.logger = logger;
        this.healthMonitor = healthMonitor;
        this.disasterMode = disasterMode;
        this.optionsMonitor = optionsMonitor;

        // Subscribe to health changes
        healthMonitor.HealthChanged += OnHealthChanged;
    }

    public async Task AttemptRecoveryAsync(CancellationToken ct = default)
    {
        if (!disasterMode.IsDisasterModeActive)
        {
            logger.LogDebug("[VSF-RECOVERY] Disaster mode not active, no recovery needed");
            return;
        }

        var options = optionsMonitor.CurrentValue;
        var recoveryOptions = options.VirtualSoulfind?.DisasterMode;

        // Check if enough time has passed since last attempt
        if (lastRecoveryAttempt.HasValue)
        {
            var timeSinceLastAttempt = DateTimeOffset.UtcNow - lastRecoveryAttempt.Value;
            var minInterval = TimeSpan.FromMinutes(recoveryOptions?.RecoveryCheckIntervalMinutes ?? 5);
            
            if (timeSinceLastAttempt < minInterval)
            {
                logger.LogDebug("[VSF-RECOVERY] Recovery attempt too soon, waiting");
                return;
            }
        }

        lastRecoveryAttempt = DateTimeOffset.UtcNow;

        logger.LogInformation("[VSF-RECOVERY] Attempting recovery from disaster mode");

        // Check current health
        var health = healthMonitor.CurrentHealth;

        if (health == SoulseekHealth.Healthy)
        {
            consecutiveHealthyChecks++;
            
            // Require multiple consecutive healthy checks before recovery
            var requiredChecks = recoveryOptions?.RecoveryHealthyChecksRequired ?? 3;
            
            if (consecutiveHealthyChecks >= requiredChecks)
            {
                logger.LogInformation("[VSF-RECOVERY] Soulseek healthy for {Checks} checks, deactivating disaster mode",
                    consecutiveHealthyChecks);
                
                await disasterMode.DeactivateDisasterModeAsync(ct);
                consecutiveHealthyChecks = 0;
            }
            else
            {
                logger.LogDebug("[VSF-RECOVERY] Soulseek healthy but need {Required} consecutive checks (have {Current})",
                    requiredChecks, consecutiveHealthyChecks);
            }
        }
        else
        {
            // Reset counter if health is not healthy
            consecutiveHealthyChecks = 0;
            logger.LogDebug("[VSF-RECOVERY] Soulseek not healthy ({Health}), recovery not possible", health);
        }
    }

    public bool ShouldAttemptRecovery()
    {
        if (!disasterMode.IsDisasterModeActive)
        {
            return false;
        }

        var health = healthMonitor.CurrentHealth;
        return health == SoulseekHealth.Healthy || health == SoulseekHealth.Degraded;
    }

    private async void OnHealthChanged(object? sender, SoulseekHealthChangedEventArgs e)
    {
        if (e.NewHealth == SoulseekHealth.Healthy && disasterMode.IsDisasterModeActive)
        {
            // Health improved, attempt recovery
            await AttemptRecoveryAsync(CancellationToken.None);
        }
        else if (e.NewHealth != SoulseekHealth.Healthy)
        {
            // Health degraded, reset recovery counter
            consecutiveHealthyChecks = 0;
        }
    }
}
