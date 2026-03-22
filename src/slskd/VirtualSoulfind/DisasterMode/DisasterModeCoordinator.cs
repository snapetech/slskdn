// <copyright file="DisasterModeCoordinator.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.VirtualSoulfind.DisasterMode;

using Microsoft.Extensions.Options;
using slskd;
using OptionsModel = slskd.Options;

/// <summary>
/// Interface for disaster mode coordination.
/// </summary>
public interface IDisasterModeCoordinator
{
    /// <summary>
    /// Current disaster mode level (0 = normal dual-network operation, higher = more degraded).
    /// </summary>
    DisasterModeLevel CurrentLevel { get; }

    /// <summary>
    /// Is disaster mode currently active (level > 0)?
    /// </summary>
    bool IsDisasterModeActive => CurrentLevel > DisasterModeLevel.Normal;

    /// <summary>
    /// Set disaster mode level.
    /// </summary>
    Task SetDisasterModeLevelAsync(DisasterModeLevel level, string reason, CancellationToken ct = default);

    /// <summary>
    /// Deactivate disaster mode (restore normal dual-network operation).
    /// </summary>
    Task DeactivateDisasterModeAsync(CancellationToken ct = default);

    /// <summary>
    /// Event fired when disaster mode level changes.
    /// </summary>
    event EventHandler<DisasterModeLevelChangedEventArgs> DisasterModeLevelChanged;
}

/// <summary>
/// Disaster mode degradation levels.
/// </summary>
public enum DisasterModeLevel
{
    /// <summary>
    /// Normal operation: Soulseek + mesh networks operating together.
    /// </summary>
    Normal = 0,

    /// <summary>
    /// Soulseek degraded: Mesh assists with additional capacity.
    /// </summary>
    SoulseekDegraded = 1,

    /// <summary>
    /// Soulseek unavailable: Mesh becomes primary network.
    /// </summary>
    SoulseekUnavailable = 2,

    /// <summary>
    /// Full fallback: Shadow-index, relay, and swarm-only operation.
    /// </summary>
    FullFallback = 3
}

/// <summary>
/// Disaster mode level changed event args.
/// </summary>
public class DisasterModeLevelChangedEventArgs : EventArgs
{
    public DisasterModeLevel Level { get; set; }
    public DisasterModeLevel PreviousLevel { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// Coordinates disaster mode level management.
/// </summary>
public class DisasterModeCoordinator : IDisasterModeCoordinator
{
    private readonly ILogger<DisasterModeCoordinator> logger;
    private readonly ISoulseekHealthMonitor healthMonitor;
    private readonly IOptionsMonitor<OptionsModel> optionsMonitor;
    private DisasterModeLevel currentLevel;
    private DateTimeOffset? lastHealthCheck;
    private int consecutiveUnhealthyChecks;

    public DisasterModeCoordinator(
        ILogger<DisasterModeCoordinator> logger,
        ISoulseekHealthMonitor healthMonitor,
        IOptionsMonitor<OptionsModel> optionsMonitor)
    {
        this.logger = logger;
        this.healthMonitor = healthMonitor;
        this.optionsMonitor = optionsMonitor;

        // Subscribe to health changes
        healthMonitor.HealthChanged += OnHealthChanged;

        // Start in normal mode (dual-network operation)
        currentLevel = DisasterModeLevel.Normal;
    }

    public DisasterModeLevel CurrentLevel
    {
        get => currentLevel;
        private set => currentLevel = value;
    }

    public event EventHandler<DisasterModeLevelChangedEventArgs>? DisasterModeLevelChanged;

    public Task DeactivateDisasterModeAsync(CancellationToken ct = default)
    {
        return SetDisasterModeLevelAsync(DisasterModeLevel.Normal, "Manual deactivation", ct);
    }

    public async Task SetDisasterModeLevelAsync(DisasterModeLevel level, string reason, CancellationToken ct = default)
    {
        if (CurrentLevel == level)
        {
            logger.LogDebug("[VSF-DISASTER] Already at disaster mode level {Level}", level);
            return;
        }

        var previousLevel = CurrentLevel;
        logger.LogWarning("[VSF-DISASTER] Changing disaster mode level from {Previous} to {New}: {Reason}",
            previousLevel, level, reason);

        CurrentLevel = level;

        // Emit telemetry
        DisasterModeLevelChanged?.Invoke(this, new DisasterModeLevelChangedEventArgs
        {
            Level = level,
            PreviousLevel = previousLevel,
            Timestamp = DateTimeOffset.UtcNow,
            Reason = reason
        });

        logger.LogInformation("[VSF-DISASTER] Disaster mode level set to {Level} - {Description}",
            level, GetLevelDescription(level));

        await Task.CompletedTask;
    }

    private string GetLevelDescription(DisasterModeLevel level) => level switch
    {
        DisasterModeLevel.Normal => "Soulseek + mesh networks operating together",
        DisasterModeLevel.SoulseekDegraded => "Soulseek degraded, mesh assisting",
        DisasterModeLevel.SoulseekUnavailable => "Soulseek unavailable, mesh primary",
        DisasterModeLevel.FullFallback => "Full fallback: shadow-index, relay, swarm-only",
        _ => "Unknown level"
    };

    private async void OnHealthChanged(object? sender, SoulseekHealthChangedEventArgs e)
    {
        var options = optionsMonitor.CurrentValue;
        var disasterOptions = options.VirtualSoulfind?.DisasterMode;

        // Check if auto mode is enabled
        if (disasterOptions?.Auto != true)
        {
            logger.LogDebug("[VSF-DISASTER] Auto disaster mode disabled, ignoring health change");
            return;
        }

        lastHealthCheck = e.Timestamp;

        if (e.NewHealth == SoulseekHealth.Unavailable)
        {
            consecutiveUnhealthyChecks++;

            var elapsedMinutes = consecutiveUnhealthyChecks * 0.5; // Checks every 30 seconds

            // Progressive escalation based on downtime duration
            DisasterModeLevel targetLevel;
            string reason;

            // 30+ minutes down.
            if (elapsedMinutes >= 30)
            {
                targetLevel = DisasterModeLevel.FullFallback;
                reason = $"Soulseek unavailable for {elapsedMinutes:F1} minutes - full fallback mode";
            }

            // 10+ minutes down.
            else if (elapsedMinutes >= 10)
            {
                targetLevel = DisasterModeLevel.SoulseekUnavailable;
                reason = $"Soulseek unavailable for {elapsedMinutes:F1} minutes - mesh primary";
            }

            // 2+ minutes down.
            else if (elapsedMinutes >= 2)
            {
                targetLevel = DisasterModeLevel.SoulseekDegraded;
                reason = $"Soulseek unavailable for {elapsedMinutes:F1} minutes - mesh assisting";
            }
            else
            {
                return; // Too early to escalate
            }

            if (CurrentLevel < targetLevel)
            {
                await SetDisasterModeLevelAsync(targetLevel, reason, CancellationToken.None);
            }
        }
        else if (e.NewHealth == SoulseekHealth.Healthy)
        {
            consecutiveUnhealthyChecks = 0;

            if (CurrentLevel > DisasterModeLevel.Normal)
            {
                logger.LogInformation("[VSF-DISASTER] Soulseek healthy again, preparing to restore normal operation");

                // Wait a bit to ensure stability
                await Task.Delay(TimeSpan.FromMinutes(1));

                if (healthMonitor.CurrentHealth == SoulseekHealth.Healthy)
                {
                    await DeactivateDisasterModeAsync(CancellationToken.None);
                }
            }
        }
    }
}
