namespace slskd.VirtualSoulfind.DisasterMode;

/// <summary>
/// Soulseek server health status.
/// </summary>
public enum SoulseekHealth
{
    /// <summary>
    /// Connected and responsive.
    /// </summary>
    Healthy,
    
    /// <summary>
    /// Slow or intermittent connection.
    /// </summary>
    Degraded,
    
    /// <summary>
    /// Cannot connect or banned.
    /// </summary>
    Unavailable
}

/// <summary>
/// Interface for Soulseek health monitoring.
/// </summary>
public interface ISoulseekHealthMonitor
{
    /// <summary>
    /// Current health status.
    /// </summary>
    SoulseekHealth CurrentHealth { get; }
    
    /// <summary>
    /// Start monitoring Soulseek health.
    /// </summary>
    Task StartMonitoringAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Event fired when health status changes.
    /// </summary>
    event EventHandler<SoulseekHealthChangedEventArgs> HealthChanged;
}

/// <summary>
/// Health changed event args.
/// </summary>
public class SoulseekHealthChangedEventArgs : EventArgs
{
    public SoulseekHealth OldHealth { get; set; }
    public SoulseekHealth NewHealth { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// Monitors Soulseek server health and detects outages/bans.
/// </summary>
public class SoulseekHealthMonitor : ISoulseekHealthMonitor
{
    private readonly ILogger<SoulseekHealthMonitor> logger;
    private readonly ISoulseekClient soulseek;
    private SoulseekHealth currentHealth = SoulseekHealth.Healthy;

    public SoulseekHealthMonitor(
        ILogger<SoulseekHealthMonitor> logger,
        ISoulseekClient soulseek)
    {
        this.logger = logger;
        this.soulseek = soulseek;
    }

    public SoulseekHealth CurrentHealth
    {
        get => currentHealth;
        private set => currentHealth = value;
    }

    public event EventHandler<SoulseekHealthChangedEventArgs>? HealthChanged;

    public async Task StartMonitoringAsync(CancellationToken ct)
    {
        logger.LogInformation("[VSF-HEALTH] Starting Soulseek health monitoring");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var health = await CheckHealthAsync(ct);

                if (health != CurrentHealth)
                {
                    logger.LogWarning("[VSF-HEALTH] Soulseek health changed: {Old} → {New}",
                        CurrentHealth, health);

                    var oldHealth = CurrentHealth;
                    CurrentHealth = health;

                    HealthChanged?.Invoke(this, new SoulseekHealthChangedEventArgs
                    {
                        OldHealth = oldHealth,
                        NewHealth = health,
                        Timestamp = DateTimeOffset.UtcNow,
                        Reason = GetHealthChangeReason(health)
                    });
                }

                await Task.Delay(TimeSpan.FromSeconds(30), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[VSF-HEALTH] Health check failed");
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
            }
        }

        logger.LogInformation("[VSF-HEALTH] Health monitoring stopped");
    }

    private async Task<SoulseekHealth> CheckHealthAsync(CancellationToken ct)
    {
        // Check connection state
        if (soulseek.State != SoulseekClientStates.Connected &&
            soulseek.State != SoulseekClientStates.LoggedIn)
        {
            logger.LogDebug("[VSF-HEALTH] Soulseek not connected, attempting reconnect");

            // Try to reconnect
            try
            {
                await soulseek.ConnectAsync(cancellationToken: ct);
                return SoulseekHealth.Healthy;
            }
            catch (Exception ex) when (ex.Message.Contains("banned", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogError("[VSF-HEALTH] Detected ban: {Message}", ex.Message);
                return SoulseekHealth.Unavailable;
            }
            catch (Exception ex)
            {
                logger.LogWarning("[VSF-HEALTH] Connection failed: {Message}", ex.Message);
                return SoulseekHealth.Unavailable;
            }
        }

        // Check responsiveness
        try
        {
            var pingTask = Task.Run(async () =>
            {
                // Soulseek.NET doesn't have PingAsync, so we'll use a simple state check
                await Task.Delay(100, ct);
                return soulseek.State == SoulseekClientStates.Connected ||
                       soulseek.State == SoulseekClientStates.LoggedIn;
            });

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5), ct);
            var completedTask = await Task.WhenAny(pingTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                logger.LogWarning("[VSF-HEALTH] Soulseek response timeout");
                return SoulseekHealth.Degraded;
            }

            var isResponsive = await pingTask;
            return isResponsive ? SoulseekHealth.Healthy : SoulseekHealth.Degraded;
        }
        catch (TimeoutException)
        {
            logger.LogWarning("[VSF-HEALTH] Soulseek response timeout");
            return SoulseekHealth.Degraded;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[VSF-HEALTH] Health check exception");
            return SoulseekHealth.Degraded;
        }
    }

    private string GetHealthChangeReason(SoulseekHealth newHealth)
    {
        return newHealth switch
        {
            SoulseekHealth.Healthy => "Connection restored",
            SoulseekHealth.Degraded => "Slow or intermittent connection",
            SoulseekHealth.Unavailable => "Cannot connect to Soulseek server",
            _ => "Unknown"
        };
    }
}
