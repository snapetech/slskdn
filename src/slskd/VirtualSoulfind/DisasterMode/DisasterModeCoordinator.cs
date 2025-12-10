namespace slskd.VirtualSoulfind.DisasterMode;

using Microsoft.Extensions.Options;

/// <summary>
/// Interface for disaster mode coordination.
/// </summary>
public interface IDisasterModeCoordinator
{
    /// <summary>
    /// Is disaster mode currently active?
    /// </summary>
    bool IsDisasterModeActive { get; }
    
    /// <summary>
    /// Activate disaster mode (mesh-only operation).
    /// </summary>
    Task ActivateDisasterModeAsync(string reason, CancellationToken ct = default);
    
    /// <summary>
    /// Deactivate disaster mode (restore Soulseek).
    /// </summary>
    Task DeactivateDisasterModeAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Event fired when disaster mode is activated or deactivated.
    /// </summary>
    event EventHandler<DisasterModeChangedEventArgs> DisasterModeChanged;
}

/// <summary>
/// Disaster mode changed event args.
/// </summary>
public class DisasterModeChangedEventArgs : EventArgs
{
    public bool IsActive { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// Coordinates disaster mode activation/deactivation.
/// </summary>
public class DisasterModeCoordinator : IDisasterModeCoordinator
{
    private readonly ILogger<DisasterModeCoordinator> logger;
    private readonly ISoulseekHealthMonitor healthMonitor;
    private readonly IOptionsMonitor<Options> optionsMonitor;
    private bool isDisasterModeActive;
    private DateTimeOffset? lastHealthCheck;
    private int consecutiveUnhealthyChecks;

    public DisasterModeCoordinator(
        ILogger<DisasterModeCoordinator> logger,
        ISoulseekHealthMonitor healthMonitor,
        IOptionsMonitor<Options> optionsMonitor)
    {
        this.logger = logger;
        this.healthMonitor = healthMonitor;
        this.optionsMonitor = optionsMonitor;

        // Subscribe to health changes
        healthMonitor.HealthChanged += OnHealthChanged;
    }

    public bool IsDisasterModeActive
    {
        get => isDisasterModeActive;
        private set => isDisasterModeActive = value;
    }

    public event EventHandler<DisasterModeChangedEventArgs>? DisasterModeChanged;

    public async Task ActivateDisasterModeAsync(string reason, CancellationToken ct)
    {
        if (IsDisasterModeActive)
        {
            logger.LogDebug("[VSF-DISASTER] Disaster mode already active");
            return;
        }

        logger.LogWarning("[VSF-DISASTER] Activating disaster mode: {Reason}", reason);

        IsDisasterModeActive = true;

        // Emit telemetry
        DisasterModeChanged?.Invoke(this, new DisasterModeChangedEventArgs
        {
            IsActive = true,
            Timestamp = DateTimeOffset.UtcNow,
            Reason = reason
        });

        logger.LogInformation("[VSF-DISASTER] Disaster mode activated - all operations now mesh-only");

        await Task.CompletedTask;
    }

    public async Task DeactivateDisasterModeAsync(CancellationToken ct)
    {
        if (!IsDisasterModeActive)
        {
            logger.LogDebug("[VSF-DISASTER] Disaster mode already inactive");
            return;
        }

        logger.LogInformation("[VSF-DISASTER] Deactivating disaster mode - restoring Soulseek");

        IsDisasterModeActive = false;
        consecutiveUnhealthyChecks = 0;

        // Emit telemetry
        DisasterModeChanged?.Invoke(this, new DisasterModeChangedEventArgs
        {
            IsActive = false,
            Timestamp = DateTimeOffset.UtcNow,
            Reason = "Soulseek connection restored"
        });

        logger.LogInformation("[VSF-DISASTER] Disaster mode deactivated - Soulseek operations resumed");

        await Task.CompletedTask;
    }

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

            var threshold = disasterOptions.UnavailableThresholdMinutes ?? 10;
            var elapsedMinutes = consecutiveUnhealthyChecks * 0.5; // Checks every 30 seconds

            if (elapsedMinutes >= threshold)
            {
                await ActivateDisasterModeAsync(
                    $"Soulseek unavailable for {elapsedMinutes:F1} minutes",
                    CancellationToken.None);
            }
        }
        else if (e.NewHealth == SoulseekHealth.Healthy)
        {
            consecutiveUnhealthyChecks = 0;

            if (IsDisasterModeActive)
            {
                logger.LogInformation("[VSF-DISASTER] Soulseek healthy again, preparing to deactivate disaster mode");

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
