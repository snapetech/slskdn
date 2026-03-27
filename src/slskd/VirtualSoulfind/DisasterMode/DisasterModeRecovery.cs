// <copyright file="DisasterModeRecovery.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.VirtualSoulfind.DisasterMode;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading;
using System.Threading.Tasks;

public interface IDisasterModeRecovery : IDisposable
{
    Task AttemptRecoveryAsync(CancellationToken ct = default);
    bool ShouldAttemptRecovery();
}

/// <summary>
/// Recovery logic for disaster mode (re-enable Soulseek when healthy).
/// Phase 6D: T-830 - Real implementation.
/// </summary>
public sealed class DisasterModeRecovery : IDisasterModeRecovery
{
    private readonly ILogger<DisasterModeRecovery> logger;
    private readonly ISoulseekHealthMonitor healthMonitor;
    private readonly IDisasterModeCoordinator disasterMode;
    private readonly IOptionsMonitor<slskd.Options> optionsMonitor;
    private DateTimeOffset? lastRecoveryAttempt;
    private int consecutiveHealthyChecks;
    private bool disposed;

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

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        healthMonitor.HealthChanged -= OnHealthChanged;
        disposed = true;
        GC.SuppressFinalize(this);
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
            var checks = Interlocked.Increment(ref consecutiveHealthyChecks);

            // Require multiple consecutive healthy checks before recovery
            var requiredChecks = recoveryOptions?.RecoveryHealthyChecksRequired ?? 3;

            if (checks >= requiredChecks)
            {
                logger.LogInformation("[VSF-RECOVERY] Soulseek healthy for {Checks} checks, deactivating disaster mode",
                    checks);

                await disasterMode.DeactivateDisasterModeAsync(ct);
                Interlocked.Exchange(ref consecutiveHealthyChecks, 0);
            }
            else
            {
                logger.LogDebug("[VSF-RECOVERY] Soulseek healthy but need {Required} consecutive checks (have {Current})",
                    requiredChecks, checks);
            }
        }
        else
        {
            // Reset counter if health is not healthy
            Interlocked.Exchange(ref consecutiveHealthyChecks, 0);
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
        try
        {
            if (e.NewHealth == SoulseekHealth.Healthy && disasterMode.IsDisasterModeActive)
            {
                // Health improved, attempt recovery
                await AttemptRecoveryAsync(CancellationToken.None);
            }
            else if (e.NewHealth != SoulseekHealth.Healthy)
            {
                // Health degraded, reset recovery counter
                Interlocked.Exchange(ref consecutiveHealthyChecks, 0);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("[VSF-RECOVERY] Health-change recovery processing cancelled");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[VSF-RECOVERY] Unhandled exception while processing health change");
        }
    }
}
